using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RefactorScore.Core.Interfaces;
using RefactorScore.Core.Specifications;

namespace RefactorScore.Infrastructure.Ollama
{
    public class OllamaService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaService> _logger;
        private readonly OllamaOptions _options;
        private readonly JsonSerializerOptions _jsonOptions;

        public OllamaService(
            HttpClient httpClient,
            IOptions<OllamaOptions> options,
            ILogger<OllamaService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
            
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
            
            // Set timeout if configured
            if (_options.Timeout > 0)
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(_options.Timeout);
                _logger.LogInformation("[LLM_CONFIG] Setting HTTP client timeout to {Timeout} seconds", _options.Timeout);
            }
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <inheritdoc />
        public async Task<string> ProcessPromptAsync(string prompt, string? model = null, float temperature = 0.1f, int maxTokens = 2048)
        {
            try
            {
                var modelName = model ?? _options.DefaultModel;
                var promptLength = prompt.Length;
                
                _logger.LogInformation("[LLM_BEGIN] Processing prompt with model {Model}. Prompt length: {PromptLength} chars", 
                    modelName, promptLength);
                
                var request = new OllamaRequest
                {
                    Model = modelName,
                    Prompt = prompt,
                    Stream = false,
                    Options = new OllamaRequestOptions
                    {
                        Temperature = temperature,
                        NumPredict = maxTokens,
                        TopP = _options.TopP,
                        TopK = _options.TopK
                    }
                };
                
                var content = JsonSerializer.Serialize(request, _jsonOptions);
                var httpContent = new StringContent(content, Encoding.UTF8, "application/json");
                
                _logger.LogInformation("[LLM_REQUEST] Sending request to Ollama API with timeout of {Timeout} seconds...", 
                    _httpClient.Timeout.TotalSeconds);
                var startTime = DateTime.UtcNow;
                
                var response = await _httpClient.PostAsync("api/generate", httpContent);
                
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("[LLM_RESPONSE] Received response from Ollama API after {Duration}ms. Status: {Status}", 
                    duration.TotalMilliseconds, response.StatusCode);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("[LLM_ERROR] Error processing prompt: {ErrorContent}", errorContent);
                    throw new Exception($"Error processing prompt: {response.StatusCode} - {errorContent}");
                }
                
                _logger.LogInformation("[LLM_PARSING] Reading response content...");
                var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaResponse>(_jsonOptions);
                
                if (ollamaResponse == null)
                {
                    _logger.LogError("[LLM_ERROR] Null response from Ollama service");
                    throw new Exception("Null response from Ollama service");
                }
                
                var responseLength = ollamaResponse.Response?.Length ?? 0;
                _logger.LogInformation("[LLM_SUCCESS] Successfully processed prompt. Response length: {ResponseLength} chars", 
                    responseLength);
                
                return ollamaResponse.Response;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "[LLM_TIMEOUT] Request timed out after {Timeout} seconds", _httpClient.Timeout.TotalSeconds);
                throw new Exception($"LLM request timed out after {_httpClient.Timeout.TotalSeconds} seconds. This usually indicates the model is taking too long to generate a response.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LLM_EXCEPTION] Error processing prompt");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                _logger.LogInformation("[LLM_CHECK] Checking Ollama service availability...");
                var response = await _httpClient.GetAsync("api/tags");
                var isAvailable = response.IsSuccessStatusCode;
                
                _logger.LogInformation("[LLM_CHECK] Ollama service availability: {IsAvailable}", isAvailable);
                return isAvailable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LLM_CHECK] Error checking Ollama service availability");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsModelAvailableAsync(string modelName)
        {
            try
            {
                var models = await GetAvailableModelsAsync();
                return models.Contains(modelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar disponibilidade do modelo {ModelName}", modelName);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<string[]> GetAvailableModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/tags");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Erro ao obter modelos disponíveis: {StatusCode}", response.StatusCode);
                    return Array.Empty<string>();
                }
                
                var tagsResponse = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(_jsonOptions);
                
                if (tagsResponse == null || tagsResponse.Models == null)
                {
                    _logger.LogError("Resposta de tags nula do serviço Ollama");
                    return Array.Empty<string>();
                }
                
                return tagsResponse.Models
                    .Select(m => m.Name)
                    .ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter modelos disponíveis");
                return Array.Empty<string>();
            }
        }
    }

    /// <summary>
    /// Opções de configuração para o serviço Ollama
    /// </summary>
    public class OllamaOptions
    {
        /// <summary>
        /// URL base da API Ollama
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:11434/";
        
        /// <summary>
        /// Modelo padrão a ser usado
        /// </summary>
        public string DefaultModel { get; set; } = "refactorscore";
        
        /// <summary>
        /// Temperatura de geração (controla aleatoriedade)
        /// </summary>
        public float Temperature { get; set; } = 0.1f;
        
        /// <summary>
        /// Número máximo de tokens na resposta
        /// </summary>
        public int MaxTokens { get; set; } = 2048;
        
        /// <summary>
        /// Configuração de top-p para sampling
        /// </summary>
        public float TopP { get; set; } = 0.9f;
        
        /// <summary>
        /// Configuração de top-k para sampling
        /// </summary>
        public int TopK { get; set; } = 40;
        
        /// <summary>
        /// Timeout em segundos para requisições HTTP
        /// </summary>
        public int Timeout { get; set; } = 600;
    }

    #region DTOs

    /// <summary>
    /// Requisição para a API Ollama
    /// </summary>
    internal class OllamaRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }
        
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; }
        
        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
        
        [JsonPropertyName("options")]
        public OllamaRequestOptions Options { get; set; }
    }
    
    /// <summary>
    /// Opções da requisição para a API Ollama
    /// </summary>
    internal class OllamaRequestOptions
    {
        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }
        
        [JsonPropertyName("num_predict")]
        public int? NumPredict { get; set; }
        
        [JsonPropertyName("top_p")]
        public float TopP { get; set; }
        
        [JsonPropertyName("top_k")]
        public int TopK { get; set; }
    }
    
    /// <summary>
    /// Resposta da API Ollama
    /// </summary>
    internal class OllamaResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }
        
        [JsonPropertyName("response")]
        public string Response { get; set; }
        
        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }
    
    /// <summary>
    /// Resposta de tags (modelos) da API Ollama
    /// </summary>
    internal class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo> Models { get; set; }
    }
    
    /// <summary>
    /// Informações de um modelo na API Ollama
    /// </summary>
    internal class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("size")]
        public long Size { get; set; }
        
        [JsonPropertyName("modified_at")]
        public DateTime ModifiedAt { get; set; }
    }

    #endregion
} 