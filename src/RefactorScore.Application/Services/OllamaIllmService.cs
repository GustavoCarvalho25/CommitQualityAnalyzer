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
            return await ParseAnalysisResponse(response);
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
            return await ParseSuggestionsResponse(response);
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

            Responda em JSON no formato (não altere o formato, precisa ser um json com exatamente estas chaves):
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

    private async Task<LLMAnalysisResult> ParseAnalysisResponse(string response)
    {
        var jsonContent = ExtractJsonFromResponse(response);
    
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (TryParseAnalysisJson(jsonContent, out var result))
            {
                return result;
            }
        
            jsonContent = await FixJsonWithLlmAsync(jsonContent);
            if (jsonContent == null) break;
        }
    
        return GetDefaultAnalysisResult();
    }

    private string ExtractJsonFromResponse(string response)
    {
        try
        {
            _logger.LogDebug("Extracting JSON from LLM response: {Response}", response);
            
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart == -1 || jsonEnd == -1)
            {
                _logger.LogWarning("No JSON found in LLM response");
                return "{}";
            }

            var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            _logger.LogDebug("Extracted JSON: {Json}", jsonContent);
            
            return jsonContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting JSON from response: {Response}", response);
            return "{}";
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
    
    private async Task<List<LLMSuggestion>> ParseSuggestionsResponse(string response)
    {
        var jsonContent = ExtractSuggestionsJsonFromResponse(response);
    
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (TryParseSuggestionsJson(jsonContent, out var result))
            {
                return result;
            }
        
            jsonContent = await FixSuggestionsJsonWithLlmAsync(jsonContent);
            if (jsonContent == null) break;
        }
    
        return GetDefaultSuggestions();
    }
    
    private string ExtractSuggestionsJsonFromResponse(string response)
    {
        try
        {
            _logger.LogDebug("Extracting suggestions JSON from LLM response: {Response}", response);
            
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');
            
            if (jsonStart != -1 && jsonEnd != -1)
            {
                var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                _logger.LogDebug("Extracted suggestions JSON array: {Json}", jsonContent);
                return jsonContent;
            }
            
            jsonStart = response.IndexOf('{');
            jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart != -1 && jsonEnd != -1)
            {
                var singleObject = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var wrappedJson = $"[{singleObject}]";
                _logger.LogDebug("Extracted single suggestion object, wrapped in array: {Json}", wrappedJson);
                return wrappedJson;
            }
            
            _logger.LogWarning("No JSON found in suggestions response");
            return "[]";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting suggestions JSON from response: {Response}", response);
            return "[]";
        }
    }

    private bool TryParseSuggestionsJson(string jsonContent, out List<LLMSuggestion> suggestions)
    {
        suggestions = null;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };
            
            suggestions = JsonSerializer.Deserialize<List<LLMSuggestion>>(jsonContent, options);
            
            if (suggestions == null || !suggestions.Any())
            {
                _logger.LogWarning("Deserialized suggestions list is null or empty");
                return false;
            }
            
            var validSuggestions = suggestions
                .Where(s => !string.IsNullOrWhiteSpace(s.Title) && !string.IsNullOrWhiteSpace(s.Description))
                .Take(5)
                .ToList();
            
            suggestions = validSuggestions;
            _logger.LogInformation("Successfully parsed {Count} suggestions", validSuggestions.Count);
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Suggestions JSON parsing failed: {Error}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during suggestions JSON parsing: {Error}", ex.Message);
            return false;
        }
    }

    private async Task<string> FixSuggestionsJsonWithLlmAsync(string brokenJson, int maxRetries = 5)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Attempting suggestions JSON correction with LLM (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);

                var fixPrompt = BuildSuggestionsJsonFixPrompt(brokenJson);
                var correctedResponse = await CallOllamaAsync(fixPrompt);
                
                var jsonStart = correctedResponse.IndexOf('[');
                var jsonEnd = correctedResponse.LastIndexOf(']');
                
                if (jsonStart == -1 || jsonEnd == -1)
                {
                    _logger.LogWarning("LLM suggestions correction attempt {Attempt} did not return valid JSON array structure", attempt);
                    continue;
                }
                
                var correctedJson = correctedResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                _logger.LogDebug("LLM corrected suggestions JSON (attempt {Attempt}): {Json}", attempt, correctedJson);
                
                try
                {
                    JsonDocument.Parse(correctedJson);
                    _logger.LogInformation("LLM successfully corrected suggestions JSON on attempt {Attempt}", attempt);
                    return correctedJson;
                }
                catch (JsonException)
                {
                    _logger.LogWarning("LLM suggestions correction attempt {Attempt} still produced invalid JSON", attempt);
                    brokenJson = correctedJson;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during LLM suggestions JSON correction attempt {Attempt}", attempt);
            }
        }
        
        _logger.LogError("Failed to correct suggestions JSON after {MaxRetries} attempts with LLM", maxRetries);
        return null;
    }

    private string BuildSuggestionsJsonFixPrompt(string brokenJson)
    {
        return $@"
            O JSON array abaixo está malformado e precisa ser corrigido. Por favor, corrija-o mantendo exatamente os mesmos dados, apenas corrigindo problemas de sintaxe como vírgulas extras, estrutura incorreta, etc.

            JSON com problemas:
            {brokenJson}

            Retorne APENAS o JSON array corrigido, sem explicações adicionais. O JSON deve ser um array válido com esta estrutura:
            [
              {{
                ""title"": ""texto"",
                ""description"": ""texto"",
                ""priority"": ""Medium"",
                ""type"": ""CodeStyle"",
                ""difficulty"": ""Easy"",
                ""studyResources"": [""recurso1"", ""recurso2""]
              }}
            ]";
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
    private bool TryParseAnalysisJson(string jsonContent, out LLMAnalysisResult result)
    {
        result = null;
        try
        {
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            JsonElement scoreElement;
            if (root.TryGetProperty("score", out var scoreProperty))
            {
                scoreElement = scoreProperty;
                _logger.LogDebug("Found nested 'score' object");
            }
            else
            {
                scoreElement = root;
                _logger.LogDebug("Using root object directly");
            }

            result = new LLMAnalysisResult
            {
                VariableScore = scoreElement.GetProperty("variableScore").GetInt32(),
                FunctionScore = scoreElement.GetProperty("functionScore").GetInt32(),
                CommentScore = scoreElement.GetProperty("commentScore").GetInt32(),
                CohesionScore = scoreElement.GetProperty("cohesionScore").GetInt32(),
                DeadCodeScore = scoreElement.GetProperty("deadCodeScore").GetInt32()
            };

            if (scoreElement.TryGetProperty("justifications", out var justifications))
            {
                foreach (var prop in justifications.EnumerateObject())
                {
                    result.Justifications[prop.Name] = prop.Value.GetString() ?? "";
                }
            }

            _logger.LogInformation("Successfully parsed analysis: Variable={Variable}, Function={Function}, Comment={Comment}", 
                result.VariableScore, result.FunctionScore, result.CommentScore);

            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "JSON parsing failed: {Error}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during JSON parsing: {Error}", ex.Message);
            return false;
        }
    }

    private async Task<string> FixJsonWithLlmAsync(string brokenJson, int maxRetries = 5)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Attempting JSON correction with LLM (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);

                var fixPrompt = BuildJsonFixPrompt(brokenJson);
                var correctedResponse = await CallOllamaAsync(fixPrompt);
                
                var jsonStart = correctedResponse.IndexOf('{');
                var jsonEnd = correctedResponse.LastIndexOf('}');
                
                if (jsonStart == -1 || jsonEnd == -1)
                {
                    _logger.LogWarning("LLM correction attempt {Attempt} did not return valid JSON structure", attempt);
                    continue;
                }
                
                var correctedJson = correctedResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                _logger.LogDebug("LLM corrected JSON (attempt {Attempt}): {Json}", attempt, correctedJson);
                
                try
                {
                    JsonDocument.Parse(correctedJson);
                    _logger.LogInformation("LLM successfully corrected JSON on attempt {Attempt}", attempt);
                    return correctedJson;
                }
                catch (JsonException)
                {
                    _logger.LogWarning("LLM correction attempt {Attempt} still produced invalid JSON", attempt);
                    brokenJson = correctedJson;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during LLM JSON correction attempt {Attempt}", attempt);
            }
        }
        
        _logger.LogError("Failed to correct JSON after {MaxRetries} attempts with LLM", maxRetries);
        return null;
    }

    private string BuildJsonFixPrompt(string brokenJson)
    {
        return $@"
            O JSON abaixo está malformado e precisa ser corrigido. Por favor, corrija-o mantendo exatamente a mesma estrutura e dados, apenas corrigindo problemas de sintaxe como vírgulas extras, quebras de linha incorretas, etc.

            JSON com problemas:
            {brokenJson}

            Retorne APENAS o JSON corrigido, sem explicações adicionais. O JSON deve ter exatamente esta estrutura:
            {{
              ""variableScore"": número,
              ""functionScore"": número,
              ""commentScore"": número,
              ""cohesionScore"": número,
              ""deadCodeScore"": número,
              ""justifications"": {{
                ""VariableNaming"": ""texto"",
                ""FunctionSizes"": ""texto""
              }}
            }}";
    }
}