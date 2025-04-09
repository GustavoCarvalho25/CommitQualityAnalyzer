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
            _maxPromptLength = _configuration.GetValue<int>("Ollama:MaxPromptLength", 16000);
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

                var requestData = new
                {
                    model = modelName,
                    prompt = promptPart,
                    stream = false,
                    options = new
                    {
                        temperature = 0.1,
                        top_p = 0.9,
                        top_k = 40
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(apiUrl, requestData, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Erro ao chamar API Ollama: {StatusCode} - {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    return string.Empty;
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
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
                if (prompt.Length <= _maxPromptLength)
                {
                    // Processar o prompt diretamente
                    return await ProcessPromptPart(prompt, modelName, cancellationToken);
                }
                else
                {
                    // Dividir o prompt em partes
                    var promptParts = SplitPromptIntoParts(prompt);
                    _logger.LogInformation("Prompt dividido em {PartCount} partes", promptParts.Count);
                    
                    var responseBuilder = new StringBuilder();
                    
                    // Processar cada parte sequencialmente
                    for (int i = 0; i < promptParts.Count; i++)
                    {
                        _logger.LogInformation("Processando parte {CurrentPart}/{TotalParts}", i + 1, promptParts.Count);
                        var partResponse = await ProcessPromptPart(promptParts[i], modelName, cancellationToken);
                        responseBuilder.AppendLine(partResponse);
                    }
                    
                    return responseBuilder.ToString();
                }
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

                var requestData = new
                {
                    model = modelName,
                    prompt = "Teste de conexão. Responda apenas com 'OK'.",
                    stream = false
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
        public List<string> SplitPromptIntoParts(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
                return new List<string>();

            if (prompt.Length <= _maxPromptLength)
                return new List<string> { prompt };

            var parts = new List<string>();
            int maxPartLength = Math.Min(_maxPromptLength, 2000); // Garantir que não exceda 2000 caracteres
            
            // Dividir o prompt em partes lógicas (parágrafos, frases, etc.)
            int startIndex = 0;
            
            while (startIndex < prompt.Length)
            {
                // Determinar o tamanho máximo para esta parte
                int remainingLength = prompt.Length - startIndex;
                int partLength = Math.Min(remainingLength, maxPartLength);
                
                // Se não estamos no final do prompt, tentar encontrar um ponto de quebra natural
                if (partLength < remainingLength)
                {
                    // Procurar por quebras de parágrafo
                    int paragraphBreak = prompt.LastIndexOf("\n\n", startIndex + partLength - 1, Math.Min(partLength, prompt.Length - startIndex));
                    
                    if (paragraphBreak > startIndex && paragraphBreak - startIndex >= maxPartLength / 2)
                    {
                        partLength = paragraphBreak - startIndex + 2; // +2 para incluir a quebra de parágrafo
                    }
                    else
                    {
                        // Procurar por quebras de linha
                        int lineBreak = prompt.LastIndexOf('\n', startIndex + partLength - 1, Math.Min(partLength, prompt.Length - startIndex));
                        
                        if (lineBreak > startIndex && lineBreak - startIndex >= maxPartLength / 2)
                        {
                            partLength = lineBreak - startIndex + 1; // +1 para incluir a quebra de linha
                        }
                        else
                        {
                            // Procurar por pontuação (ponto final, interrogação, exclamação)
                            int punctuation = Math.Max(
                                prompt.LastIndexOf(". ", startIndex + partLength - 1, Math.Min(partLength, prompt.Length - startIndex)),
                                Math.Max(
                                    prompt.LastIndexOf("? ", startIndex + partLength - 1, Math.Min(partLength, prompt.Length - startIndex)),
                                    prompt.LastIndexOf("! ", startIndex + partLength - 1, Math.Min(partLength, prompt.Length - startIndex))
                                )
                            );
                            
                            if (punctuation > startIndex && punctuation - startIndex >= maxPartLength / 2)
                            {
                                partLength = punctuation - startIndex + 2; // +2 para incluir a pontuação e o espaço
                            }
                            else
                            {
                                // Procurar por espaços
                                int space = prompt.LastIndexOf(' ', startIndex + partLength - 1, Math.Min(partLength, prompt.Length - startIndex));
                                
                                if (space > startIndex && space - startIndex >= maxPartLength / 2)
                                {
                                    partLength = space - startIndex + 1; // +1 para incluir o espaço
                                }
                                // Se não encontrar nenhum ponto de quebra natural, usar o tamanho máximo
                            }
                        }
                    }
                }
                
                // Extrair a parte atual
                string part = prompt.Substring(startIndex, partLength);
                
                // Adicionar cabeçalho informativo se não for a primeira parte
                if (startIndex > 0)
                {
                    part = $"[Continuação da parte {parts.Count}]\n\n" + part;
                }
                
                // Adicionar rodapé informativo se não for a última parte
                if (startIndex + partLength < prompt.Length)
                {
                    part += $"\n\n[Continua na parte {parts.Count + 2}]";
                }
                
                parts.Add(part);
                startIndex += partLength;
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
