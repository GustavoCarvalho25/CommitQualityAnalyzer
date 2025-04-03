using CommitQualityAnalyzer.Core.Models;
using CommitQualityAnalyzer.Core.Repositories;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CommitQualityAnalyzer.Worker.Services
{
    public class CommitAnalyzerService
    {
        private readonly string _repoPath;
        private readonly ILogger<CommitAnalyzerService> _logger;
        private readonly ICodeAnalysisRepository _repository;
        private readonly IConfiguration _configuration;
        private readonly int _maxPromptLength;

        public CommitAnalyzerService(
            string repoPath, 
            ILogger<CommitAnalyzerService> logger,
            ICodeAnalysisRepository repository,
            IConfiguration configuration)
        {
            _repoPath = repoPath;
            _logger = logger;
            _repository = repository;
            _configuration = configuration;
            _maxPromptLength = _configuration.GetValue<int>("Ollama:MaxPromptLength", 16000);
        }

        public async Task AnalyzeLastDayCommits()
        {
            _logger.LogInformation("Iniciando análise do repositório: {RepoPath}", _repoPath);
            
            using var repo = new Repository(_repoPath);
            var yesterday = DateTime.Now.AddDays(-1);

            var commits = repo.Commits
                .Where(c => c.Author.When >= yesterday)
                .ToList();

            _logger.LogInformation("Encontrados {CommitCount} commits nas últimas 24 horas", commits.Count);

            // Processar cada commit sequencialmente
            foreach (var commit in commits)
            {
                using (LogContext.PushProperty("CommitId", commit.Sha))
                using (LogContext.PushProperty("Author", commit.Author.Name))
                using (LogContext.PushProperty("CommitDate", commit.Author.When.DateTime))
                {
                    _logger.LogInformation("Iniciando análise do commit: {CommitId} de {Author} em {CommitDate}", 
                        commit.Sha.Substring(0, 8), commit.Author.Name, commit.Author.When.DateTime);
                    
                    var startTime = DateTime.Now;
                    
                    try
                    {
                        // Obter as mudanças do commit
                        var changes = GetCommitChangesWithDiff(repo, commit);
                        
                        // Processar cada arquivo modificado sequencialmente
                        foreach (var change in changes)
                        {
                            if (Path.GetExtension(change.Path).ToLower() == ".cs")
                            {
                                _logger.LogInformation("Analisando arquivo: {FilePath}", change.Path);
                                
                                CodeAnalysis analysis = null;
                                
                                // Se temos diferenças significativas, analisar apenas as diferenças
                                if (!string.IsNullOrEmpty(change.DiffText))
                                {
                                    _logger.LogInformation("Analisando diferenças para {FilePath} ({DiffTextLength} caracteres)", 
                                        change.Path, change.DiffText.Length);
                                    
                                    // Aguardar a conclusão da análise antes de prosseguir
                                    analysis = await AnalyzeCodeDiff(change.OriginalContent, change.ModifiedContent, change.Path, commit);
                                }
                                else
                                {
                                    // Fallback para o método original se não conseguirmos obter diferenças
                                    _logger.LogInformation("Analisando arquivo completo para {FilePath}", change.Path);
                                    
                                    // Aguardar a conclusão da análise antes de prosseguir
                                    analysis = await AnalyzeCode(change.ModifiedContent, change.Path, commit);
                                }
                                
                                // Verificar se a análise foi bem-sucedida
                                if (analysis != null)
                                {
                                    // Salvar a análise no MongoDB e aguardar a conclusão
                                    await _repository.SaveAnalysisAsync(analysis);
                                    _logger.LogInformation("Análise salva com sucesso para o arquivo {FilePath}", change.Path);
                                }
                                else
                                {
                                    _logger.LogWarning("Não foi possível gerar análise para o arquivo {FilePath}", change.Path);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao analisar commit {CommitId}: {ErrorMessage}", commit.Sha, ex.Message);
                    }
                    
                    var endTime = DateTime.Now;
                    _logger.LogInformation("Análise do commit {CommitId} concluída em {Duration} segundos", 
                        commit.Sha.Substring(0, 8), (endTime - startTime).TotalSeconds);
                }
            }
            
            _logger.LogInformation("Análise de todos os commits concluída com sucesso");
        }

        private async Task<CodeAnalysis> AnalyzeCode(string content, string filePath, Commit commit)
        {
            using var logContext = LogContext.PushProperty("FilePath", filePath);
            LogContext.PushProperty("CommitId", commit.Sha.Substring(0, 8));
            LogContext.PushProperty("CodeLength", content?.Length ?? 0);
            LogContext.PushProperty("Author", commit.Author.Name);
            
            _logger.LogInformation("Iniciando análise de código para arquivo: {FilePath}", filePath);
            
            var startTime = DateTime.Now;
            
            try
            {
                // Verificar se temos conteúdo para analisar
                if (string.IsNullOrEmpty(content))
                {
                    _logger.LogWarning("Conteúdo vazio para o arquivo {FilePath}. Não é possível realizar análise.", filePath);
                    return new CodeAnalysis
                    {
                        CommitId = commit.Sha,
                        FilePath = filePath,
                        AuthorName = commit.Author.Name,
                        CommitDate = commit.Author.When.DateTime,
                        AnalysisDate = DateTime.Now,
                        Analysis = new AnalysisResult
                        {
                            AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                            NotaFinal = 0,
                            ComentarioGeral = "Não foi possível analisar: arquivo vazio."
                        },
                        RefactoringProposals = new List<RefactoringProposal>()
                    };
                }
                
                // Construir o prompt
                var prompt = BuildAnalysisPrompt(content, filePath);
                
                _logger.LogDebug("Prompt construído com {PromptLength} caracteres", prompt.Length);
                
                // Verificar se o prompt é muito grande e precisa ser dividido
                string jsonResponse;
                if (prompt.Length > _maxPromptLength)
                {
                    _logger.LogInformation("Prompt excede o tamanho máximo ({PromptLength} > {MaxLength}). Dividindo em partes...",
                        prompt.Length, _maxPromptLength);
                    
                    // Dividir o prompt em partes menores
                    var promptParts = SplitPromptIntoParts(prompt);
                    
                    // Processar cada parte e combinar os resultados
                    var results = new List<string>();
                    var modelName = _configuration.GetValue<string>("Ollama:ModelName", "deepseek-coder:33b");
                    for (int i = 0; i < promptParts.Count; i++)
                    {
                        _logger.LogInformation("Processando parte {PartNumber}/{TotalParts} do prompt", i + 1, promptParts.Count);
                        var partResult = await ProcessPromptPart(promptParts[i], modelName);
                        results.Add(partResult);
                    }
                    
                    // Combinar os resultados
                    jsonResponse = CombineResults(results);
                    _logger.LogDebug("Resultados combinados: {JsonLength} caracteres", jsonResponse?.Length ?? 0);
                }
                else
                {
                    // Processar o prompt completo
                    _logger.LogInformation("Processando prompt completo para análise de código");
                    var modelName = _configuration.GetValue<string>("Ollama:ModelName", "deepseek-coder:33b");
                    jsonResponse = await ProcessPromptPart(prompt, modelName);
                    _logger.LogDebug("Resposta JSON recebida: {JsonLength} caracteres", jsonResponse?.Length ?? 0);
                }
                
                // Deserializar a resposta JSON
                AnalysisResult analysisResult;
                try
                {
                    if (string.IsNullOrEmpty(jsonResponse))
                    {
                        _logger.LogWarning("Resposta JSON vazia para análise do arquivo {FilePath}", filePath);
                        analysisResult = new AnalysisResult
                        {
                            AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                            NotaFinal = 0,
                            ComentarioGeral = "Não foi possível obter análise: resposta vazia do modelo."
                        };
                    }
                    else
                    {
                        analysisResult = JsonSerializer.Deserialize<AnalysisResult>(jsonResponse);
                        
                        if (analysisResult == null)
                        {
                            _logger.LogWarning("Falha ao deserializar resposta JSON para análise do arquivo {FilePath}", filePath);
                            analysisResult = new AnalysisResult
                            {
                                AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                                NotaFinal = 0,
                                ComentarioGeral = "Falha ao processar resposta do modelo."
                            };
                        }
                        else
                        {
                            var endTime = DateTime.Now;
                            _logger.LogInformation("Análise concluída para arquivo {FilePath} em {Duration} segundos. Pontuação final: {NotaFinal}", 
                                filePath, (endTime - startTime).TotalSeconds, analysisResult.NotaFinal);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao deserializar resposta JSON para análise do arquivo {FilePath}: {ErrorMessage}", 
                        filePath, ex.Message);
                    
                    analysisResult = new AnalysisResult
                    {
                        AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                        NotaFinal = 0,
                        ComentarioGeral = $"Erro ao processar resposta: {ex.Message}"
                    };
                }
                
                // Criar e retornar a análise de código
                var codeAnalysis = new CodeAnalysis
                {
                    CommitId = commit.Sha,
                    FilePath = filePath,
                    AuthorName = commit.Author.Name,
                    CommitDate = commit.Author.When.DateTime,
                    AnalysisDate = DateTime.Now,
                    Analysis = analysisResult,
                    RefactoringProposals = new List<RefactoringProposal>()
                };
                
                return codeAnalysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar código para arquivo {FilePath}: {ErrorMessage}", filePath, ex.Message);
                
                return new CodeAnalysis
                {
                    CommitId = commit.Sha,
                    FilePath = filePath,
                    AuthorName = commit.Author.Name,
                    CommitDate = commit.Author.When.DateTime,
                    AnalysisDate = DateTime.Now,
                    Analysis = new AnalysisResult
                    {
                        AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                        NotaFinal = 0,
                        ComentarioGeral = $"Erro durante análise: {ex.Message}"
                    },
                    RefactoringProposals = new List<RefactoringProposal>()
                };
            }
        }

        private async Task<CodeAnalysis> AnalyzeCodeDiff(string originalContent, string modifiedContent, string filePath, Commit commit)
        {
            using var logContext = LogContext.PushProperty("FilePath", filePath);
            LogContext.PushProperty("CommitId", commit.Sha.Substring(0, 8));
            LogContext.PushProperty("Author", commit.Author.Name);
            LogContext.PushProperty("OriginalLength", originalContent?.Length ?? 0);
            LogContext.PushProperty("ModifiedLength", modifiedContent?.Length ?? 0);
            
            _logger.LogInformation("Iniciando análise de diferenças para arquivo: {FilePath}", filePath);
            
            var startTime = DateTime.Now;
            
            try
            {
                // Verificar se temos conteúdo para analisar
                if (string.IsNullOrEmpty(originalContent) && string.IsNullOrEmpty(modifiedContent))
                {
                    _logger.LogWarning("Conteúdo original e modificado vazios para o arquivo {FilePath}. Não é possível realizar análise.", filePath);
                    return new CodeAnalysis
                    {
                        CommitId = commit.Sha,
                        FilePath = filePath,
                        AuthorName = commit.Author.Name,
                        CommitDate = commit.Author.When.DateTime,
                        AnalysisDate = DateTime.Now,
                        Analysis = new AnalysisResult
                        {
                            AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                            NotaFinal = 0,
                            ComentarioGeral = "Não foi possível analisar: arquivos vazios."
                        },
                        RefactoringProposals = new List<RefactoringProposal>()
                    };
                }
                
                // Gerar o texto de diferenças
                _logger.LogDebug("Gerando texto de diferenças para o arquivo {FilePath}", filePath);
                var diffText = GenerateDiffText(originalContent, modifiedContent);
                
                if (string.IsNullOrEmpty(diffText))
                {
                    _logger.LogWarning("Não foi possível gerar diferenças para o arquivo {FilePath}. Os conteúdos podem ser idênticos.", filePath);
                    return new CodeAnalysis
                    {
                        CommitId = commit.Sha,
                        FilePath = filePath,
                        AuthorName = commit.Author.Name,
                        CommitDate = commit.Author.When.DateTime,
                        AnalysisDate = DateTime.Now,
                        Analysis = new AnalysisResult
                        {
                            AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                            NotaFinal = 0,
                            ComentarioGeral = "Não foi possível analisar: não foram detectadas diferenças significativas."
                        },
                        RefactoringProposals = new List<RefactoringProposal>()
                    };
                }
                
                _logger.LogDebug("Texto de diferenças gerado com {DiffLength} caracteres", diffText.Length);
                
                // Construir o prompt
                var prompt = BuildDiffAnalysisPrompt(filePath, diffText);
                
                _logger.LogDebug("Prompt construído com {PromptLength} caracteres", prompt.Length);
                
                // Verificar se o prompt é muito grande e precisa ser dividido
                string jsonResponse;
                if (prompt.Length > _maxPromptLength)
                {
                    _logger.LogInformation("Prompt excede o tamanho máximo ({PromptLength} > {MaxLength}). Dividindo em partes...",
                        prompt.Length, _maxPromptLength);
                    
                    // Dividir o prompt em partes menores
                    var promptParts = SplitPromptIntoParts(prompt);
                    
                    // Processar cada parte e combinar os resultados
                    var results = new List<string>();
                    var modelName = _configuration.GetValue<string>("Ollama:ModelName", "deepseek-coder:33b");
                    for (int i = 0; i < promptParts.Count; i++)
                    {
                        _logger.LogInformation("Processando parte {PartNumber}/{TotalParts} do prompt", i + 1, promptParts.Count);
                        var partResult = await ProcessPromptPart(promptParts[i], modelName);
                        results.Add(partResult);
                    }
                    
                    // Combinar os resultados
                    jsonResponse = CombineResults(results);
                    _logger.LogDebug("Resultados combinados: {JsonLength} caracteres", jsonResponse?.Length ?? 0);
                }
                else
                {
                    // Processar o prompt completo
                    _logger.LogInformation("Processando prompt completo para análise de diferenças");
                    var modelName = _configuration.GetValue<string>("Ollama:ModelName", "deepseek-coder:33b");
                    jsonResponse = await ProcessPromptPart(prompt, modelName);
                    _logger.LogDebug("Resposta JSON recebida: {JsonLength} caracteres", jsonResponse?.Length ?? 0);
                }
                
                // Deserializar a resposta JSON
                AnalysisResult analysisResult;
                RefactoringProposal refactoringProposal = null;
                
                try
                {
                    if (string.IsNullOrEmpty(jsonResponse))
                    {
                        _logger.LogWarning("Resposta JSON vazia para análise de diferenças do arquivo {FilePath}", filePath);
                        analysisResult = new AnalysisResult
                        {
                            AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                            NotaFinal = 0,
                            ComentarioGeral = "Não foi possível obter análise: resposta vazia do modelo."
                        };
                    }
                    else
                    {
                        // Tentar extrair a resposta JSON
                        var jsonOptions = new JsonDocumentOptions
                        {
                            AllowTrailingCommas = true,
                            CommentHandling = JsonCommentHandling.Skip
                        };
                        
                        try
                        {
                            using var document = JsonDocument.Parse(jsonResponse, jsonOptions);
                            var root = document.RootElement;
                            
                            // Extrair a proposta de refatoração, se existir
                            if (root.TryGetProperty("RefactoringProposal", out var refactoringElement) && 
                                !string.IsNullOrEmpty(refactoringElement.GetString()))
                            {
                                _logger.LogInformation("Proposta de refatoração encontrada para o arquivo {FilePath}", filePath);
                                refactoringProposal = new RefactoringProposal
                                {
                                    FilePath = filePath,
                                    CommitId = commit.Sha,
                                    Justification = refactoringElement.GetString(),
                                    ProposalDate = DateTime.Now,
                                    OriginalCode = originalContent ?? string.Empty,
                                    ProposedCode = modifiedContent ?? string.Empty,
                                    Priority = 3 // Prioridade média por padrão
                                };
                            }
                            else
                            {
                                _logger.LogInformation("Nenhuma proposta de refatoração encontrada para o arquivo {FilePath}", filePath);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Erro ao extrair proposta de refatoração do JSON para o arquivo {FilePath}: {ErrorMessage}", 
                                filePath, ex.Message);
                        }
                        
                        // Deserializar o resultado completo
                        analysisResult = JsonSerializer.Deserialize<AnalysisResult>(jsonResponse);
                        
                        if (analysisResult == null)
                        {
                            _logger.LogWarning("Falha ao deserializar resposta JSON para análise de diferenças do arquivo {FilePath}", filePath);
                            analysisResult = new AnalysisResult
                            {
                                AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                                NotaFinal = 0,
                                ComentarioGeral = "Falha ao processar resposta do modelo."
                            };
                        }
                        else
                        {
                            var endTime = DateTime.Now;
                            _logger.LogInformation("Análise de diferenças concluída para arquivo {FilePath} em {Duration} segundos. Pontuação final: {NotaFinal}", 
                                filePath, (endTime - startTime).TotalSeconds, analysisResult.NotaFinal);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao deserializar resposta JSON para análise de diferenças do arquivo {FilePath}: {ErrorMessage}", 
                        filePath, ex.Message);
                    
                    analysisResult = new AnalysisResult
                    {
                        AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                        NotaFinal = 0,
                        ComentarioGeral = $"Erro ao processar resposta: {ex.Message}"
                    };
                }
                
                // Criar e retornar a análise de código
                var codeAnalysis = new CodeAnalysis
                {
                    CommitId = commit.Sha,
                    FilePath = filePath,
                    AuthorName = commit.Author.Name,
                    CommitDate = commit.Author.When.DateTime,
                    AnalysisDate = DateTime.Now,
                    Analysis = analysisResult,
                    RefactoringProposals = refactoringProposal != null 
                        ? new List<RefactoringProposal> { refactoringProposal } 
                        : new List<RefactoringProposal>()
                };
                
                return codeAnalysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar diferenças para arquivo {FilePath}: {ErrorMessage}", filePath, ex.Message);
                
                return new CodeAnalysis
                {
                    CommitId = commit.Sha,
                    FilePath = filePath,
                    AuthorName = commit.Author.Name,
                    CommitDate = commit.Author.When.DateTime,
                    AnalysisDate = DateTime.Now,
                    Analysis = new AnalysisResult
                    {
                        AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                        NotaFinal = 0,
                        ComentarioGeral = $"Erro durante análise de diferenças: {ex.Message}"
                    },
                    RefactoringProposals = new List<RefactoringProposal>()
                };
            }
        }

        private async Task<string> ProcessPromptPart(string promptPart, string modelName, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Processando parte do prompt com {PromptLength} caracteres usando modelo {ModelName}", promptPart.Length, modelName);
            
            var timeoutMinutes = _configuration.GetValue<int>("Ollama:TimeoutMinutes", 15);
            var startTime = DateTime.Now;
            
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
                
                // Obter a URL da API Ollama da configuração
                var ollamaUrl = _configuration.GetValue<string>("Ollama:ApiUrl", "http://localhost:11434/api/generate");
                
                // Criar o objeto de requisição com opções simplificadas
                var requestData = new
                {
                    model = modelName,
                    prompt = promptPart,
                    stream = false,
                    options = new
                    {
                        temperature = 0.2,
                        top_p = 0.95,
                        num_predict = 4096
                    }
                };
                
                var jsonContent = JsonSerializer.Serialize(requestData);
                _logger.LogDebug("Enviando requisição para Ollama: {RequestJson}", jsonContent);
                
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("Enviando requisição para Ollama API: {OllamaUrl}", ollamaUrl);
                
                // Adicionar timeout mais curto para a requisição HTTP
                var response = await httpClient.PostAsync(ollamaUrl, content, cts.Token);
                
                _logger.LogDebug("Resposta recebida com status: {StatusCode}", response.StatusCode);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Erro na API Ollama: {StatusCode} - {ReasonPhrase}", 
                        (int)response.StatusCode, response.ReasonPhrase);
                    return string.Empty;
                }
                
                var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
                
                _logger.LogDebug("Resposta JSON recebida: {ResponseLength} caracteres", responseJson?.Length ?? 0);
                
                if (string.IsNullOrEmpty(responseJson))
                {
                    _logger.LogWarning("Resposta vazia da API Ollama");
                    return string.Empty;
                }
                
                try
                {
                    // Extrair o campo 'response' do JSON retornado pela API
                    using var document = JsonDocument.Parse(responseJson);
                    if (document.RootElement.TryGetProperty("response", out var responseElement))
                    {
                        var ollamaResponse = responseElement.GetString();
                        _logger.LogDebug("Resposta extraída do JSON: {ResponseLength} caracteres", ollamaResponse?.Length ?? 0);
                        
                        // Limpar códigos ANSI e extrair JSON
                        var cleanedOutput = CleanAnsiCodes(ollamaResponse ?? "");
                        _logger.LogDebug("Resposta limpa de códigos ANSI: {CleanedLength} caracteres", cleanedOutput.Length);
                        
                        var jsonResponse = ExtractJsonFromResponse(cleanedOutput);
                        _logger.LogDebug("JSON extraído da resposta: {JsonLength} caracteres", jsonResponse.Length);
                        
                        var endTime = DateTime.Now;
                        _logger.LogInformation("Processamento concluído em {Duration} segundos", 
                            (endTime - startTime).TotalSeconds);
                        
                        // Se não conseguimos extrair JSON, retornar a resposta limpa para debug
                        if (string.IsNullOrWhiteSpace(jsonResponse))
                        {
                            _logger.LogWarning("Não foi possível extrair JSON da resposta. Resposta original: {Response}", 
                                cleanedOutput.Length > 500 ? cleanedOutput.Substring(0, 500) + "..." : cleanedOutput);
                            
                            // Tentar criar um JSON válido como fallback
                            return CreateFallbackAnalysisJson();
                        }
                        
                        return jsonResponse;
                    }
                    else
                    {
                        _logger.LogWarning("Resposta da API Ollama não contém o campo 'response'. Resposta completa: {Response}", 
                            responseJson.Length > 500 ? responseJson.Substring(0, 500) + "..." : responseJson);
                        return CreateFallbackAnalysisJson();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar resposta da API Ollama: {ErrorMessage}", ex.Message);
                    return CreateFallbackAnalysisJson();
                }
            }
            catch (OperationCanceledException)
            {
                var endTime = DateTime.Now;
                _logger.LogWarning("Timeout atingido após {Duration} segundos", 
                    (endTime - startTime).TotalSeconds);
                return CreateFallbackAnalysisJson();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar parte do prompt: {ErrorMessage}", ex.Message);
                return CreateFallbackAnalysisJson();
            }
        }
        
        private string CreateFallbackAnalysisJson()
        {
            // Criar um JSON de análise vazio como fallback
            var fallbackAnalysis = new AnalysisResult
            {
                AnaliseGeral = new Dictionary<string, CriteriaAnalysis>
                {
                    ["CleanCode"] = new CriteriaAnalysis { Nota = 0, Comentario = "Não foi possível analisar" },
                    ["SOLID"] = new CriteriaAnalysis { Nota = 0, Comentario = "Não foi possível analisar" },
                    ["DesignPatterns"] = new CriteriaAnalysis { Nota = 0, Comentario = "Não foi possível analisar" },
                    ["Testabilidade"] = new CriteriaAnalysis { Nota = 0, Comentario = "Não foi possível analisar" },
                    ["Seguranca"] = new CriteriaAnalysis { Nota = 0, Comentario = "Não foi possível analisar" }
                },
                NotaFinal = 0,
                ComentarioGeral = "Não foi possível obter análise: erro na comunicação com o modelo."
            };
            
            return JsonSerializer.Serialize(fallbackAnalysis);
        }

        private string CleanAnsiCodes(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
                
            // Regex para remover códigos ANSI de escape
            var ansiRegex = new Regex(@"\x1B\[[^m]*m");
            return ansiRegex.Replace(input, "");
        }
        
        private string ExtractJsonFromResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                _logger.LogWarning("Resposta vazia ao tentar extrair JSON");
                return string.Empty;
            }

            try
            {
                _logger.LogDebug("Tentando extrair JSON da resposta com {Length} caracteres", response.Length);
                
                // Procurar por padrões comuns de início e fim de JSON
                int startIndex = -1;
                int endIndex = -1;
                
                // Procurar por { no início de uma linha ou após ```json
                var jsonStartPatterns = new[] { "```json\n{", "```\n{", "```json\r\n{", "```\r\n{", "\n{", "\r\n{", "{" };
                foreach (var pattern in jsonStartPatterns)
                {
                    int index = response.IndexOf(pattern);
                    if (index >= 0)
                    {
                        startIndex = index + pattern.Length - 1; // -1 para voltar ao {
                        break;
                    }
                }
                
                // Se não encontrou o início, tentar uma abordagem mais agressiva
                if (startIndex < 0)
                {
                    startIndex = response.IndexOf('{');
                    if (startIndex < 0)
                    {
                        _logger.LogWarning("Não foi possível encontrar o início do JSON na resposta");
                        
                        // Tentar converter a resposta em texto para JSON
                        return ConvertTextResponseToJson(response);
                    }
                }
                
                // Encontrar o fim do JSON (último } antes de ```, se houver)
                var jsonEndPatterns = new[] { "}\n```", "}\r\n```", "}" };
                foreach (var pattern in jsonEndPatterns)
                {
                    int index = response.LastIndexOf(pattern);
                    if (index >= 0)
                    {
                        endIndex = index + 1; // +1 para incluir o }
                        break;
                    }
                }
                
                // Se não encontrou o fim, assumir que é o último } da string
                if (endIndex < 0)
                {
                    endIndex = response.LastIndexOf('}');
                    if (endIndex < 0)
                    {
                        _logger.LogWarning("Não foi possível encontrar o fim do JSON na resposta");
                        
                        // Tentar converter a resposta em texto para JSON
                        return ConvertTextResponseToJson(response);
                    }
                    endIndex++; // +1 para incluir o }
                }
                
                // Verificar se os índices são válidos
                if (startIndex >= endIndex || startIndex < 0 || endIndex > response.Length)
                {
                    _logger.LogWarning("Índices inválidos ao extrair JSON: start={StartIndex}, end={EndIndex}, length={Length}", 
                        startIndex, endIndex, response.Length);
                    
                    // Tentar converter a resposta em texto para JSON
                    return ConvertTextResponseToJson(response);
                }
                
                // Extrair o JSON
                string jsonString = response.Substring(startIndex, endIndex - startIndex);
                
                _logger.LogDebug("JSON extraído com {Length} caracteres", jsonString.Length);
                
                // Validar se é um JSON válido
                try
                {
                    using var document = JsonDocument.Parse(jsonString);
                    _logger.LogDebug("JSON extraído é válido");
                    return jsonString;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "JSON extraído não é válido: {ErrorMessage}", ex.Message);
                    
                    // Tentar corrigir problemas comuns
                    _logger.LogDebug("Tentando corrigir JSON malformado");
                    
                    // Verificar se há chaves não balanceadas
                    int openBraces = jsonString.Count(c => c == '{');
                    int closeBraces = jsonString.Count(c => c == '}');
                    
                    if (openBraces > closeBraces)
                    {
                        _logger.LogDebug("Adicionando {Count} chaves de fechamento", openBraces - closeBraces);
                        jsonString += new string('}', openBraces - closeBraces);
                    }
                    else if (closeBraces > openBraces)
                    {
                        _logger.LogDebug("Removendo {Count} chaves de fechamento extras", closeBraces - openBraces);
                        int lastBrace = jsonString.LastIndexOf('}');
                        for (int i = 0; i < closeBraces - openBraces; i++)
                        {
                            if (lastBrace >= 0)
                            {
                                jsonString = jsonString.Remove(lastBrace, 1);
                                lastBrace = jsonString.LastIndexOf('}');
                            }
                        }
                    }
                    
                    // Tentar validar novamente
                    try
                    {
                        using var document = JsonDocument.Parse(jsonString);
                        _logger.LogDebug("JSON corrigido é válido");
                        return jsonString;
                    }
                    catch (JsonException ex2)
                    {
                        _logger.LogWarning(ex2, "Não foi possível corrigir o JSON: {ErrorMessage}", ex2.Message);
                        
                        // Tentar converter a resposta em texto para JSON
                        return ConvertTextResponseToJson(response);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair JSON da resposta: {ErrorMessage}", ex.Message);
                return string.Empty;
            }
        }
        
        private string ConvertTextResponseToJson(string textResponse)
        {
            _logger.LogInformation("Tentando converter resposta em texto para JSON");
            
            if (string.IsNullOrEmpty(textResponse))
            {
                _logger.LogWarning("Resposta em texto vazia");
                return string.Empty;
            }
            
            try
            {
                // Analisar a resposta em formato de texto
                var analysisResult = new AnalysisResult
                {
                    AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                    NotaFinal = 0,
                    ComentarioGeral = ""
                };
                
                // Remover marcações de código e outros caracteres especiais
                textResponse = Regex.Replace(textResponse, @"```[^`]*```", "", RegexOptions.Singleline);
                textResponse = textResponse.Replace("```", "").Trim();
                
                // Extrair comentário geral
                var generalCommentMatch = Regex.Match(textResponse, @"Comentário\s*Geral\s*:([^\n]*(?:\n(?!\*)[^\n]*)*)", RegexOptions.IgnoreCase);
                if (generalCommentMatch.Success && generalCommentMatch.Groups.Count > 1)
                {
                    analysisResult.ComentarioGeral = generalCommentMatch.Groups[1].Value.Trim();
                }
                else
                {
                    // Tentar extrair o último parágrafo como comentário geral
                    var paragraphs = Regex.Split(textResponse, @"\n\s*\n");
                    if (paragraphs.Length > 0)
                    {
                        analysisResult.ComentarioGeral = paragraphs.Last().Trim();
                    }
                }
                
                // Extrair nota final
                var finalScoreMatch = Regex.Match(textResponse, @"Nota\s*Final\s*:\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                if (finalScoreMatch.Success && finalScoreMatch.Groups.Count > 1)
                {
                    if (double.TryParse(finalScoreMatch.Groups[1].Value, out double finalScore))
                    {
                        analysisResult.NotaFinal = finalScore;
                    }
                }
                
                // Extrair análises de critérios
                ExtractCriteriaAnalysis(textResponse, "Clean Code", analysisResult);
                ExtractCriteriaAnalysis(textResponse, "SOLID", analysisResult);
                ExtractCriteriaAnalysis(textResponse, "Design Patterns", analysisResult);
                ExtractCriteriaAnalysis(textResponse, "Testabilidade", analysisResult);
                ExtractCriteriaAnalysis(textResponse, "Segurança", analysisResult);
                
                // Se não conseguiu extrair nenhuma análise, usar o texto completo como comentário geral
                if (analysisResult.AnaliseGeral.Count == 0 && string.IsNullOrEmpty(analysisResult.ComentarioGeral))
                {
                    analysisResult.ComentarioGeral = textResponse.Length > 500 ? textResponse.Substring(0, 500) + "..." : textResponse;
                }
                
                // Calcular nota final se não foi encontrada
                if (analysisResult.NotaFinal == 0 && analysisResult.AnaliseGeral.Count > 0)
                {
                    analysisResult.NotaFinal = analysisResult.AnaliseGeral.Values.Average(a => a.Nota);
                }
                
                // Serializar o resultado para JSON
                var jsonResult = JsonSerializer.Serialize(analysisResult);
                _logger.LogInformation("Resposta em texto convertida para JSON com sucesso: {Length} caracteres", jsonResult.Length);
                
                return jsonResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao converter resposta em texto para JSON: {ErrorMessage}", ex.Message);
                return string.Empty;
            }
        }
        
        private void ExtractCriteriaAnalysis(string textResponse, string criteriaName, AnalysisResult analysisResult)
        {
            try
            {
                // Padrão para capturar algo como "Clean Code: 8/10 - Comentário" ou "* Clean Code: 8/10 - Comentário"
                var pattern = $@"(?:\*\s*)?{Regex.Escape(criteriaName)}(?:\s*|\s*:\s*)(\d+(?:\.\d+)?)(?:/\d+)?(?:\s*[-:]\s*|\s+)([^\n]*(?:\n(?!\*)[^\n]*)*)";
                var match = Regex.Match(textResponse, pattern, RegexOptions.IgnoreCase);
                
                if (match.Success && match.Groups.Count > 2)
                {
                    var scoreStr = match.Groups[1].Value.Trim();
                    var comment = match.Groups[2].Value.Trim();
                    
                    if (double.TryParse(scoreStr, out double scoreDouble))
                    {
                        // Converter para int, já que a propriedade Nota é int
                        int score = (int)Math.Round(scoreDouble);
                        
                        var criteriaKey = criteriaName.Replace(" ", "");
                        analysisResult.AnaliseGeral[criteriaKey] = new CriteriaAnalysis
                        {
                            Nota = score,
                            Comentario = comment
                        };
                        
                        _logger.LogDebug("Extraído critério {CriteriaName}: Nota={Score}, Comentário={Comment}", 
                            criteriaName, score, comment);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao extrair análise do critério {CriteriaName}: {ErrorMessage}", 
                    criteriaName, ex.Message);
            }
        }

        private List<(string Path, string OriginalContent, string ModifiedContent, string DiffText)> GetCommitChangesWithDiff(Repository repo, Commit commit)
        {
            _logger.LogInformation("Obtendo mudanças para o commit {CommitId}", commit.Sha.Substring(0, 8));
            
            var changes = new List<(string Path, string OriginalContent, string ModifiedContent, string DiffText)>();
            
            try
            {
                var parent = commit.Parents.FirstOrDefault();
                if (parent == null)
                {
                    _logger.LogWarning("Commit {CommitId} não tem parent, não é possível obter diferenças", commit.Sha.Substring(0, 8));
                    return changes;
                }
                
                var comparison = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
                
                foreach (var change in comparison)
                {
                    if (change.Status == ChangeKind.Modified || change.Status == ChangeKind.Added)
                    {
                        string path = change.Path;
                        
                        _logger.LogDebug("Processando alteração em {FilePath} (Status: {Status})", path, change.Status);
                        
                        // Obter conteúdo original e modificado
                        string originalContent = "";
                        string modifiedContent = "";
                        
                        try
                        {
                            if (change.Status == ChangeKind.Modified)
                            {
                                var oldBlob = parent.Tree[path]?.Target as Blob;
                                if (oldBlob != null)
                                {
                                    using var contentStream = oldBlob.GetContentStream();
                                    using var reader = new StreamReader(contentStream);
                                    originalContent = reader.ReadToEnd();
                                }
                            }
                            
                            var newBlob = commit.Tree[path]?.Target as Blob;
                            if (newBlob != null)
                            {
                                using var contentStream = newBlob.GetContentStream();
                                using var reader = new StreamReader(contentStream);
                                modifiedContent = reader.ReadToEnd();
                            }
                            
                            // Gerar texto diff
                            string diffText = GenerateDiffText(originalContent, modifiedContent);
                            
                            changes.Add((path, originalContent, modifiedContent, diffText));
                            
                            _logger.LogDebug("Alteração processada para {FilePath}: Original ({OriginalLength} chars), " +
                                "Modificado ({ModifiedLength} chars), Diff ({DiffLength} chars)",
                                path, originalContent.Length, modifiedContent.Length, diffText.Length);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao processar alteração em {FilePath}: {ErrorMessage}", path, ex.Message);
                        }
                    }
                }
                
                _logger.LogInformation("Obtidas {ChangeCount} alterações para o commit {CommitId}", 
                    changes.Count, commit.Sha.Substring(0, 8));
                
                return changes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter mudanças para o commit {CommitId}: {ErrorMessage}", 
                    commit.Sha.Substring(0, 8), ex.Message);
                return changes;
            }
        }

        private string GenerateDiffText(string originalContent, string modifiedContent)
        {
            if (string.IsNullOrEmpty(originalContent) && string.IsNullOrEmpty(modifiedContent))
                return "";
                
            if (string.IsNullOrEmpty(originalContent))
                return $"+ {modifiedContent.Replace("\n", "\n+ ")}";
                
            if (string.IsNullOrEmpty(modifiedContent))
                return $"- {originalContent.Replace("\n", "\n- ")}";
            
            try
            {
                _logger.LogDebug("Gerando diff entre conteúdos: Original ({OriginalLength} chars), Modificado ({ModifiedLength} chars)",
                    originalContent.Length, modifiedContent.Length);
                    
                var originalLines = originalContent.Split('\n');
                var modifiedLines = modifiedContent.Split('\n');
                
                var diffBuilder = new StringBuilder();
                
                // Implementação simplificada de diff
                var lcs = LongestCommonSubsequence(originalLines, modifiedLines);
                
                int originalIndex = 0;
                int modifiedIndex = 0;
                
                foreach (var item in lcs)
                {
                    // Adicionar linhas removidas
                    while (originalIndex < item.OriginalIndex)
                    {
                        diffBuilder.AppendLine($"- {originalLines[originalIndex]}");
                        originalIndex++;
                    }
                    
                    // Adicionar linhas adicionadas
                    while (modifiedIndex < item.ModifiedIndex)
                    {
                        diffBuilder.AppendLine($"+ {modifiedLines[modifiedIndex]}");
                        modifiedIndex++;
                    }
                    
                    // Adicionar linha comum
                    diffBuilder.AppendLine($"  {originalLines[originalIndex]}");
                    originalIndex++;
                    modifiedIndex++;
                }
                
                // Adicionar linhas restantes
                while (originalIndex < originalLines.Length)
                {
                    diffBuilder.AppendLine($"- {originalLines[originalIndex]}");
                    originalIndex++;
                }
                
                while (modifiedIndex < modifiedLines.Length)
                {
                    diffBuilder.AppendLine($"+ {modifiedLines[modifiedIndex]}");
                    modifiedIndex++;
                }
                
                var result = diffBuilder.ToString();
                _logger.LogDebug("Diff gerado com {LineCount} linhas", result.Split('\n').Length);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar diff: {ErrorMessage}", ex.Message);
                return "";
            }
        }
        
        private List<(int OriginalIndex, int ModifiedIndex)> LongestCommonSubsequence(string[] original, string[] modified)
        {
            int[,] lengths = new int[original.Length + 1, modified.Length + 1];
            
            // Preencher a matriz de comprimentos
            for (int i = 0; i < original.Length; i++)
            {
                for (int j = 0; j < modified.Length; j++)
                {
                    if (original[i] == modified[j])
                    {
                        lengths[i + 1, j + 1] = lengths[i, j] + 1;
                    }
                    else
                    {
                        lengths[i + 1, j + 1] = Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
                    }
                }
            }
            
            // Reconstruir a sequência
            var sequence = new List<(int OriginalIndex, int ModifiedIndex)>();
            int originalIndex = original.Length;
            int modifiedIndex = modified.Length;
            
            while (originalIndex > 0 && modifiedIndex > 0)
            {
                if (original[originalIndex - 1] == modified[modifiedIndex - 1])
                {
                    sequence.Add((originalIndex - 1, modifiedIndex - 1));
                    originalIndex--;
                    modifiedIndex--;
                }
                else if (lengths[originalIndex - 1, modifiedIndex] >= lengths[originalIndex, modifiedIndex - 1])
                {
                    originalIndex--;
                }
                else
                {
                    modifiedIndex--;
                }
            }
            
            sequence.Reverse();
            return sequence;
        }

        private string BuildAnalysisPrompt(string content, string filePath)
        {
            using var logContext = LogContext.PushProperty("FilePath", filePath);
            LogContext.PushProperty("CodeLength", content?.Length ?? 0);
            
            _logger.LogInformation("Construindo prompt para análise de código para arquivo {FilePath}", filePath);
            
            var promptBuilder = new StringBuilder();
            
            // Cabeçalho do prompt
            promptBuilder.AppendLine("Você é um especialista em análise de código e deve avaliar a qualidade do código a seguir.");
            promptBuilder.AppendLine("Analise o código e forneça uma avaliação detalhada com base nos seguintes critérios:");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Critérios de avaliação:");
            promptBuilder.AppendLine("1. Clean Code: Avalie a legibilidade, simplicidade e organização do código.");
            promptBuilder.AppendLine("2. Princípios SOLID: Verifique se o código segue os princípios SOLID.");
            promptBuilder.AppendLine("3. Design Patterns: Identifique padrões de design utilizados ou que poderiam ser aplicados.");
            promptBuilder.AppendLine("4. Testabilidade: Avalie se o código é fácil de testar.");
            promptBuilder.AppendLine("5. Segurança: Verifique se existem problemas de segurança no código.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Para cada critério, atribua uma nota de 0 a 10 e forneça um comentário explicativo.");
            promptBuilder.AppendLine();
            
            // Informações sobre o arquivo
            promptBuilder.AppendLine($"Arquivo: {filePath}");
            promptBuilder.AppendLine();
            
            // Adicionar o código para análise
            promptBuilder.AppendLine("Código para análise:");
            promptBuilder.AppendLine("```csharp");
            promptBuilder.AppendLine(content);
            promptBuilder.AppendLine("```");
            promptBuilder.AppendLine();
            
            // Instruções para o formato da resposta
            promptBuilder.AppendLine("Forneça sua análise no seguinte formato JSON:");
            promptBuilder.AppendLine("```json");
            promptBuilder.AppendLine("{");
            promptBuilder.AppendLine("  \"AnaliseGeral\": {");
            promptBuilder.AppendLine("    \"cleanCode\": { \"Nota\": 0, \"Comentario\": \"\" },");
            promptBuilder.AppendLine("    \"solidPrinciples\": { \"Nota\": 0, \"Comentario\": \"\" },");
            promptBuilder.AppendLine("    \"designPatterns\": { \"Nota\": 0, \"Comentario\": \"\" },");
            promptBuilder.AppendLine("    \"testability\": { \"Nota\": 0, \"Comentario\": \"\" },");
            promptBuilder.AppendLine("    \"security\": { \"Nota\": 0, \"Comentario\": \"\" }");
            promptBuilder.AppendLine("  },");
            promptBuilder.AppendLine("  \"NotaFinal\": 0,");
            promptBuilder.AppendLine("  \"ComentarioGeral\": \"\"");
            promptBuilder.AppendLine("}");
            promptBuilder.AppendLine("```");
            
            var prompt = promptBuilder.ToString();
            _logger.LogDebug("Prompt construído com {PromptLength} caracteres", prompt.Length);
            
            // Verificar se o prompt é muito grande
            if (prompt.Length > _maxPromptLength)
            {
                _logger.LogWarning("Prompt excede o tamanho máximo ({CurrentLength} > {MaxLength}). Truncando código...", 
                    prompt.Length, _maxPromptLength);
                
                // Calcular quanto precisamos reduzir
                int excessLength = prompt.Length - _maxPromptLength + 100; // Margem de segurança
                
                // Truncar o código
                int maxCodeLength = content.Length - excessLength;
                if (maxCodeLength < 500)
                {
                    _logger.LogWarning("Código seria muito pequeno após truncamento. Usando abordagem alternativa.");
                    // Abordagem alternativa: manter apenas o início e o fim do código
                    int halfLength = Math.Max(500, (content.Length - excessLength) / 2);
                    string truncatedCode = content.Substring(0, halfLength) + 
                        "\n\n// ... parte do código omitida devido ao tamanho ...\n\n" + 
                        content.Substring(content.Length - halfLength);
                    
                    // Reconstruir o prompt com o código truncado
                    promptBuilder = new StringBuilder(prompt);
                    promptBuilder.Replace(content, truncatedCode);
                    prompt = promptBuilder.ToString();
                    
                    _logger.LogDebug("Prompt reconstruído com código truncado: {NewLength} caracteres", prompt.Length);
                }
                else
                {
                    // Truncar o código normalmente
                    string truncatedCode = content.Substring(0, maxCodeLength) + 
                        "\n\n// ... restante do código omitido devido ao tamanho ...";
                    
                    // Reconstruir o prompt com o código truncado
                    promptBuilder = new StringBuilder(prompt);
                    promptBuilder.Replace(content, truncatedCode);
                    prompt = promptBuilder.ToString();
                    
                    _logger.LogDebug("Prompt reconstruído com código truncado: {NewLength} caracteres", prompt.Length);
                }
            }
            
            return prompt;
        }

        private string BuildDiffAnalysisPrompt(string filePath, string diffText)
        {
            using var logContext = LogContext.PushProperty("FilePath", filePath);
            LogContext.PushProperty("DiffLength", diffText?.Length ?? 0);
            
            _logger.LogInformation("Construindo prompt para análise de diferenças para arquivo {FilePath}", filePath);
            
            var promptBuilder = new StringBuilder();
            
            // Cabeçalho do prompt
            promptBuilder.AppendLine("Você é um especialista em análise de código e deve avaliar as alterações feitas em um arquivo de código.");
            promptBuilder.AppendLine("Analise as diferenças abaixo e forneça uma avaliação detalhada das alterações.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Critérios de avaliação:");
            promptBuilder.AppendLine("1. Clean Code: Avalie se as alterações melhoraram a legibilidade, simplicidade e organização do código.");
            promptBuilder.AppendLine("2. Princípios SOLID: Verifique se as alterações seguem ou melhoram a aderência aos princípios SOLID.");
            promptBuilder.AppendLine("3. Design Patterns: Identifique se padrões de design foram aplicados ou melhorados.");
            promptBuilder.AppendLine("4. Testabilidade: Avalie se as alterações facilitam ou dificultam o teste do código.");
            promptBuilder.AppendLine("5. Segurança: Verifique se as alterações introduzem ou corrigem problemas de segurança.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Para cada critério, atribua uma nota de 0 a 10 e forneça um comentário explicativo.");
            promptBuilder.AppendLine();
            
            // Informações sobre o arquivo
            promptBuilder.AppendLine($"Arquivo: {filePath}");
            promptBuilder.AppendLine();
            
            // Formato das diferenças
            promptBuilder.AppendLine("Legenda do diff:");
            promptBuilder.AppendLine("- Linhas com '-' no início foram removidas");
            promptBuilder.AppendLine("- Linhas com '+' no início foram adicionadas");
            promptBuilder.AppendLine("- Linhas com ' ' (espaço) no início permaneceram inalteradas");
            promptBuilder.AppendLine();
            
            // Adicionar o texto das diferenças
            promptBuilder.AppendLine("Diferenças:");
            promptBuilder.AppendLine("```");
            promptBuilder.AppendLine(diffText);
            promptBuilder.AppendLine("```");
            promptBuilder.AppendLine();
            
            // Instruções para o formato da resposta
            promptBuilder.AppendLine("Forneça sua análise no seguinte formato JSON:");
            promptBuilder.AppendLine("```json");
            promptBuilder.AppendLine("{");
            promptBuilder.AppendLine("  \"AnaliseGeral\": {");
            promptBuilder.AppendLine("    \"cleanCode\": { \"Nota\": 0, \"Comentario\": \"\" },");
            promptBuilder.AppendLine("    \"solidPrinciples\": { \"Nota\": 0, \"Comentario\": \"\" },");
            promptBuilder.AppendLine("    \"designPatterns\": { \"Nota\": 0, \"Comentario\": \"\" },");
            promptBuilder.AppendLine("    \"testability\": { \"Nota\": 0, \"Comentario\": \"\" },");
            promptBuilder.AppendLine("    \"security\": { \"Nota\": 0, \"Comentario\": \"\" }");
            promptBuilder.AppendLine("  },");
            promptBuilder.AppendLine("  \"NotaFinal\": 0,");
            promptBuilder.AppendLine("  \"ComentarioGeral\": \"\",");
            promptBuilder.AppendLine("  \"RefactoringProposal\": \"\"");
            promptBuilder.AppendLine("}");
            promptBuilder.AppendLine("```");
            
            var prompt = promptBuilder.ToString();
            _logger.LogDebug("Prompt construído com {PromptLength} caracteres", prompt.Length);
            
            // Verificar se o prompt é muito grande
            if (prompt.Length > _maxPromptLength)
            {
                _logger.LogWarning("Prompt excede o tamanho máximo ({CurrentLength} > {MaxLength}). Truncando diff...", 
                    prompt.Length, _maxPromptLength);
                
                // Calcular quanto precisamos reduzir
                int excessLength = prompt.Length - _maxPromptLength + 100; // Margem de segurança
                
                // Truncar o texto diff
                int maxDiffLength = diffText.Length - excessLength;
                if (maxDiffLength < 500)
                {
                    _logger.LogWarning("Diff seria muito pequeno após truncamento. Usando abordagem alternativa.");
                    // Abordagem alternativa: manter apenas o início e o fim do diff
                    int halfLength = Math.Max(500, (diffText.Length - excessLength) / 2);
                    string truncatedDiff = diffText.Substring(0, halfLength) + 
                        "\n\n[... parte do diff omitida devido ao tamanho ...]\n\n" + 
                        diffText.Substring(diffText.Length - halfLength);
                    
                    // Reconstruir o prompt com o diff truncado
                    promptBuilder = new StringBuilder(prompt);
                    promptBuilder.Replace(diffText, truncatedDiff);
                    prompt = promptBuilder.ToString();
                    
                    _logger.LogDebug("Prompt reconstruído com diff truncado: {NewLength} caracteres", prompt.Length);
                }
                else
                {
                    // Truncar o diff normalmente
                    string truncatedDiff = diffText.Substring(0, maxDiffLength) + 
                        "\n\n[... restante do diff omitido devido ao tamanho ...]";
                    
                    // Reconstruir o prompt com o diff truncado
                    promptBuilder = new StringBuilder(prompt);
                    promptBuilder.Replace(diffText, truncatedDiff);
                    prompt = promptBuilder.ToString();
                    
                    _logger.LogDebug("Prompt reconstruído com diff truncado: {NewLength} caracteres", prompt.Length);
                }
            }
            
            return prompt;
        }

        private string CombineResults(List<string> results)
        {
            _logger.LogInformation("Combinando {Count} resultados parciais", results.Count);
            
            try
            {
                // Se temos apenas um resultado, retorná-lo diretamente
                if (results.Count == 1)
                {
                    _logger.LogDebug("Apenas um resultado encontrado, retornando diretamente");
                    return results[0];
                }
                
                // Tentar combinar resultados JSON
                var combinedAnalysis = new AnalysisResult
                {
                    AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                    NotaFinal = 0,
                    ComentarioGeral = ""
                };
                
                // Inicializar critérios
                combinedAnalysis.AnaliseGeral["cleanCode"] = new CriteriaAnalysis { Nota = 0, Comentario = "" };
                combinedAnalysis.AnaliseGeral["solidPrinciples"] = new CriteriaAnalysis { Nota = 0, Comentario = "" };
                combinedAnalysis.AnaliseGeral["designPatterns"] = new CriteriaAnalysis { Nota = 0, Comentario = "" };
                combinedAnalysis.AnaliseGeral["testability"] = new CriteriaAnalysis { Nota = 0, Comentario = "" };
                combinedAnalysis.AnaliseGeral["security"] = new CriteriaAnalysis { Nota = 0, Comentario = "" };
                
                int validResults = 0;
                
                foreach (var result in results)
                {
                    try
                    {
                        var analysisResult = JsonSerializer.Deserialize<AnalysisResult>(result);
                        if (analysisResult != null)
                        {
                            _logger.LogDebug("Processando resultado parcial {ResultNumber}", validResults + 1);
                            
                            // Combinar comentários
                            if (analysisResult.AnaliseGeral.ContainsKey("cleanCode"))
                                combinedAnalysis.AnaliseGeral["cleanCode"].Comentario += (string.IsNullOrEmpty(combinedAnalysis.AnaliseGeral["cleanCode"].Comentario) ? "" : " ") + analysisResult.AnaliseGeral["cleanCode"].Comentario;
                            
                            if (analysisResult.AnaliseGeral.ContainsKey("solidPrinciples"))
                                combinedAnalysis.AnaliseGeral["solidPrinciples"].Comentario += (string.IsNullOrEmpty(combinedAnalysis.AnaliseGeral["solidPrinciples"].Comentario) ? "" : " ") + analysisResult.AnaliseGeral["solidPrinciples"].Comentario;
                            
                            if (analysisResult.AnaliseGeral.ContainsKey("designPatterns"))
                                combinedAnalysis.AnaliseGeral["designPatterns"].Comentario += (string.IsNullOrEmpty(combinedAnalysis.AnaliseGeral["designPatterns"].Comentario) ? "" : " ") + analysisResult.AnaliseGeral["designPatterns"].Comentario;
                            
                            if (analysisResult.AnaliseGeral.ContainsKey("testability"))
                                combinedAnalysis.AnaliseGeral["testability"].Comentario += (string.IsNullOrEmpty(combinedAnalysis.AnaliseGeral["testability"].Comentario) ? "" : " ") + analysisResult.AnaliseGeral["testability"].Comentario;
                            
                            if (analysisResult.AnaliseGeral.ContainsKey("security"))
                                combinedAnalysis.AnaliseGeral["security"].Comentario += (string.IsNullOrEmpty(combinedAnalysis.AnaliseGeral["security"].Comentario) ? "" : " ") + analysisResult.AnaliseGeral["security"].Comentario;
                            
                            // Acumular notas
                            if (analysisResult.AnaliseGeral.ContainsKey("cleanCode"))
                                combinedAnalysis.AnaliseGeral["cleanCode"].Nota += analysisResult.AnaliseGeral["cleanCode"].Nota;
                            
                            if (analysisResult.AnaliseGeral.ContainsKey("solidPrinciples"))
                                combinedAnalysis.AnaliseGeral["solidPrinciples"].Nota += analysisResult.AnaliseGeral["solidPrinciples"].Nota;
                            
                            if (analysisResult.AnaliseGeral.ContainsKey("designPatterns"))
                                combinedAnalysis.AnaliseGeral["designPatterns"].Nota += analysisResult.AnaliseGeral["designPatterns"].Nota;
                            
                            if (analysisResult.AnaliseGeral.ContainsKey("testability"))
                                combinedAnalysis.AnaliseGeral["testability"].Nota += analysisResult.AnaliseGeral["testability"].Nota;
                            
                            if (analysisResult.AnaliseGeral.ContainsKey("security"))
                                combinedAnalysis.AnaliseGeral["security"].Nota += analysisResult.AnaliseGeral["security"].Nota;
                            
                            combinedAnalysis.NotaFinal += analysisResult.NotaFinal;
                            
                            combinedAnalysis.ComentarioGeral += (string.IsNullOrEmpty(combinedAnalysis.ComentarioGeral) ? "" : " ") + analysisResult.ComentarioGeral;
                            
                            validResults++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao combinar resultado parcial: {ErrorMessage}", ex.Message);
                    }
                }
                
                // Calcular médias se tivermos resultados válidos
                if (validResults > 0)
                {
                    _logger.LogInformation("Calculando médias para {ValidResults} resultados válidos", validResults);
                    
                    if (combinedAnalysis.AnaliseGeral.ContainsKey("cleanCode"))
                        combinedAnalysis.AnaliseGeral["cleanCode"].Nota /= validResults;
                    
                    if (combinedAnalysis.AnaliseGeral.ContainsKey("solidPrinciples"))
                        combinedAnalysis.AnaliseGeral["solidPrinciples"].Nota /= validResults;
                    
                    if (combinedAnalysis.AnaliseGeral.ContainsKey("designPatterns"))
                        combinedAnalysis.AnaliseGeral["designPatterns"].Nota /= validResults;
                    
                    if (combinedAnalysis.AnaliseGeral.ContainsKey("testability"))
                        combinedAnalysis.AnaliseGeral["testability"].Nota /= validResults;
                    
                    if (combinedAnalysis.AnaliseGeral.ContainsKey("security"))
                        combinedAnalysis.AnaliseGeral["security"].Nota /= validResults;
                    
                    combinedAnalysis.NotaFinal /= validResults;
                    
                    return JsonSerializer.Serialize(combinedAnalysis);
                }
                
                // Se não conseguimos combinar os resultados, retornar o primeiro resultado válido
                _logger.LogWarning("Não foi possível combinar resultados. Retornando primeiro resultado válido");
                foreach (var result in results)
                {
                    if (!string.IsNullOrEmpty(result))
                    {
                        return result;
                    }
                }
                
                return "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao combinar resultados: {ErrorMessage}", ex.Message);
                
                // Em caso de erro, retornar o primeiro resultado não vazio
                foreach (var result in results)
                {
                    if (!string.IsNullOrEmpty(result))
                    {
                        return result;
                    }
                }
                
                return "";
            }
        }

        private List<string> SplitPromptIntoParts(string prompt)
        {
            _logger.LogInformation("Dividindo prompt de {PromptLength} caracteres em partes", prompt.Length);
            
            var maxPromptLength = _configuration.GetValue<int>("Ollama:MaxPromptLength", 8000);
            
            // Identificar o início do código (após o cabeçalho do prompt)
            int codeStart = prompt.IndexOf("```csharp");
            if (codeStart < 0)
            {
                codeStart = prompt.IndexOf("Código para análise:");
                if (codeStart < 0)
                {
                    // Se não encontrarmos o início do código, dividir o prompt em partes iguais
                    _logger.LogWarning("Não foi possível identificar o início do código. Usando divisão genérica");
                    return SplitPromptIntoEqualParts(prompt);
                }
            }
            
            _logger.LogDebug("Início do código encontrado na posição {Position}", codeStart);
            
            // Extrair o cabeçalho do prompt (instruções)
            string header = prompt.Substring(0, codeStart);
            
            // Encontrar o final do bloco de código
            int codeEnd = prompt.LastIndexOf("```");
            if (codeEnd < codeStart)
            {
                codeEnd = prompt.Length;
            }
            
            // Extrair o código
            string code = prompt.Substring(codeStart, codeEnd - codeStart);
            
            // Extrair o rodapé (se houver)
            string footer = codeEnd < prompt.Length ? prompt.Substring(codeEnd) : "";
            
            // Dividir o código em partes
            var codeLines = code.Split('\n');
            var currentPart = new StringBuilder(header);
            currentPart.AppendLine("\nPARTE 1 - ANÁLISE PARCIAL DO CÓDIGO\n");
            
            int partNumber = 1;
            int lineCount = 0;
            int totalLines = codeLines.Length;
            var parts = new List<string>();
            
            _logger.LogDebug("Dividindo {LineCount} linhas de código em partes", codeLines.Length);
            
            foreach (var line in codeLines)
            {
                // Verificar se adicionar esta linha excederia o tamanho máximo
                if (currentPart.Length + line.Length + 2 > maxPromptLength / 2 && lineCount > 0)
                {
                    // Finalizar a parte atual
                    if (partNumber == 1)
                    {
                        currentPart.AppendLine("\nEsta é apenas a primeira parte do código. Por favor, analise esta parte e forneça uma avaliação parcial.");
                    }
                    else
                    {
                        currentPart.AppendLine($"\nEsta é a parte {partNumber} do código. Por favor, analise esta parte e forneça uma avaliação parcial.");
                    }
                    
                    parts.Add(currentPart.ToString());
                    _logger.LogDebug("Parte {PartNumber} criada com {CharCount} caracteres", partNumber, currentPart.Length);
                    
                    // Iniciar nova parte
                    partNumber++;
                    currentPart = new StringBuilder(header);
                    currentPart.AppendLine($"\nPARTE {partNumber} - CONTINUAÇÃO DA ANÁLISE DO CÓDIGO\n");
                    lineCount = 0;
                }
                
                currentPart.AppendLine(line);
                lineCount++;
            }
            
            // Adicionar o rodapé à última parte
            if (footer.Length > 0)
            {
                currentPart.Append(footer);
            }
            
            // Adicionar a última parte se não estiver vazia
            if (lineCount > 0)
            {
                currentPart.AppendLine($"\nEsta é a parte final ({partNumber} de {partNumber}) do código. Por favor, analise esta parte e forneça uma avaliação completa.");
                parts.Add(currentPart.ToString());
                _logger.LogDebug("Parte final {PartNumber} criada com {CharCount} caracteres", partNumber, currentPart.Length);
            }
            
            _logger.LogInformation("Prompt dividido em {PartCount} partes", parts.Count);
            
            return parts;
        }
        
        private List<string> SplitPromptIntoEqualParts(string prompt)
        {
            _logger.LogInformation("Dividindo prompt em partes iguais");
            
            var maxPromptLength = _configuration.GetValue<int>("Ollama:MaxPromptLength", 8000);
            var parts = new List<string>();
            int maxPartLength = maxPromptLength / 2;
            
            // Dividir o prompt em partes de tamanho aproximadamente igual
            int partCount = (int)Math.Ceiling((double)prompt.Length / maxPartLength);
            
            // Se tivermos apenas uma parte, retornar o prompt original
            if (partCount <= 1)
            {
                _logger.LogDebug("Prompt não precisa ser dividido, retornando como uma única parte");
                parts.Add(prompt);
                return parts;
            }
            
            _logger.LogDebug("Dividindo prompt em {PartCount} partes", partCount);
            
            // Tentar encontrar pontos naturais de quebra (parágrafos, pontuação)
            int currentPos = 0;
            for (int i = 0; i < partCount; i++)
            {
                int targetEnd = Math.Min(currentPos + maxPartLength, prompt.Length);
                int actualEnd = targetEnd;
                
                // Se não estamos no final do prompt, procurar um ponto natural de quebra
                if (targetEnd < prompt.Length)
                {
                    // Procurar por quebras de parágrafo
                    int paragraphBreak = prompt.LastIndexOf("\n\n", targetEnd, targetEnd - currentPos);
                    if (paragraphBreak > currentPos + maxPartLength / 2)
                    {
                        actualEnd = paragraphBreak + 2; // +2 para incluir a quebra de linha
                    }
                    else
                    {
                        // Procurar por quebras de linha
                        int lineBreak = prompt.LastIndexOf('\n', targetEnd, targetEnd - currentPos);
                        if (lineBreak > currentPos + maxPartLength / 2)
                        {
                            actualEnd = lineBreak + 1; // +1 para incluir a quebra de linha
                        }
                        else
                        {
                            // Procurar por pontuação seguida de espaço
                            foreach (char punct in new[] { '.', '!', '?', ';' })
                            {
                                int punctPos = prompt.LastIndexOf(punct + " ", targetEnd, targetEnd - currentPos);
                                if (punctPos > currentPos + maxPartLength / 2)
                                {
                                    actualEnd = punctPos + 2; // +2 para incluir a pontuação e o espaço
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // Extrair a parte atual
                string part = prompt.Substring(currentPos, actualEnd - currentPos);
                
                // Adicionar cabeçalho informativo
                if (partCount > 1)
                {
                    part = $"PARTE {i+1} DE {partCount} - ANÁLISE PARCIAL\n\n" + part;
                    
                    if (i < partCount - 1)
                    {
                        part += "\n\nContinua na próxima parte...";
                    }
                }
                
                parts.Add(part);
                _logger.LogDebug("Parte {PartNumber}/{TotalParts} criada com {CharCount} caracteres", 
                    i + 1, partCount, part.Length);
                
                currentPos = actualEnd;
            }
            
            return parts;
        }

        private async Task<bool> TestOllamaConnection(string modelName)
        {
            _logger.LogInformation("Testando conexão com Ollama usando modelo {ModelName}", modelName);
            
            try
            {
                var ollamaApiUrl = _configuration.GetValue<string>("Ollama:ApiUrl", "http://localhost:11434/api/generate");
                var ollamaTagsUrl = "http://localhost:11434/api/tags";
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30); // Timeout curto para teste
                
                // Primeiro verificar se o Ollama está rodando e quais modelos estão disponíveis
                _logger.LogDebug("Verificando modelos disponíveis no Ollama: {OllamaTagsUrl}", ollamaTagsUrl);
                
                try
                {
                    var tagsResponse = await httpClient.GetAsync(ollamaTagsUrl);
                    
                    if (!tagsResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Erro ao verificar modelos disponíveis: {StatusCode} - {ReasonPhrase}", 
                            (int)tagsResponse.StatusCode, tagsResponse.ReasonPhrase);
                        return false;
                    }
                    
                    var tagsJson = await tagsResponse.Content.ReadAsStringAsync();
                    
                    if (string.IsNullOrEmpty(tagsJson))
                    {
                        _logger.LogWarning("Resposta vazia ao verificar modelos disponíveis");
                        return false;
                    }
                    
                    // Verificar se o modelo solicitado está na lista de modelos disponíveis
                    using var document = JsonDocument.Parse(tagsJson);
                    if (document.RootElement.TryGetProperty("models", out var modelsElement))
                    {
                        bool modelFound = false;
                        
                        foreach (var model in modelsElement.EnumerateArray())
                        {
                            if (model.TryGetProperty("name", out var nameElement))
                            {
                                var name = nameElement.GetString();
                                if (name == modelName)
                                {
                                    modelFound = true;
                                    _logger.LogInformation("Modelo {ModelName} encontrado na lista de modelos disponíveis", modelName);
                                    break;
                                }
                            }
                        }
                        
                        if (!modelFound)
                        {
                            _logger.LogWarning("Modelo {ModelName} não está disponível no Ollama", modelName);
                            _logger.LogInformation("Modelos disponíveis: {AvailableModels}", tagsJson);
                            return false;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Resposta da API Ollama não contém a lista de modelos");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao verificar modelos disponíveis: {ErrorMessage}", ex.Message);
                    return false;
                }
                
                // Agora testar o modelo com um prompt simples
                _logger.LogDebug("Testando modelo {ModelName} com um prompt simples", modelName);
                
                // Criar um prompt simples para teste
                var testPrompt = "Responda com uma palavra: Teste";
                
                var requestData = new
                {
                    model = modelName,
                    prompt = testPrompt,
                    stream = false,
                    options = new
                    {
                        temperature = 0.1,
                        num_predict = 10 // Valor baixo para resposta rápida
                    }
                };
                
                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("Enviando requisição de teste para Ollama API: {OllamaUrl}", ollamaApiUrl);
                
                var response = await httpClient.PostAsync(ollamaApiUrl, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Erro na API Ollama durante teste: {StatusCode} - {ReasonPhrase}", 
                        (int)response.StatusCode, response.ReasonPhrase);
                    return false;
                }
                
                var responseJson = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrEmpty(responseJson))
                {
                    _logger.LogWarning("Resposta vazia da API Ollama durante teste");
                    return false;
                }
                
                // Verificar se a resposta contém o campo 'response'
                using var responseDocument = JsonDocument.Parse(responseJson);
                if (!responseDocument.RootElement.TryGetProperty("response", out var _))
                {
                    _logger.LogWarning("Resposta da API Ollama não contém o campo 'response' durante teste");
                    return false;
                }
                
                _logger.LogInformation("Conexão com Ollama testada com sucesso. Modelo {ModelName} está disponível e respondendo", modelName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao testar conexão com Ollama: {ErrorMessage}", ex.Message);
                return false;
            }
        }
        
        private async Task<string> RunCodeLlama(string prompt)
        {
            var startTime = DateTime.Now;
            _logger.LogInformation("Iniciando execução do modelo LLM");
            
            try
            {
                var modelName = _configuration.GetValue<string>("Ollama:ModelName", "codellama:latest");
                
                // Verificar se o Ollama está disponível
                if (!await TestOllamaConnection(modelName))
                {
                    _logger.LogError("Não foi possível conectar ao Ollama ou o modelo {ModelName} não está disponível", modelName);
                    return CreateFallbackAnalysisJson();
                }
                
                var maxPromptLength = _configuration.GetValue<int>("Ollama:MaxPromptLength", 8000);
                
                // Verificar se o prompt é muito grande
                if (prompt.Length > maxPromptLength)
                {
                    _logger.LogWarning("Prompt excede o tamanho máximo ({MaxLength}). Dividindo em partes...",
                        maxPromptLength);
                    
                    // Dividir o prompt em partes menores
                    var promptParts = SplitPromptIntoParts(prompt);
                    _logger.LogInformation("Prompt dividido em {PartCount} partes", promptParts.Count);
                    
                    // Processar cada parte sequencialmente e combinar os resultados
                    var results = new List<string>();
                    for (int i = 0; i < promptParts.Count; i++)
                    {
                        _logger.LogInformation("Processando parte {PartNumber}/{TotalParts}", i + 1, promptParts.Count);
                        
                        // Processar a parte atual e aguardar sua conclusão antes de prosseguir
                        var partResult = await ProcessPromptPart(promptParts[i], modelName);
                        
                        // Verificar se obtivemos um resultado válido
                        if (!string.IsNullOrEmpty(partResult))
                        {
                            _logger.LogInformation("Parte {PartNumber}/{TotalParts} processada com sucesso: {ResultLength} caracteres", 
                                i + 1, promptParts.Count, partResult.Length);
                            results.Add(partResult);
                        }
                        else
                        {
                            _logger.LogWarning("Parte {PartNumber}/{TotalParts} retornou resultado vazio", i + 1, promptParts.Count);
                        }
                    }
                    
                    // Verificar se temos resultados para combinar
                    if (results.Count == 0)
                    {
                        _logger.LogError("Nenhuma parte do prompt retornou resultado válido");
                        return CreateFallbackAnalysisJson();
                    }
                    
                    // Combinar os resultados
                    _logger.LogInformation("Combinando resultados de {ResultCount} partes", results.Count);
                    var combinedResult = CombineResults(results);
                    
                    if (string.IsNullOrEmpty(combinedResult))
                    {
                        _logger.LogError("Falha ao combinar resultados das partes do prompt");
                        return CreateFallbackAnalysisJson();
                    }
                    
                    var endTime = DateTime.Now;
                    _logger.LogInformation("Execução do modelo concluída em {Duration} segundos", 
                        (endTime - startTime).TotalSeconds);
                    
                    return combinedResult;
                }
                else
                {
                    _logger.LogInformation("Processando prompt completo de {PromptLength} caracteres", prompt.Length);
                    
                    // Processar o prompt completo e aguardar sua conclusão
                    var result = await ProcessPromptPart(prompt, modelName);
                    
                    if (string.IsNullOrEmpty(result))
                    {
                        _logger.LogError("Modelo retornou resultado vazio");
                        return CreateFallbackAnalysisJson();
                    }
                    
                    var endTime = DateTime.Now;
                    _logger.LogInformation("Execução do modelo concluída em {Duration} segundos", 
                        (endTime - startTime).TotalSeconds);
                    
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar o modelo LLM: {ErrorMessage}", ex.Message);
                return CreateFallbackAnalysisJson();
            }
        }
    }
}
