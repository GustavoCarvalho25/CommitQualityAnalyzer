using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RefactorScore.Domain.Models;
using RefactorScore.Domain.Services;
using RefactorScore.Domain.ValueObjects;

namespace RefactorScore.Application.Services;

public class OllamaIllmService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaIllmService> _logger;
    private readonly string _ollamaUrl;
    private readonly IConfiguration _configuration;

    public OllamaIllmService(ILogger<OllamaIllmService> logger, HttpClient httpClient, string ollamaUrl, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _ollamaUrl = ollamaUrl;
        _configuration = configuration;
    }

    public async Task<LLMAnalysisResult> AnalyzeFileAsync(string fileContent)
    {
        try
        {
            var prompt = BuildAnalysisPrompt(fileContent);
            var response = await CallOllamaAsync(prompt);
            return ParseAnalysisResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing file with LLM");
            throw;
        }
    }

    public async Task<List<LLMSuggestion>> GenerateSuggestionsAsync(string fileContent, CleanCodeRating rating)
    {
        try
        {
            var prompt = BuildSuggestionsPrompt(fileContent, rating);
            var response = await CallOllamaAsync(prompt);
            return ParseSuggestionsResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating suggestions with LLM");
            return new List<LLMSuggestion>();
        }
    }

    private string BuildSuggestionsPrompt(string fileContent, CleanCodeRating rating)
    {
        return $@"
                Com base na análise de Clean Code abaixo, gere sugestões específicas para melhorar o código:

                Notas atuais:
                - Variable Naming: {rating.VariableNaming}/10
                - Function Sizes: {rating.FunctionSizes}/10
                - No Needs Comments: {rating.NoNeedsComments}/10
                - Method Cohesion: {rating.MethodCohesion}/10
                - Dead Code: {rating.DeadCode}/10

                Código:
                {fileContent}

                Gere 3-5 sugestões específicas em JSON:
                [
                  {{
                    ""title"": ""Melhorar nomenclatura de variáveis"",
                    ""description"": ""Usar nomes mais descritivos para as variáveis x e y"",
                    ""priority"": ""Medium"",
                    ""type"": ""CodeStyle"",
                    ""difficulty"": ""Easy"",
                    ""studyResources"": [""Clean Code - Chapter 2""]
                  }}
                ]";
    }


    private string BuildAnalysisPrompt(string fileContent)
    {
        return $@"
            Analise o seguinte código e avalie de 1 a 10 os seguintes critérios de Clean Code:

            1. Variable Naming (nomenclatura de variáveis)
            2. Function Sizes (tamanho das funções)
            3. No Needs Comments (código auto-explicativo)
            4. Method Cohesion (coesão dos métodos)
            5. Dead Code (código morto)

            Código:
            {fileContent}

            Responda em JSON no formato:
            {{
              ""variableScore"": 8,
              ""functionScore"": 7,
              ""commentScore"": 9,
              ""cohesionScore"": 8,
              ""deadCodeScore"": 10,
              ""justifications"": {{
                ""VariableNaming"": ""Nomes descritivos e claros"",
                ""FunctionSizes"": ""Funções pequenas e focadas""
              }}
            }}";
    }

    private async Task<string> CallOllamaAsync(string prompt)
    {
        var model = _configuration["Ollama:Model"];
        var request = new
        {
            model,
            prompt,
            stream = false
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_ollamaUrl}/api/generate", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        if (!root.TryGetProperty("response", out var responseProperty))
        {
            _logger.LogWarning("No 'response' property found in LLM response, using default values");
            throw new InvalidOperationException("No 'response' property found in LLM response");
        }
        
        var llmResponse = responseProperty.GetString();
        
        if (string.IsNullOrEmpty(llmResponse))
        {
            _logger.LogWarning("Empty LLM response, using default values");
            throw new InvalidOperationException("Empty LLM response");;
        }
        
        return llmResponse;
    }

    private LLMAnalysisResult ParseAnalysisResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart == -1 || jsonEnd == -1)
            {
                _logger.LogWarning("No JSON found in LLM response, using default values");
                return GetDefaultAnalysisResult();
            }

            var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart + 1);

            var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            var result = new LLMAnalysisResult
            {
                VariableScore = root.GetProperty("variableScore").GetInt32(),
                FunctionScore = root.GetProperty("functionScore").GetInt32(),
                CommentScore = root.GetProperty("commentScore").GetInt32(),
                CohesionScore = root.GetProperty("cohesionScore").GetInt32(),
                DeadCodeScore = root.GetProperty("deadCodeScore").GetInt32()
            };

            if (root.TryGetProperty("justifications", out var justifications))
            {
                foreach (var prop in justifications.EnumerateObject())
                {
                    result.Justifications[prop.Name] = prop.Value.GetString() ?? "";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing LLM response: {Response}", response);
            return GetDefaultAnalysisResult();
        }
    }

    private LLMAnalysisResult GetDefaultAnalysisResult()
    {
        return new LLMAnalysisResult
        {
            VariableScore = 5,
            FunctionScore = 5,
            CommentScore = 5,
            CohesionScore = 5,
            DeadCodeScore = 5,
            Justifications = new Dictionary<string, string>
            {
                ["VariableNaming"] = "Análise não disponível",
                ["FunctionSizes"] = "Análise não disponível",
                ["NoNeedsComments"] = "Análise não disponível",
                ["MethodCohesion"] = "Análise não disponível",
                ["DeadCode"] = "Análise não disponível"
            }
        };
    }
    
    private List<LLMSuggestion> ParseSuggestionsResponse(string response)
{
    try
    {
        _logger.LogInformation("Parsing suggestions response from LLM");
        
        var jsonStart = response.IndexOf('[');
        var jsonEnd = response.LastIndexOf(']');
        
        if (jsonStart == -1 || jsonEnd == -1)
        {
            _logger.LogWarning("No JSON array found in suggestions response, trying to find single object");
            
            jsonStart = response.IndexOf('{');
            jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart == -1 || jsonEnd == -1)
            {
                _logger.LogWarning("No JSON found in suggestions response");
                return GetDefaultSuggestions();
            }
            
            var singleObjectJson = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var singleSuggestion = JsonSerializer.Deserialize<LLMSuggestion>(singleObjectJson);
            return singleSuggestion != null ? new List<LLMSuggestion> { singleSuggestion } : GetDefaultSuggestions();
        }
        
        var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
        _logger.LogDebug("Extracted JSON content: {JsonContent}", jsonContent);
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };
        
        var suggestions = JsonSerializer.Deserialize<List<LLMSuggestion>>(jsonContent, options);
        
        if (suggestions == null || !suggestions.Any())
        {
            _logger.LogWarning("Deserialized suggestions list is null or empty");
            return GetDefaultSuggestions();
        }
        
        var validSuggestions = suggestions
            .Where(s => !string.IsNullOrWhiteSpace(s.Title) && !string.IsNullOrWhiteSpace(s.Description))
            .Take(5)
            .ToList();
        
        _logger.LogInformation("Successfully parsed {Count} suggestions", validSuggestions.Count);
        return validSuggestions;
    }
    catch (JsonException jsonEx)
    {
        _logger.LogError(jsonEx, "JSON parsing error in suggestions response: {Response}", response);
        return GetDefaultSuggestions();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error parsing suggestions response: {Response}", response);
        return GetDefaultSuggestions();
    }
}

private List<LLMSuggestion> GetDefaultSuggestions()
{
    return new List<LLMSuggestion>
    {
        new LLMSuggestion
        {
            Title = "Revisar nomenclatura de variáveis",
            Description = "Verificar se os nomes das variáveis são descritivos e seguem convenções",
            Priority = "Medium",
            Type = "CodeStyle",
            Difficulty = "Easy",
            StudyResources = new List<string> { "Clean Code - Chapter 2: Meaningful Names" }
        },
        new LLMSuggestion
        {
            Title = "Analisar tamanho das funções",
            Description = "Verificar se as funções estão pequenas e focadas em uma única responsabilidade",
            Priority = "Medium",
            Type = "Structure",
            Difficulty = "Medium",
            StudyResources = new List<string> { "Clean Code - Chapter 3: Functions" }
        },
        new LLMSuggestion
        {
            Title = "Verificar necessidade de comentários",
            Description = "Avaliar se o código é auto-explicativo ou se precisa de comentários",
            Priority = "Low",
            Type = "Documentation",
            Difficulty = "Easy",
            StudyResources = new List<string> { "Clean Code - Chapter 4: Comments" }
        }
    };
}
}