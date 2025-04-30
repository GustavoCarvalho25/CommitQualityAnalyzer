using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Serviço para interação com o modelo Ollama
    /// </summary>
    public class OllamaService
    {
        private readonly ILogger<OllamaService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly int _maxPromptLength;

        public OllamaService(
            ILogger<OllamaService> logger,
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClient;
            
            // Configurar timeout mais longo para o HttpClient
            int timeoutMinutes = _configuration.GetValue<int>("Ollama:TimeoutMinutes", 10);
            _httpClient.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
            
            _maxPromptLength = _configuration.GetValue<int>("Ollama:MaxPromptLength", 8000);
            
            _logger.LogInformation("OllamaService inicializado com timeout de {TimeoutMinutes} minutos e tamanho máximo de prompt de {MaxPromptLength} caracteres",
                timeoutMinutes, _maxPromptLength);
        }

        /// <summary>
        /// Processa uma parte do prompt usando o modelo Ollama
        /// </summary>
        public async Task<string> ProcessPromptPart(string promptPart, string modelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(promptPart))
            {
                _logger.LogWarning("Prompt vazio, não será processado");
                return string.Empty;
            }

            try
            {
                _logger.LogDebug("Processando prompt de {PromptLength} caracteres com modelo {ModelName}", 
                    promptPart.Length, modelName);

                var baseUrl = _configuration.GetValue<string>("Ollama:BaseUrl", "http://localhost:11434");
                var apiUrl = $"{baseUrl}/api/generate";

                // Configurar opções do modelo com base no tamanho do prompt
                double temperature = 0.1;
                double top_p = 0.9;
                int top_k = 40;
                
                // Para prompts maiores, usar configurações mais conservadoras
                if (promptPart.Length > 1000)
                {
                    temperature = 0.05; // Menor temperatura para respostas mais determinísticas
                    top_p = 0.95; // Maior top_p para considerar mais tokens
                }
                
                // Obter o tamanho do contexto da configuração
                int contextLength = _configuration.GetValue<int>("Ollama:ContextLength", 4096);
                
                var requestData = new
                {
                    model = modelName,
                    prompt = promptPart,
                    stream = false,
                    options = new
                    {
                        temperature,
                        top_p,
                        top_k,
                        num_ctx = contextLength
                    }
                };
                
                _logger.LogInformation("Usando num_ctx = {ContextLength} para o modelo {ModelName}", contextLength, modelName);

                // Criar um novo token de cancelação com timeout específico para esta requisição
                int timeoutSeconds = _configuration.GetValue<int>("Ollama:RequestTimeoutSeconds", 300); // 5 minutos por padrão
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                
                try
                {
                    _logger.LogInformation("Enviando requisição para Ollama com timeout de {TimeoutSeconds} segundos", timeoutSeconds);
                    var response = await _httpClient.PostAsJsonAsync(apiUrl, requestData, linkedCts.Token);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Erro ao chamar API Ollama: {StatusCode} - {ReasonPhrase}", 
                            response.StatusCode, response.ReasonPhrase);
                        return string.Empty;
                    }

                    var responseContent = await response.Content.ReadAsStringAsync(linkedCts.Token);
                    
                    if (string.IsNullOrEmpty(responseContent))
                    {
                        _logger.LogError("Resposta vazia da API Ollama");
                        return string.Empty;
                    }

                    try
                    {
                        var jsonResponse = JsonDocument.Parse(responseContent);
                        var responseText = jsonResponse.RootElement.GetProperty("response").GetString();
                        
                        if (string.IsNullOrEmpty(responseText))
                        {
                            _logger.LogError("Texto de resposta vazio no JSON retornado pela API Ollama");
                            return string.Empty;
                        }

                        // Limpar códigos ANSI que podem estar presentes na resposta
                        responseText = CleanAnsiCodes(responseText);
                        
                        _logger.LogDebug("Resposta processada com sucesso: {ResponseLength} caracteres", 
                            responseText.Length);
                        
                        return responseText;
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Erro ao processar JSON da resposta: {ErrorMessage}", jsonEx.Message);
                        return string.Empty;
                    }
                }
                catch (OperationCanceledException)
                {
                    if (timeoutCts.IsCancellationRequested)
                    {
                        _logger.LogError("Timeout ao processar prompt após {TimeoutSeconds} segundos", timeoutSeconds);
                        return "[TIMEOUT] A operação excedeu o tempo limite.";
                    }
                    else
                    {
                        _logger.LogWarning("Operação cancelada pelo usuário");
                        throw; // Propagar o cancelamento do usuário
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    _logger.LogError(httpEx, "Erro de conexão com a API Ollama: {ErrorMessage}", httpEx.Message);
                    return "[ERRO DE CONEXÃO] Não foi possível conectar ao servidor Ollama.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar prompt: {ErrorMessage}", ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Processa um prompt completo, dividindo-o em partes se necessário
        /// </summary>
        public async Task<string> ProcessPrompt(string prompt, string modelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                _logger.LogWarning("Prompt vazio, não será processado");
                return string.Empty;
            }

            try
            {
                _logger.LogInformation("Processando prompt completo de {PromptLength} caracteres", prompt.Length);
                
                // Verificar se o prompt precisa ser dividido
                if (prompt.Length > 500000)
                {
                    _logger.LogError("Prompt muito grande ({PromptLength} caracteres). Limite máximo: 500000 caracteres", prompt.Length);
                    return "[ERRO] Prompt excede o tamanho máximo permitido de 500000 caracteres.";
                }
                
                // Para prompts pequenos, processar diretamente
                if (prompt.Length <= _maxPromptLength)
                {
                    _logger.LogInformation("Processando prompt completo diretamente");
                    return await ProcessPromptPart(prompt, modelName, cancellationToken);
                }
                
                // Para prompts maiores, dividir em partes
                _logger.LogInformation("Dividindo prompt em partes para processamento");
                var promptParts = SplitPromptIntoParts(prompt);
                _logger.LogInformation("Prompt dividido em {PartCount} partes", promptParts.Count);
                
                var results = new List<string>();
                
                for (int i = 0; i < promptParts.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Processamento cancelado pelo usuário após {ProcessedParts} partes", i);
                        break;
                    }
                    
                    _logger.LogInformation("Processando parte {PartNumber} de {TotalParts}", i + 1, promptParts.Count);
                    var result = await ProcessPromptPart(promptParts[i], modelName, cancellationToken);
                    
                    if (!string.IsNullOrEmpty(result))
                    {
                        results.Add(result);
                        _logger.LogInformation("Parte {PartNumber} processada com sucesso", i + 1);
                    }
                    else
                    {
                        _logger.LogWarning("Falha ao processar parte {PartNumber}", i + 1);
                    }
                }
                
                // Se não conseguiu processar nenhuma parte, retornar vazio
                if (results.Count == 0)
                {
                    _logger.LogError("Não foi possível processar nenhuma parte do prompt");
                    return string.Empty;
                }
                
                // Combinar os resultados
                return CombineResults(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar prompt completo: {ErrorMessage}", ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Testa a conexão com o servidor Ollama
        /// </summary>
        public async Task<bool> TestOllamaConnection(string modelName)
        {
            try
            {
                _logger.LogInformation("Testando conexão com Ollama usando modelo {ModelName}", modelName);
                
                var baseUrl = _configuration.GetValue<string>("Ollama:BaseUrl", "http://localhost:11434");
                var apiUrl = $"{baseUrl}/api/generate";

                // Obter o tamanho do contexto da configuração
                int contextLength = _configuration.GetValue<int>("Ollama:ContextLength", 4096);
                
                var requestData = new
                {
                    model = modelName,
                    prompt = "Teste de conexão. Responda apenas com 'OK'.",
                    stream = false,
                    options = new
                    {
                        temperature = 0.1,
                        top_p = 0.9,
                        top_k = 40,
                        num_ctx = contextLength
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(apiUrl, requestData);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Falha no teste de conexão: {StatusCode} - {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    return false;
                }

                _logger.LogInformation("Conexão com Ollama estabelecida com sucesso");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao testar conexão com Ollama: {ErrorMessage}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Divide um prompt em partes menores para processamento
        /// </summary>
        private List<string> SplitPromptIntoParts(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
                return new List<string>();

            // Tamanho máximo para cada parte (2000 caracteres é um bom limite para modelos como o Ollama)
            int maxPartLength = _configuration.GetValue<int>("Ollama:MaxPartLength", 2000);
            
            if (prompt.Length <= maxPartLength)
                return new List<string> { prompt };

            var parts = new List<string>();
            int totalParts = (int)Math.Ceiling((double)prompt.Length / maxPartLength);
            
            _logger.LogInformation("Dividindo prompt de {PromptLength} caracteres em aproximadamente {TotalParts} partes", 
                prompt.Length, totalParts);
            
            int startIndex = 0;
            int partNumber = 1;
            
            while (startIndex < prompt.Length)
            {
                // Calcular o tamanho desta parte
                int remainingLength = prompt.Length - startIndex;
                int partLength = Math.Min(maxPartLength, remainingLength);
                
                // Tentar encontrar um ponto natural para quebrar (parágrafo, ponto final, vírgula)
                if (partLength < remainingLength)
                {
                    int breakIndex = -1;
                    
                    // Procurar por quebra de parágrafo
                    int paragraphBreak = prompt.IndexOf("\n\n", startIndex + partLength - 100, Math.Min(200, remainingLength));
                    if (paragraphBreak >= 0 && paragraphBreak < startIndex + partLength)
                    {
                        breakIndex = paragraphBreak + 2; // +2 para incluir a quebra de parágrafo
                    }
                    
                    // Se não encontrou quebra de parágrafo, procurar por ponto final
                    if (breakIndex < 0)
                    {
                        int sentenceBreak = prompt.LastIndexOf(". ", startIndex + partLength - 100, Math.Min(200, remainingLength));
                        if (sentenceBreak >= 0 && sentenceBreak < startIndex + partLength)
                        {
                            breakIndex = sentenceBreak + 2; // +2 para incluir o ponto e o espaço
                        }
                    }
                    
                    // Se não encontrou ponto final, procurar por vírgula
                    if (breakIndex < 0)
                    {
                        int commaBreak = prompt.LastIndexOf(", ", startIndex + partLength - 50, Math.Min(100, remainingLength));
                        if (commaBreak >= 0 && commaBreak < startIndex + partLength)
                        {
                            breakIndex = commaBreak + 2; // +2 para incluir a vírgula e o espaço
                        }
                    }
                    
                    // Se encontrou um ponto de quebra, ajustar o tamanho da parte
                    if (breakIndex > 0)
                    {
                        partLength = breakIndex - startIndex;
                    }
                }
                
                string part = prompt.Substring(startIndex, partLength);
                
                // Adicionar cabeçalho informativo para cada parte
                string header = $"[PARTE {partNumber} DE ~{totalParts}] ";
                if (partNumber > 1)
                {
                    header += "Continuação da análise. ";
                }
                
                parts.Add(header + part);
                startIndex += partLength;
                partNumber++;
            }
            
            // Verificação final para garantir que nenhuma parte exceda o limite
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Length > maxPartLength)
                {
                    _logger.LogWarning("Parte {PartNumber} excede o tamanho máximo ({ActualLength} > {MaxLength}). Truncando...", 
                        i + 1, parts[i].Length, maxPartLength);
                    parts[i] = parts[i].Substring(0, maxPartLength);
                }
            }
            
            return parts;
        }

        /// <summary>
        /// Combina os resultados de múltiplas partes de prompt
        /// </summary>
        public string CombineResults(List<string> results)
        {
            if (results == null || results.Count == 0)
                return string.Empty;

            if (results.Count == 1)
                return results[0];

            var combinedResult = new StringBuilder();
            
            // Tentar extrair JSON de cada resultado e combiná-los
            var jsonResults = new List<string>();
            
            foreach (var result in results)
            {
                var jsonMatch = Regex.Match(result, @"```(?:json)?\s*({[\s\S]*?})\s*```", RegexOptions.Singleline);
                if (jsonMatch.Success && jsonMatch.Groups.Count > 1)
                {
                    jsonResults.Add(jsonMatch.Groups[1].Value.Trim());
                }
                else
                {
                    // Se não encontrar JSON, adicionar o texto completo
                    combinedResult.AppendLine(result);
                }
            }
            
            // Se encontrou JSON em algum resultado, tentar combinar
            if (jsonResults.Count > 0)
            {
                try
                {
                    // Usar o primeiro JSON como base
                    var baseJson = jsonResults[0];
                    
                    if (jsonResults.Count == 1)
                    {
                        return $"```json\n{baseJson}\n```";
                    }
                    
                    // Tentar mesclar os JSONs
                    var baseDoc = JsonDocument.Parse(baseJson);
                    var resultObject = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(baseJson);
                    
                    // Mesclar os demais JSONs
                    for (int i = 1; i < jsonResults.Count; i++)
                    {
                        try
                        {
                            var additionalJson = jsonResults[i];
                            var additionalDoc = JsonDocument.Parse(additionalJson);
                            var additionalObject = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(additionalJson);
                            
                            // Mesclar propriedades
                            foreach (var prop in additionalObject)
                            {
                                if (!resultObject.ContainsKey(prop.Key))
                                {
                                    resultObject[prop.Key] = prop.Value;
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Erro ao processar JSON adicional: {ErrorMessage}", ex.Message);
                        }
                    }
                    
                    // Serializar o resultado combinado
                    var combinedJson = JsonSerializer.Serialize(resultObject, new JsonSerializerOptions { WriteIndented = true });
                    return $"```json\n{combinedJson}\n```";
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Erro ao combinar JSONs: {ErrorMessage}", ex.Message);
                    
                    // Em caso de erro, retornar o texto combinado
                    return combinedResult.ToString();
                }
            }
            
            // Se não encontrou JSON, retornar o texto combinado
            return combinedResult.ToString();
        }

        /// <summary>
        /// Remove códigos ANSI de uma string
        /// </summary>
        private string CleanAnsiCodes(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Padrão para códigos ANSI de cores e formatação
            var ansiPattern = new Regex(@"\x1B\[[0-9;]*[a-zA-Z]");
            return ansiPattern.Replace(text, string.Empty);
        }
    }
}
