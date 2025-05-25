using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RefactorScore.Core.Entities;
using RefactorScore.Core.Interfaces;

namespace RefactorScore.Infrastructure.LLM
{
    /// <summary>
    /// Implementação do serviço LLM utilizando Ollama
    /// </summary>
    public class OllamaService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly OllamaOptions _options;
        private readonly ILogger<OllamaService> _logger;
        private readonly PromptTemplates _promptTemplates;
        
        public OllamaService(HttpClient httpClient, IOptions<OllamaOptions> options, 
            ILogger<OllamaService> logger, PromptTemplates promptTemplates)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
            _promptTemplates = promptTemplates;
            
            // Configurar o client HTTP com a URL base do Ollama
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
            
            // Garantir que o timeout do HttpClient está configurado corretamente
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSegundos);
            
            _logger.LogInformation("🤖 Serviço Ollama inicializado com base URL: {BaseUrl}", _options.BaseUrl);
            _logger.LogInformation("⏱️ Timeout configurado: {TimeoutSegundos} segundos", _options.TimeoutSegundos);
            _logger.LogInformation("⏱️ HttpClient Timeout: {HttpClientTimeout} segundos", _httpClient.Timeout.TotalSeconds);
        }
        
        /// <inheritdoc/>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                _logger.LogInformation("🔍 Verificando disponibilidade do serviço Ollama em {BaseUrl}", _options.BaseUrl);
                var modelos = await ObterModelosDisponiveisAsync();
                
                bool disponivel = modelos.Count > 0;
                
                if (disponivel)
                {
                    _logger.LogInformation("✅ Serviço Ollama está disponível. Encontrados {NumModelos} modelos.", modelos.Count);
                }
                else
                {
                    _logger.LogWarning("⚠️ Serviço Ollama não está disponível. Nenhum modelo encontrado.");
                }
                
                return disponivel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao verificar disponibilidade do serviço Ollama");
                return false;
            }
        }
        
        /// <inheritdoc/>
        public async Task<string> ProcessarPromptAsync(
            string prompt, 
            string? modelo = null, 
            float temperatura = 0.1f, 
            int maxTokens = 2048)
        {
            try
            {
                // Usar o modelo configurado se não for especificado
                string modeloAtual = string.IsNullOrEmpty(modelo) 
                    ? _options.ModeloPadrao 
                    : modelo;
                
                _logger.LogInformation("🤖 Processando prompt usando modelo {Modelo} (temperatura: {Temperatura}, maxTokens: {MaxTokens})", 
                    modeloAtual, temperatura, maxTokens);
                
                // Verificar se o modelo está disponível
                _logger.LogInformation("🔍 Verificando disponibilidade do modelo {Modelo}", modeloAtual);
                if (!await VerificarModeloDisponivelAsync(modeloAtual))
                {
                    _logger.LogError("❌ Modelo '{Modelo}' não está disponível", modeloAtual);
                    throw new InvalidOperationException($"Modelo '{modeloAtual}' não está disponível");
                }
                
                // Preparar a requisição para o Ollama
                var request = new OllamaRequest
                {
                    Model = modeloAtual,
                    Prompt = prompt,
                    Temperature = temperatura,
                    MaxTokens = maxTokens,
                    Stream = false
                };
                
                // Fazer a requisição
                _logger.LogInformation("📤 Enviando requisição para Ollama API (tamanho do prompt: {TamanhoPrompt} caracteres)", 
                    prompt.Length);
                
                var startTime = DateTime.UtcNow;
                var response = await _httpClient.PostAsJsonAsync("/api/generate", request);
                
                // Verificar o resultado da requisição
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ Falha na requisição ao Ollama: {StatusCode} - {ReasonPhrase}", 
                        (int)response.StatusCode, response.ReasonPhrase);
                    response.EnsureSuccessStatusCode(); // Isso vai lançar uma exceção com os detalhes
                }
                
                // Processar a resposta
                var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                var responseTime = DateTime.UtcNow - startTime;
                
                _logger.LogInformation("📥 Resposta recebida do Ollama em {ResponseTime:N2}s (tamanho: {TamanhoResposta} caracteres, tokens gerados: {TokensGerados})", 
                    responseTime.TotalSeconds,
                    ollamaResponse?.Response?.Length ?? 0,
                    ollamaResponse?.EvalCount ?? 0);
                
                return ollamaResponse?.Response ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao processar prompt no Ollama");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<CodigoLimpo> AnalisarCodigoAsync(
            string codigo, 
            string linguagem, 
            string? contexto = null)
        {
            try
            {
                _logger.LogInformation("🔍 Iniciando análise de código na linguagem {Linguagem}", linguagem);
                
                // Construir o prompt usando o template de análise de código
                string prompt = _promptTemplates.AnaliseCodigo
                    .Replace("{{CODIGO}}", codigo)
                    .Replace("{{LINGUAGEM}}", linguagem)
                    .Replace("{{CONTEXTO}}", contexto ?? "Nenhum contexto adicional fornecido.");
                
                _logger.LogInformation("🤖 Preparando análise de código: {TamanhoArquivo} caracteres, linguagem: {Linguagem}", 
                    codigo.Length, linguagem);
                
                // Enviar para o LLM
                string resposta = await ProcessarPromptAsync(prompt, _options.ModeloAnalise);
                
                // Processar a resposta para extrair a análise formatada
                _logger.LogInformation("🔄 Processando resposta de análise de código");
                var analise = ProcessarRespostaAnalise(resposta);
                
                _logger.LogInformation("✅ Análise de código concluída. Nota geral: {NotaGeral:F1}", analise.NotaGeral);
                
                return analise;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao analisar código no Ollama");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<List<Recomendacao>> GerarRecomendacoesAsync(CodigoLimpo analise, 
            string codigo, string linguagem)
        {
            try
            {
                _logger.LogInformation("🔍 Iniciando geração de recomendações para código na linguagem {Linguagem}", linguagem);
                
                // Criar um JSON com a análise para incluir no prompt
                string analiseJson = JsonSerializer.Serialize(analise);
                
                // Construir o prompt usando o template de recomendações
                string prompt = _promptTemplates.Recomendacoes
                    .Replace("{{ANALISE}}", analiseJson)
                    .Replace("{{CODIGO}}", codigo)
                    .Replace("{{LINGUAGEM}}", linguagem);
                
                _logger.LogInformation("🤖 Preparando geração de recomendações para arquivo de {TamanhoArquivo} caracteres", 
                    codigo.Length);
                
                // Enviar para o LLM
                string resposta = await ProcessarPromptAsync(prompt, _options.ModeloRecomendacoes);
                
                // Processar a resposta para extrair as recomendações
                _logger.LogInformation("🔄 Processando resposta para extrair recomendações");
                var recomendacoes = ProcessarRespostaRecomendacoes(resposta);
                
                _logger.LogInformation("✅ Geração de recomendações concluída. Total de recomendações: {Total}", 
                    recomendacoes.Count);
                
                return recomendacoes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao gerar recomendações no Ollama");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<List<string>> ObterModelosDisponiveisAsync()
        {
            try
            {
                _logger.LogInformation("🔍 Buscando modelos disponíveis no Ollama");
                
                var response = await _httpClient.GetFromJsonAsync<OllamaModelosResponse>("/api/tags");
                
                if (response?.Models == null)
                {
                    _logger.LogWarning("⚠️ Nenhum modelo encontrado no Ollama");
                    return new List<string>();
                }
                
                var modelos = new List<string>();
                foreach (var modelo in response.Models)
                {
                    modelos.Add(modelo.Name);
                }
                
                _logger.LogInformation("📋 Encontrados {Total} modelos no Ollama: {ModelosList}", 
                    modelos.Count, string.Join(", ", modelos));
                
                return modelos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao obter modelos disponíveis no Ollama");
                return new List<string>();
            }
        }
        
        /// <inheritdoc/>
        public async Task<bool> VerificarModeloDisponivelAsync(string nomeModelo)
        {
            try
            {
                _logger.LogInformation("🔍 Verificando disponibilidade do modelo {NomeModelo}", nomeModelo);
                
                var modelos = await ObterModelosDisponiveisAsync();
                bool disponivel = modelos.Contains(nomeModelo);
                
                if (disponivel)
                {
                    _logger.LogInformation("✅ Modelo {NomeModelo} está disponível", nomeModelo);
                }
                else
                {
                    _logger.LogWarning("⚠️ Modelo {NomeModelo} não está disponível. Modelos disponíveis: {ModelosDisponiveis}", 
                        nomeModelo, string.Join(", ", modelos));
                }
                
                return disponivel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao verificar disponibilidade do modelo {NomeModelo}", nomeModelo);
                return false;
            }
        }
        
        #region Métodos Auxiliares
        
        /// <summary>
        /// Processa a resposta da análise de código e retorna um objeto CodigoLimpo
        /// </summary>
        private CodigoLimpo ProcessarRespostaAnalise(string resposta)
        {
            try
            {
                // Tentar extrair JSON da resposta
                int startIndex = resposta.IndexOf('{');
                int endIndex = resposta.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    string jsonContent = resposta.Substring(startIndex, endIndex - startIndex + 1);
                    
                    _logger.LogInformation("🔄 Extraindo análise do JSON (tamanho: {TamanhoJson} caracteres)", 
                        jsonContent.Length);
                    
                    // Deserializar o JSON para um objeto CodigoLimpo
                    return JsonSerializer.Deserialize<CodigoLimpo>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                
                _logger.LogWarning("⚠️ Não foi possível extrair JSON da resposta de análise. Usando valores padrão.");
                
                // Se não conseguir extrair o JSON, criar um objeto com valores padrão
                return new CodigoLimpo
                {
                    NomenclaturaVariaveis = 5,
                    TamanhoFuncoes = 5,
                    UsoComentariosRelevantes = 5,
                    CoesaoMetodos = 5,
                    EvitacaoCodigoMorto = 5,
                    Justificativas = new Dictionary<string, string>
                    {
                        { "NomenclaturaVariaveis", "Não foi possível analisar" },
                        { "TamanhoFuncoes", "Não foi possível analisar" },
                        { "UsoComentariosRelevantes", "Não foi possível analisar" },
                        { "CoesaoMetodos", "Não foi possível analisar" },
                        { "EvitacaoCodigoMorto", "Não foi possível analisar" }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao processar resposta de análise");
                
                // Retornar um objeto padrão em caso de erro
                return new CodigoLimpo
                {
                    NomenclaturaVariaveis = 5,
                    TamanhoFuncoes = 5,
                    UsoComentariosRelevantes = 5,
                    CoesaoMetodos = 5,
                    EvitacaoCodigoMorto = 5,
                    Justificativas = new Dictionary<string, string>
                    {
                        { "Error", ex.Message }
                    }
                };
            }
        }
        
        /// <summary>
        /// Processa a resposta de recomendações e retorna uma lista de objetos Recomendacao
        /// </summary>
        private List<Recomendacao> ProcessarRespostaRecomendacoes(string resposta)
        {
            try
            {
                // Tentar extrair JSON da resposta
                int startIndex = resposta.IndexOf('[');
                int endIndex = resposta.LastIndexOf(']');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    string jsonContent = resposta.Substring(startIndex, endIndex - startIndex + 1);
                    
                    // Deserializar o JSON para uma lista de Recomendacao
                    return JsonSerializer.Deserialize<List<Recomendacao>>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<Recomendacao>();
                }
                
                // Se não conseguir extrair o JSON, retornar uma lista vazia
                return new List<Recomendacao>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao processar resposta de recomendações");
                
                // Retornar uma lista com uma recomendação de erro
                return new List<Recomendacao>
                {
                    new Recomendacao
                    {
                        Titulo = "Erro ao processar recomendações",
                        Descricao = $"Ocorreu um erro ao processar as recomendações: {ex.Message}",
                        Prioridade = "Alta",
                        Tipo = "Erro",
                        Dificuldade = "N/A",
                        ReferenciaArquivo = "N/A",
                        RecursosEstudo = new List<string> { "N/A" }
                    }
                };
            }
        }
        
        #endregion
    }
    
    #region Classes de Apoio
    
    /// <summary>
    /// Opções de configuração para o serviço Ollama
    /// </summary>
    public class OllamaOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string ModeloPadrao { get; set; } = "deepseek-coder:6.7b-instruct-q4_0";
        public string ModeloAnalise { get; set; } = "deepseek-coder:6.7b-instruct-q4_0";
        public string ModeloRecomendacoes { get; set; } = "deepseek-coder:6.7b-instruct-q4_0";
        public int TimeoutSegundos { get; set; } = 600;
    }
    
    /// <summary>
    /// Templates de prompts para diferentes tarefas
    /// </summary>
    public class PromptTemplates
    {
        /// <summary>
        /// Template para análise de código
        /// </summary>
        public string AnaliseCodigo { get; set; } = @"
Você é um especialista em análise de código e boas práticas de programação. Analise o código abaixo e avalie-o segundo os princípios de código limpo e boas práticas.

Linguagem: {{LINGUAGEM}}

Contexto adicional: {{CONTEXTO}}

Código a ser analisado:
```
{{CODIGO}}
```

Avalie cada um dos seguintes critérios de 0 a 10 (onde 0 é péssimo e 10 é excelente):
1. Nomenclatura de variáveis, funções e classes
2. Tamanho e responsabilidade única de funções
3. Uso adequado de comentários e documentação
4. Coesão de métodos e classes
5. Ausência de código morto ou redundante

Para cada critério, forneça uma breve justificativa.

IMPORTANTE: Responda APENAS em formato JSON, sem texto adicional antes ou depois. Forneça apenas o objeto JSON puro, sem formatação markdown ou explicações.

Formato esperado:
{
  ""nomenclaturaVariaveis"": numero,
  ""tamanhoFuncoes"": numero,
  ""usoComentariosRelevantes"": numero,
  ""coesaoMetodos"": numero,
  ""evitacaoCodigoMorto"": numero,
  ""justificativas"": {
    ""nomenclaturaVariaveis"": ""Descrição do critério de nomenclatura"",
    ""tamanhoFuncoes"": ""Descrição do critério de tamanho"",
    ""usoComentariosRelevantes"": ""Descrição do critério de comentários"",
    ""coesaoMetodos"": ""Descrição do critério de coesão"",
    ""evitacaoCodigoMorto"": ""Descrição do critério de código morto""
  }
}";
        
        /// <summary>
        /// Template para geração de recomendações
        /// </summary>
        public string Recomendacoes { get; set; } = @"
Você é um tutor de programação experiente. Com base na análise de código abaixo, gere recomendações educativas para ajudar o desenvolvedor a melhorar seu código e aprender melhores práticas.

Linguagem: {{LINGUAGEM}}

Análise do código:
{{ANALISE}}

Código analisado:
```
{{CODIGO}}
```

Forneça 3-5 recomendações específicas, priorizando os aspectos que mais precisam de melhoria conforme indicado na análise. Cada recomendação deve:
1. Focar em um problema ou oportunidade de melhoria específica
2. Explicar o impacto positivo da mudança
3. Incluir um exemplo concreto de como implementar a melhoria
4. Fornecer links ou recursos para aprendizado adicional

IMPORTANTE: Responda APENAS em formato JSON, sem texto adicional antes ou depois. Forneça apenas o array JSON puro, sem formatação markdown ou explicações.

Formato esperado:
[
  {
    ""titulo"": ""Melhore a nomenclatura de variáveis"",
    ""descricao"": ""Variáveis como 'x' e 'temp' não comunicam seu propósito. Nomes descritivos melhoram a legibilidade e manutenção do código."",
    ""exemplo"": ""Em vez de 'int x = calcularTotal();', use 'int totalProdutos = calcularTotalProdutos();'"",
    ""prioridade"": ""Alta"",
    ""tipo"": ""Nomenclatura"",
    ""dificuldade"": ""Fácil"",
    ""referenciaArquivo"": ""linha 25-30"",
    ""recursosEstudo"": [""https://cleancoders.com/resources/naming-variables"", ""https://refactoring.guru/renaming""]
  }
]";
    }
    
    /// <summary>
    /// Modelo de requisição para o Ollama API
    /// </summary>
    public class OllamaRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }
        
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; }
        
        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }
        
        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
        
        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }
    
    /// <summary>
    /// Modelo de resposta do Ollama API
    /// </summary>
    public class OllamaResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }
        
        [JsonPropertyName("response")]
        public string Response { get; set; }
        
        [JsonPropertyName("total_duration")]
        public long TotalDuration { get; set; }
        
        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; }
        
        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }
    }
    
    /// <summary>
    /// Modelo para a resposta da listagem de modelos disponíveis
    /// </summary>
    public class OllamaModelosResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo> Models { get; set; }
    }
    
    /// <summary>
    /// Informações sobre um modelo no Ollama
    /// </summary>
    public class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("modified_at")]
        public string ModifiedAt { get; set; }
        
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
    
    #endregion
} 