using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RefactorScore.Core.Entities;
using RefactorScore.Core.Interfaces;
using RefactorScore.Core.Specifications;
using RefactorScore.Application.Services.LlmResponses;

namespace RefactorScore.Application.Services
{
    public class CodeAnalyzerService : ICodeAnalyzerService
    {
        private readonly IGitRepository _gitRepository;
        private readonly ILLMService _llmService;
        private readonly IAnalysisRepository _analysisRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<CodeAnalyzerService> _logger;
        private readonly CodeAnalyzerOptions _options;
        private readonly JsonSerializerOptions _jsonOptions;

        public CodeAnalyzerService(
            IGitRepository gitRepository,
            ILLMService llmService,
            IAnalysisRepository analysisRepository,
            ICacheService cacheService,
            IOptions<CodeAnalyzerOptions> options,
            ILogger<CodeAnalyzerService> logger)
        {
            _gitRepository = gitRepository;
            _llmService = llmService;
            _analysisRepository = analysisRepository;
            _cacheService = cacheService;
            _options = options.Value;
            _logger = logger;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        /// <inheritdoc />
        public async Task<Result<IEnumerable<CommitInfo>>> GetRecentCommitsAsync()
        {
            try
            {
                _logger.LogInformation("Obtendo commits recentes");
                
                var commits = await _gitRepository.GetLastDayCommitsAsync();
                
                return Result<IEnumerable<CommitInfo>>.Success(commits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter commits recentes");
                return Result<IEnumerable<CommitInfo>>.Fail(ex);
            }
        }

        /// <inheritdoc />
        public async Task<Result<IEnumerable<CommitFileChange>>> GetCommitChangesAsync(string commitId)
        {
            try
            {
                _logger.LogInformation("Obtendo alterações do commit {CommitId}", commitId);
                
                // Verificar no cache primeiro
                var cacheKey = $"commit:changes:{commitId}";
                var cachedChanges = await _cacheService.GetAsync<IEnumerable<CommitFileChange>>(cacheKey);
                
                if (cachedChanges != null)
                {
                    _logger.LogInformation("Alterações do commit {CommitId} encontradas no cache", commitId);
                    return Result<IEnumerable<CommitFileChange>>.Success(cachedChanges);
                }
                
                var changes = await _gitRepository.GetCommitChangesAsync(commitId);
                
                // Armazenar no cache
                await _cacheService.SetAsync(cacheKey, changes, TimeSpan.FromHours(24));
                
                return Result<IEnumerable<CommitFileChange>>.Success(changes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter alterações do commit {CommitId}", commitId);
                return Result<IEnumerable<CommitFileChange>>.Fail(ex);
            }
        }

        /// <inheritdoc />
        public async Task<Result<CodeAnalysis>> AnalyzeCommitFileAsync(string commitId, string filePath)
        {
            try
            {
                _logger.LogInformation("Analisando arquivo {FilePath} do commit {CommitId}", filePath, commitId);
                
                // Verificar se já existe uma análise para este arquivo no commit
                var existingAnalysis = await _analysisRepository.GetAnalysisByCommitAndFileAsync(commitId, filePath);
                
                if (existingAnalysis != null)
                {
                    _logger.LogInformation("Análise existente encontrada para {FilePath} no commit {CommitId}", 
                        filePath, commitId);
                    return Result<CodeAnalysis>.Success(existingAnalysis);
                }
                
                // Verificar no cache
                var cacheKey = $"analysis:{commitId}:{filePath.Replace('/', '_')}";
                var cachedAnalysis = await _cacheService.GetAsync<CodeAnalysis>(cacheKey);
                
                if (cachedAnalysis != null)
                {
                    _logger.LogInformation("Análise encontrada no cache para {FilePath} no commit {CommitId}", 
                        filePath, commitId);
                    return Result<CodeAnalysis>.Success(cachedAnalysis);
                }
                
                // Obter informações do commit
                var commit = await _gitRepository.GetCommitByIdAsync(commitId);
                
                if (commit == null)
                {
                    _logger.LogWarning("Commit {CommitId} não encontrado", commitId);
                    return Result<CodeAnalysis>.Fail($"Commit {commitId} não encontrado");
                }
                
                // Obter conteúdo do arquivo
                var fileContent = await _gitRepository.GetFileContentAtRevisionAsync(commitId, filePath);
                
                if (string.IsNullOrEmpty(fileContent))
                {
                    _logger.LogWarning("Conteúdo do arquivo {FilePath} no commit {CommitId} não encontrado", 
                        filePath, commitId);
                    return Result<CodeAnalysis>.Fail($"Conteúdo do arquivo {filePath} não encontrado");
                }
                
                // Obter o diff do arquivo
                var fileDiff = await _gitRepository.GetFileDiffAsync(commitId, filePath);
                
                // Realizar análise com o LLM
                var analysisResult = await AnalyzeCodeWithLLMAsync(fileContent, fileDiff, filePath, commit);
                
                if (!analysisResult.IsSuccess)
                {
                    return Result<CodeAnalysis>.Fail(analysisResult.Errors);
                }
                
                var analysis = analysisResult.Data;
                
                // Salvar análise no cache
                await _cacheService.SetAsync(cacheKey, analysis, TimeSpan.FromHours(24));
                
                // Salvar análise no repositório
                await _analysisRepository.SaveAnalysisAsync(analysis);
                
                return Result<CodeAnalysis>.Success(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar arquivo {FilePath} do commit {CommitId}", filePath, commitId);
                return Result<CodeAnalysis>.Fail(ex);
            }
        }

        /// <inheritdoc />
        public async Task<Result<IEnumerable<CodeAnalysis>>> GetAnalysesForCommitAsync(string commitId)
        {
            try
            {
                _logger.LogInformation("Obtendo análises para o commit {CommitId}", commitId);
                
                var analyses = await _analysisRepository.GetAnalysesByCommitIdAsync(commitId);
                
                return Result<IEnumerable<CodeAnalysis>>.Success(analyses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter análises para o commit {CommitId}", commitId);
                return Result<IEnumerable<CodeAnalysis>>.Fail(ex);
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CodeAnalysis>> AnalyzeCommitAsync(string commitId)
        {
            _logger.LogInformation("Analisando commit completo {CommitId}", commitId);
            
            try
            {
                // Obter informações do commit
                var commit = await _gitRepository.GetCommitByIdAsync(commitId);
                if (commit == null)
                {
                    _logger.LogWarning("Commit {CommitId} não encontrado", commitId);
                    return new List<CodeAnalysis>();
                }
                
                // Obter alterações do commit
                var changesResult = await GetCommitChangesAsync(commitId);
                if (!changesResult.IsSuccess)
                {
                    _logger.LogWarning("Não foi possível obter alterações do commit {CommitId}", commitId);
                    return new List<CodeAnalysis>();
                }
                
                var filesToAnalyze = changesResult.Data
                    .Where(f => f.Status != FileChangeType.Deleted)
                    .Select(f => f.FilePath ?? f.Path)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
                
                var analyses = new List<CodeAnalysis>();
                
                // Analisar cada arquivo
                foreach (var filePath in filesToAnalyze)
                {
                    var analysisResult = await AnalyzeFileInCommitAsync(commitId, filePath);
                    if (analysisResult != null)
                    {
                        analyses.Add(analysisResult);
                    }
                }
                
                return analyses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar commit {CommitId}", commitId);
                return new List<CodeAnalysis>();
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CodeAnalysis>> AnalyzeLastDayCommitsAsync()
        {
            _logger.LogInformation("Analisando commits das últimas 24 horas");
            
            try
            {
                // Obter commits recentes
                var commitsResult = await GetRecentCommitsAsync();
                if (!commitsResult.IsSuccess)
                {
                    _logger.LogWarning("Não foi possível obter commits recentes");
                    return new List<CodeAnalysis>();
                }
                
                var analyses = new List<CodeAnalysis>();
                
                // Analisar cada commit
                foreach (var commit in commitsResult.Data)
                {
                    var commitAnalyses = await AnalyzeCommitAsync(commit.Id);
                    analyses.AddRange(commitAnalyses);
                }
                
                return analyses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar commits recentes");
                return new List<CodeAnalysis>();
            }
        }

        /// <inheritdoc />
        public async Task<CodeAnalysis> AnalyzeFileInCommitAsync(string commitId, string filePath)
        {
            _logger.LogInformation("Analisando arquivo {FilePath} no commit {CommitId}", filePath, commitId);
            
            try
            {
                var analysisResult = await AnalyzeCommitFileAsync(commitId, filePath);
                return analysisResult.IsSuccess ? analysisResult.Data : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar arquivo {FilePath} no commit {CommitId}", filePath, commitId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<CodeAnalysis> AggregatePartialAnalysesAsync(string commitId, IEnumerable<CodeAnalysis> partialAnalyses)
        {
            _logger.LogInformation("Agregando análises parciais para o commit {CommitId}", commitId);
            
            try
            {
                var analyses = partialAnalyses.ToList();
                if (!analyses.Any())
                {
                    _logger.LogWarning("Nenhuma análise parcial fornecida para o commit {CommitId}", commitId);
                    return null;
                }
                
                // Obter informações do commit
                var commit = await _gitRepository.GetCommitByIdAsync(commitId);
                if (commit == null)
                {
                    _logger.LogWarning("Commit {CommitId} não encontrado", commitId);
                    return null;
                }
                
                // Criar análise agregada
                var aggregatedAnalysis = new CodeAnalysis
                {
                    Id = Guid.NewGuid().ToString(),
                    CommitId = commitId,
                    FilePath = "agregado",
                    Author = commit.Author,
                    CommitDate = commit.CommitDate,
                    AnalysisDate = DateTime.UtcNow,
                    CleanCodeAnalysis = new CleanCodeAnalysis
                    {
                        VariableNaming = (int)Math.Round(analyses.Average(a => a.CleanCodeAnalysis.VariableNaming)),
                        FunctionSize = (int)Math.Round(analyses.Average(a => a.CleanCodeAnalysis.FunctionSize)),
                        CommentUsage = (int)Math.Round(analyses.Average(a => a.CleanCodeAnalysis.CommentUsage)),
                        MethodCohesion = (int)Math.Round(analyses.Average(a => a.CleanCodeAnalysis.MethodCohesion)),
                        DeadCodeAvoidance = (int)Math.Round(analyses.Average(a => a.CleanCodeAnalysis.DeadCodeAvoidance))
                    },
                    Justification = "Análise agregada de múltiplos arquivos"
                };
                
                // Calcular pontuação geral
                aggregatedAnalysis.OverallScore = (
                    aggregatedAnalysis.CleanCodeAnalysis.VariableNaming +
                    aggregatedAnalysis.CleanCodeAnalysis.FunctionSize +
                    aggregatedAnalysis.CleanCodeAnalysis.CommentUsage +
                    aggregatedAnalysis.CleanCodeAnalysis.MethodCohesion +
                    aggregatedAnalysis.CleanCodeAnalysis.DeadCodeAvoidance
                ) / 5.0;
                
                // Salvar análise agregada
                await _analysisRepository.SaveAnalysisAsync(aggregatedAnalysis);
                
                return aggregatedAnalysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao agregar análises parciais para o commit {CommitId}", commitId);
                return null;
            }
        }

        /// <summary>
        /// Realiza a análise de código usando o serviço LLM
        /// </summary>
        private async Task<Result<CodeAnalysis>> AnalyzeCodeWithLLMAsync(
            string codeContent, 
            string codeDiff, 
            string filePath, 
            CommitInfo commit)
        {
            try
            {
                _logger.LogInformation("Analisando código com LLM para o arquivo {FilePath}", filePath);
                
                // Limitar o tamanho do código para evitar problemas com o contexto do modelo
                if (codeContent.Length > _options.MaxCodeLength)
                {
                    _logger.LogWarning("Código muito grande, será truncado: {FilePath}", filePath);
                    codeContent = codeContent.Substring(0, _options.MaxCodeLength) + 
                                 "\n\n// ... código truncado devido ao tamanho ...";
                }
                
                if (codeDiff.Length > _options.MaxDiffLength)
                {
                    _logger.LogWarning("Diff muito grande, será truncado: {FilePath}", filePath);
                    codeDiff = codeDiff.Substring(0, _options.MaxDiffLength) + 
                              "\n\n// ... diff truncado devido ao tamanho ...";
                }
                
                // Construir o prompt para o modelo
                var prompt = BuildAnalysisPrompt(codeContent, codeDiff, filePath, commit);
                
                // Processar com o LLM
                var responseText = await _llmService.ProcessPromptAsync(prompt, _options.ModelName);
                
                // Extrair JSON da resposta
                var jsonContent = ExtractJsonFromText(responseText);
                
                if (string.IsNullOrEmpty(jsonContent))
                {
                    _logger.LogWarning("Não foi possível extrair JSON da resposta");
                    return Result<CodeAnalysis>.Fail("Não foi possível extrair JSON da resposta");
                }
                
                try
                {
                    // Converter JSON para objeto de análise
                    var llmResponse = JsonSerializer.Deserialize<LlmAnalysisResponse>(jsonContent, _jsonOptions);
                    
                    if (llmResponse == null)
                    {
                        _logger.LogWarning("Resposta nula após deserialização do JSON");
                        return Result<CodeAnalysis>.Fail("Resposta nula após deserialização do JSON");
                    }
                    
                    // Converter a resposta do LLM para o modelo de análise
                    var analysis = new CodeAnalysis
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        CommitId = commit.Id,
                        FilePath = filePath,
                        Author = commit.Author,
                        CommitDate = commit.Date,
                        AnalysisDate = DateTime.UtcNow,
                        CleanCodeAnalysis = new CleanCodeAnalysis
                        {
                            VariableNaming = (int)Math.Round(llmResponse.AnaliseCleanCode.NomeclaturaVariaveis),
                            NamingConventions = new ScoreItem
                            {
                                Score = (int)Math.Round(llmResponse.AnaliseCleanCode.NomeclaturaVariaveis),
                                Justification = ""
                            },
                            FunctionSize = (int)Math.Round(llmResponse.AnaliseCleanCode.TamanhoFuncoes),
                            CommentUsage = (int)Math.Round(llmResponse.AnaliseCleanCode.UsoDeComentariosRelevantes),
                            MeaningfulComments = new ScoreItem
                            {
                                Score = (int)Math.Round(llmResponse.AnaliseCleanCode.UsoDeComentariosRelevantes),
                                Justification = ""
                            },
                            MethodCohesion = (int)Math.Round(llmResponse.AnaliseCleanCode.CohesaoDosMetodos),
                            DeadCodeAvoidance = (int)Math.Round(llmResponse.AnaliseCleanCode.EvitacaoDeCodigoMorto)
                        },
                        OverallScore = llmResponse.NotaGeral,
                        Justification = llmResponse.Justificativa
                    };
                    
                    return Result<CodeAnalysis>.Success(analysis);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Erro ao deserializar JSON: {Message}", ex.Message);
                    return Result<CodeAnalysis>.Fail($"Erro ao deserializar JSON: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar código com LLM");
                return Result<CodeAnalysis>.Fail(ex);
            }
        }

        /// <summary>
        /// Constrói o prompt para análise de código
        /// </summary>
        private string BuildAnalysisPrompt(
            string codeContent, 
            string codeDiff, 
            string filePath, 
            CommitInfo commit)
        {
            var fileExtension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            
            var prompt = $@"Analise o seguinte código-fonte e forneça uma avaliação detalhada de acordo com os princípios de Clean Code.

INFORMAÇÕES DO COMMIT:
- ID do commit: {commit.Id}
- Autor: {commit.Author}
- Mensagem: {commit.Message}
- Data: {commit.Date}
- Arquivo: {filePath}

CÓDIGO-FONTE:
```{fileExtension}
{codeContent}
```

ALTERAÇÕES (DIFF):
```diff
{codeDiff}
```

Analise o código com base nos seguintes critérios de Clean Code:
1. Nomenclatura de variáveis e métodos (0-10)
2. Tamanho adequado das funções (0-10)
3. Uso de comentários relevantes (0-10)
4. Coesão dos métodos (0-10)
5. Evitação de código morto ou redundante (0-10)

Sua resposta deve ser APENAS um JSON válido com a estrutura exata:
{{
  ""commit_id"": ""{commit.Id}"",
  ""autor"": ""{commit.Author}"",
  ""analise_clean_code"": {{
    ""nomeclatura_variaveis"": [valor entre 0-10],
    ""tamanho_funcoes"": [valor entre 0-10],
    ""uso_de_comentarios_relevantes"": [valor entre 0-10],
    ""cohesao_dos_metodos"": [valor entre 0-10],
    ""evitacao_de_codigo_morto"": [valor entre 0-10]
  }},
  ""nota_geral"": [média das notas acima],
  ""justificativa"": ""[Explicação objetiva das notas atribuídas]""
}}

Não inclua texto adicional antes ou depois do JSON. Responda apenas com o JSON solicitado.";

            return prompt;
        }

        /// <summary>
        /// Extrai o conteúdo JSON de uma resposta de texto
        /// </summary>
        private string ExtractJsonFromText(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    _logger.LogWarning("Texto vazio recebido para extração de JSON");
                    return string.Empty;
                }
                
                // Tentar extrair o JSON delimitado por ```json e ```
                var jsonCodeBlockPattern = @"```json\s*([\s\S]*?)\s*```";
                var jsonCodeBlockMatch = Regex.Match(text, jsonCodeBlockPattern);
                
                if (jsonCodeBlockMatch.Success)
                {
                    return jsonCodeBlockMatch.Groups[1].Value.Trim();
                }
                
                // Tentar extrair o JSON delimitado por ``` e ```
                var codeBlockPattern = @"```\s*([\s\S]*?)\s*```";
                var codeBlockMatch = Regex.Match(text, codeBlockPattern);
                
                if (codeBlockMatch.Success)
                {
                    return codeBlockMatch.Groups[1].Value.Trim();
                }
                
                // Tentar encontrar um objeto JSON 
                var jsonPattern = @"(\{[\s\S]*\})";
                var jsonMatch = Regex.Match(text, jsonPattern);
                
                if (jsonMatch.Success)
                {
                    return jsonMatch.Groups[1].Value.Trim();
                }
                
                _logger.LogWarning("Não foi possível encontrar um JSON válido na resposta");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair JSON do texto");
                return string.Empty;
            }
        }
    }
} 