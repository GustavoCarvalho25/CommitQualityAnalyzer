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
using System.Text;
using System.IO;

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
                WriteIndented = true,
                AllowTrailingCommas = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
        }

        /// <inheritdoc />
        public async Task<Result<IEnumerable<CommitInfo>>> GetRecentCommitsAsync()
        {
            try
            {
                _logger.LogInformation("Getting recent commits");
                
                var commits = await _gitRepository.GetLastDayCommitsAsync();
                
                return Result<IEnumerable<CommitInfo>>.Success(commits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent commits");
                return Result<IEnumerable<CommitInfo>>.Fail(ex);
            }
        }

        /// <inheritdoc />
        public async Task<Result<IEnumerable<CommitFileChange>>> GetCommitChangesAsync(string commitId)
        {
            try
            {
                _logger.LogInformation("Getting changes for commit {CommitId}", commitId);
                
                // Verificar no cache primeiro
                var cacheKey = $"commit:changes:{commitId}";
                var cachedChanges = await _cacheService.GetAsync<IEnumerable<CommitFileChange>>(cacheKey);
                
                if (cachedChanges != null)
                {
                    _logger.LogInformation("Changes for commit {CommitId} found in cache", commitId);
                    return Result<IEnumerable<CommitFileChange>>.Success(cachedChanges);
                }
                
                var changes = await _gitRepository.GetCommitChangesAsync(commitId);
                
                // Armazenar no cache
                await _cacheService.SetAsync(cacheKey, changes, TimeSpan.FromHours(24));
                
                return Result<IEnumerable<CommitFileChange>>.Success(changes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting changes for commit {CommitId}", commitId);
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
                _logger.LogInformation("[ANALYZER_START] Beginning code analysis with LLM for file {FilePath}", filePath);
                
                // Verificar se o código precisa ser particionado
                var codeLength = codeContent.Length;
                var diffLength = codeDiff?.Length ?? 0;
                
                _logger.LogInformation("[ANALYZER_SIZE] File stats: Code length: {CodeLength} chars, Diff length: {DiffLength} chars", 
                    codeLength, diffLength);
                
                if (codeContent.Length > _options.MaxCodeLength)
                {
                    _logger.LogInformation("[ANALYZER_PARTITION] Code too large ({Length} chars), will be partitioned: {FilePath}", 
                        codeContent.Length, filePath);
                    
                    return await AnalyzePartitionedCodeAsync(codeContent, codeDiff, filePath, commit);
                }
                
                // Código original para arquivos menores que não precisam ser particionados
                if (codeDiff.Length > _options.MaxDiffLength)
                {
                    _logger.LogWarning("[ANALYZER_TRUNCATE] Diff too large ({Length} chars), will be truncated: {FilePath}", 
                        codeDiff.Length, filePath);
                    codeDiff = codeDiff.Substring(0, _options.MaxDiffLength) + 
                              "\n\n// ... diff truncado devido ao tamanho ...";
                }
                
                // Construir o prompt para o modelo
                _logger.LogInformation("[ANALYZER_PROMPT] Building prompt for LLM...");
                var prompt = BuildAnalysisPrompt(codeContent, codeDiff, filePath, commit);
                _logger.LogInformation("[ANALYZER_PROMPT] Prompt built, length: {PromptLength} chars", prompt.Length);
                
                // Definir o número máximo de tentativas
                const int maxRetries = 3;
                int attempt = 0;
                Exception lastException = null;
                
                // Loop de retry para tentativas de processamento com o LLM
                while (attempt < maxRetries)
                {
                    attempt++;
                    try
                    {
                        _logger.LogInformation("[ANALYZER_LLM_CALL] Sending prompt to LLM service using model {ModelName}... Attempt {Attempt}/{MaxRetries}", 
                            _options.ModelName, attempt, maxRetries);
                        var startTime = DateTime.UtcNow;
                        
                        var responseText = await _llmService.ProcessPromptAsync(prompt, _options.ModelName);
                        
                        var duration = DateTime.UtcNow - startTime;
                        _logger.LogInformation("[ANALYZER_LLM_RESPONSE] Received response from LLM after {Duration}ms. " +
                            "Response length: {ResponseLength} chars", 
                            duration.TotalMilliseconds, responseText?.Length ?? 0);
                        
                        if (string.IsNullOrEmpty(responseText))
                        {
                            _logger.LogWarning("[ANALYZER_EMPTY_RESPONSE] Empty response from LLM on attempt {Attempt}", attempt);
                            continue; // Tenta novamente se a resposta for vazia
                        }
                        
                        // Extrair JSON da resposta
                        _logger.LogInformation("[ANALYZER_EXTRACT_JSON] Extracting JSON from LLM response...");
                        var jsonContent = ExtractJsonFromText(responseText);
                        
                        if (string.IsNullOrEmpty(jsonContent))
                        {
                            _logger.LogWarning("[ANALYZER_ERROR] Could not extract JSON from response on attempt {Attempt}", attempt);
                            
                            if (attempt == maxRetries)
                                return Result<CodeAnalysis>.Fail("Não foi possível extrair JSON da resposta");
                                
                            // Modificar o prompt para a próxima tentativa
                            prompt = BuildSimplifiedPrompt(codeContent, filePath, commit);
                            continue;
                        }
                        
                        _logger.LogInformation("[ANALYZER_JSON] Successfully extracted JSON, length: {JsonLength} chars", 
                            jsonContent.Length);
                        
                        // Converter JSON para objeto de análise - com tratamento de erros
                        return await DeserializeAndCreateAnalysis(jsonContent, commit, filePath, attempt, maxRetries);
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.LogError(ex, "[ANALYZER_EXCEPTION] Error on attempt {Attempt}: {ErrorMessage}", 
                            attempt, ex.Message);
                        
                        // Modificar o prompt para a próxima tentativa se necessário
                        if (attempt < maxRetries)
                        {
                            prompt = BuildSimplifiedPrompt(codeContent, filePath, commit);
                            await Task.Delay(1000 * attempt); // Backoff exponencial simples
                        }
                    }
                }
                
                // Se chegou aqui, todas as tentativas falharam
                return Result<CodeAnalysis>.Fail(lastException ?? 
                    new Exception("Falha após múltiplas tentativas de análise"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ANALYZER_EXCEPTION] Error analyzing code with LLM");
                return Result<CodeAnalysis>.Fail(ex);
            }
        }
        
        /// <summary>
        /// Tenta deserializar o JSON e criar um objeto de análise, com tratamento de erros
        /// </summary>
        private async Task<Result<CodeAnalysis>> DeserializeAndCreateAnalysis(
            string jsonContent, 
            CommitInfo commit, 
            string filePath,
            int currentAttempt,
            int maxRetries)
        {
            try
            {
                // Primeiro, tenta deserializar normalmente
                _logger.LogInformation("[ANALYZER_DESERIALIZE] Deserializing JSON to analysis object...");
                var llmResponse = JsonSerializer.Deserialize<LlmAnalysisResponse>(jsonContent, _jsonOptions);
                
                if (llmResponse == null)
                {
                    _logger.LogWarning("[ANALYZER_ERROR] Null response after JSON deserialization");
                    return Result<CodeAnalysis>.Fail("Resposta nula após deserialização do JSON");
                }
                
                // Converter a resposta do LLM para o modelo de análise
                _logger.LogInformation("[ANALYZER_CONVERT] Converting LLM response to analysis model. " +
                    "Overall score: {Score}", llmResponse.NotaGeral);
                
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
                            Justification = EnsureSafeString(llmResponse.AnaliseCleanCode.JustificativaNomenclatura, 150)
                        },
                        FunctionSize = (int)Math.Round(llmResponse.AnaliseCleanCode.TamanhoFuncoes),
                        CommentUsage = (int)Math.Round(llmResponse.AnaliseCleanCode.UsoDeComentariosRelevantes),
                        MeaningfulComments = new ScoreItem
                        {
                            Score = (int)Math.Round(llmResponse.AnaliseCleanCode.UsoDeComentariosRelevantes),
                            Justification = EnsureSafeString(llmResponse.AnaliseCleanCode.JustificativaComentarios, 150)
                        },
                        MethodCohesion = (int)Math.Round(llmResponse.AnaliseCleanCode.CohesaoDosMetodos),
                        DeadCodeAvoidance = (int)Math.Round(llmResponse.AnaliseCleanCode.EvitacaoDeCodigoMorto)
                    },
                    OverallScore = llmResponse.NotaGeral,
                    Justification = EnsureSafeString(llmResponse.Justificativa, 500)
                };
                
                // Adicionar justificativas para os outros critérios que não têm objetos ScoreItem
                analysis.CleanCodeAnalysis.AdditionalCriteria["FunctionSizeJustification"] = 
                    EnsureSafeString(llmResponse.AnaliseCleanCode.JustificativaFuncoes, 150);
                analysis.CleanCodeAnalysis.AdditionalCriteria["MethodCohesionJustification"] = 
                    EnsureSafeString(llmResponse.AnaliseCleanCode.JustificativaCohesao, 150);
                analysis.CleanCodeAnalysis.AdditionalCriteria["DeadCodeAvoidanceJustification"] = 
                    EnsureSafeString(llmResponse.AnaliseCleanCode.JustificativaCodigoMorto, 150);
                
                _logger.LogInformation("[ANALYZER_SUCCESS] Successfully analyzed file {FilePath}", filePath);
                return Result<CodeAnalysis>.Success(analysis);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "[ANALYZER_JSON_ERROR] Error deserializing JSON: {Message}", jsonEx.Message);
                
                // Se ainda tiver tentativas, retorna falha para tentar novamente
                if (currentAttempt < maxRetries)
                {
                    return Result<CodeAnalysis>.Fail($"Erro ao deserializar JSON: {jsonEx.Message}");
                }
                
                // Última tentativa - tenta extrair o que puder do JSON com método alternativo
                try
                {
                    _logger.LogWarning("[ANALYZER_FALLBACK] Trying manual JSON parsing as last resort");
                    var analysis = CreateFallbackAnalysis(jsonContent, commit, filePath);
                    return Result<CodeAnalysis>.Success(analysis);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "[ANALYZER_FALLBACK_ERROR] Error creating fallback analysis");
                    return Result<CodeAnalysis>.Fail($"Erro ao criar análise alternativa: {fallbackEx.Message}");
                }
            }
        }
        
        /// <summary>
        /// Cria uma versão simplificada do prompt para tentar novamente em caso de falha
        /// </summary>
        private string BuildSimplifiedPrompt(string codeContent, string filePath, CommitInfo commit)
        {
            var fileExtension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            
            return $@"Analise o seguinte código-fonte e forneça uma avaliação detalhada de acordo com os princípios de Clean Code.

INFORMAÇÕES DO COMMIT:
- ID do commit: {commit.Id}
- Autor: {commit.Author}
- Arquivo: {filePath}

CÓDIGO-FONTE:
```{fileExtension}
{codeContent.Substring(0, Math.Min(codeContent.Length, 3000))}
```

Forneça uma análise com pontuações de 0-10 para:
1. Nomenclatura de variáveis
2. Tamanho de funções
3. Uso de comentários
4. Coesão de métodos
5. Evitação de código morto

Responda APENAS com um JSON simples usando a seguinte estrutura exata e sem texto adicional:

{{
  ""analise_clean_code"": {{
    ""nomeclatura_variaveis"": 7,
    ""tamanho_funcoes"": 8,
    ""uso_de_comentarios_relevantes"": 6,
    ""cohesao_dos_metodos"": 7,
    ""evitacao_de_codigo_morto"": 9
  }},
  ""nota_geral"": 7.4,
  ""justificativa"": ""Breve justificativa da análise""
}}";
        }
        
        /// <summary>
        /// Cria uma análise de fallback quando todas as tentativas falham
        /// </summary>
        private CodeAnalysis CreateFallbackAnalysis(string jsonContent, CommitInfo commit, string filePath)
        {
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
                    VariableNaming = 5,
                    NamingConventions = new ScoreItem { Score = 5, Justification = "Análise parcial - falha no processamento do JSON" },
                    FunctionSize = 5,
                    CommentUsage = 5,
                    MeaningfulComments = new ScoreItem { Score = 5, Justification = "Análise parcial - falha no processamento do JSON" },
                    MethodCohesion = 5,
                    DeadCodeAvoidance = 5
                },
                OverallScore = 5.0,
                Justification = "Análise falhou ao processar resposta do LLM. Esta é uma pontuação neutra de fallback."
            };
            
            // Tenta extrair valores usando regex do JSON parcial
            try
            {
                // Procurar pontuações específicas com regex
                var overallMatch = Regex.Match(jsonContent, @"""nota_geral""[\s:]+([0-9.]+)");
                if (overallMatch.Success && overallMatch.Groups.Count > 1)
                {
                    if (double.TryParse(overallMatch.Groups[1].Value, out var score))
                    {
                        analysis.OverallScore = score;
                    }
                }
                
                // Extrair uma justificativa
                var justificationMatch = Regex.Match(jsonContent, @"""justificativa""[\s:]+""([^""]+)""");
                if (justificationMatch.Success && justificationMatch.Groups.Count > 1)
                {
                    analysis.Justification = "Recuperado parcialmente: " + EnsureSafeString(justificationMatch.Groups[1].Value, 150);
                }
                
                // Extrair critérios específicos
                ExtractScoreAndJustification(jsonContent, "nomeclatura_variaveis", "justificativa_nomenclatura", 
                    out int namingScore, out string namingJustification);
                ExtractScoreAndJustification(jsonContent, "tamanho_funcoes", "justificativa_funcoes", 
                    out int sizeScore, out string sizeJustification);
                ExtractScoreAndJustification(jsonContent, "uso_de_comentarios_relevantes", "justificativa_comentarios", 
                    out int commentScore, out string commentJustification);
                ExtractScoreAndJustification(jsonContent, "cohesao_dos_metodos", "justificativa_cohesao", 
                    out int cohesionScore, out string cohesionJustification);
                ExtractScoreAndJustification(jsonContent, "evitacao_de_codigo_morto", "justificativa_codigo_morto", 
                    out int deadCodeScore, out string deadCodeJustification);
                
                // Usar os valores extraídos
                if (namingScore > 0) analysis.CleanCodeAnalysis.VariableNaming = namingScore;
                if (sizeScore > 0) analysis.CleanCodeAnalysis.FunctionSize = sizeScore;
                if (commentScore > 0) analysis.CleanCodeAnalysis.CommentUsage = commentScore;
                if (cohesionScore > 0) analysis.CleanCodeAnalysis.MethodCohesion = cohesionScore;
                if (deadCodeScore > 0) analysis.CleanCodeAnalysis.DeadCodeAvoidance = deadCodeScore;
                
                if (!string.IsNullOrEmpty(namingJustification))
                {
                    analysis.CleanCodeAnalysis.NamingConventions.Justification = 
                        "Recuperado parcialmente: " + namingJustification;
                }
                
                if (!string.IsNullOrEmpty(commentJustification))
                {
                    analysis.CleanCodeAnalysis.MeaningfulComments.Justification = 
                        "Recuperado parcialmente: " + commentJustification;
                }
                
                analysis.CleanCodeAnalysis.AdditionalCriteria["FunctionSizeJustification"] = 
                    string.IsNullOrEmpty(sizeJustification) 
                        ? "Análise parcial - falha no processamento" 
                        : "Recuperado parcialmente: " + sizeJustification;
                        
                analysis.CleanCodeAnalysis.AdditionalCriteria["MethodCohesionJustification"] = 
                    string.IsNullOrEmpty(cohesionJustification) 
                        ? "Análise parcial - falha no processamento" 
                        : "Recuperado parcialmente: " + cohesionJustification;
                        
                analysis.CleanCodeAnalysis.AdditionalCriteria["DeadCodeAvoidanceJustification"] = 
                    string.IsNullOrEmpty(deadCodeJustification) 
                        ? "Análise parcial - falha no processamento" 
                        : "Recuperado parcialmente: " + deadCodeJustification;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ANALYZER_REGEX_ERROR] Error extracting values with regex from partial JSON");
            }
            
            return analysis;
        }
        
        /// <summary>
        /// Extrai uma pontuação e justificativa de um JSON parcial usando regex
        /// </summary>
        private void ExtractScoreAndJustification(
            string json, 
            string scoreName, 
            string justificationName, 
            out int score, 
            out string justification)
        {
            score = 0;
            justification = string.Empty;
            
            var scoreMatch = Regex.Match(json, $@"""{scoreName}""[\s:]+([0-9]+)");
            if (scoreMatch.Success && scoreMatch.Groups.Count > 1)
            {
                if (int.TryParse(scoreMatch.Groups[1].Value, out int parsedScore))
                {
                    score = parsedScore;
                }
            }
            
            var justMatch = Regex.Match(json, $@"""{justificationName}""[\s:]+""([^""]+)""");
            if (justMatch.Success && justMatch.Groups.Count > 1)
            {
                justification = EnsureSafeString(justMatch.Groups[1].Value, 150);
            }
        }

        /// <summary>
        /// Processa um arquivo de código muito grande dividindo-o em partes
        /// </summary>
        private async Task<Result<CodeAnalysis>> AnalyzePartitionedCodeAsync(
            string codeContent, 
            string codeDiff, 
            string filePath, 
            CommitInfo commit)
        {
            try
            {
                _logger.LogInformation("[PARTITION_START] Starting partitioned analysis for large file {FilePath}", filePath);
                
                // Dividir o código em chunks para análise separada
                var chunks = PartitionCode(codeContent, filePath);
                _logger.LogInformation("[PARTITION_CHUNKS] Divided file into {ChunkCount} chunks", chunks.Count);
                
                // Lista para armazenar os resultados de cada chunk
                var chunkResults = new List<CodeAnalysis>();
                
                // Processar cada chunk
                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}] Processing chunk {ChunkIndex}/{ChunkCount}, size: {ChunkSize} chars", 
                        i+1, i+1, chunks.Count, chunk.Content.Length);
                    
                    try
                    {
                        // Construir o prompt para este chunk
                        var chunkPrompt = BuildChunkAnalysisPrompt(
                            chunk.Content, 
                            null, // Não incluímos diff para chunks
                            filePath, 
                            commit,
                            chunk.Description);
                            
                        // Processar com o LLM, com retry
                        const int maxRetries = 2;  // Menos tentativas para chunks
                        for (int attempt = 1; attempt <= maxRetries; attempt++)
                        {
                            try
                            {
                                _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_LLM_CALL] Sending prompt to LLM... Attempt {Attempt}/{MaxRetries}", 
                                    i+1, attempt, maxRetries);
                                
                                var startTime = DateTime.UtcNow;
                                var responseText = await _llmService.ProcessPromptAsync(chunkPrompt, _options.ModelName);
                                var duration = DateTime.UtcNow - startTime;
                                
                                _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_LLM_RESPONSE] Received response after {Duration}ms. Length: {ResponseLength} chars", 
                                    i+1, duration.TotalMilliseconds, responseText?.Length ?? 0);
                                
                                if (string.IsNullOrEmpty(responseText))
                                {
                                    _logger.LogWarning("[PARTITION_CHUNK_{ChunkIndex}_EMPTY] Empty response from LLM", i+1);
                                    if (attempt < maxRetries)
                                        continue;
                                    break;
                                }
                                
                                _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_JSON] Extracting JSON from response", i+1);
                                var jsonContent = ExtractJsonFromText(responseText);
                                if (string.IsNullOrEmpty(jsonContent))
                                {
                                    _logger.LogWarning("[PARTITION_CHUNK_{ChunkIndex}_ERROR] Could not extract JSON from response", i+1);
                                    if (attempt < maxRetries)
                                        continue;
                                    break;
                                }
                                
                                try
                                {
                                    _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_DESERIALIZE] Deserializing JSON", i+1);
                                    var llmResponse = JsonSerializer.Deserialize<LlmAnalysisResponse>(jsonContent, _jsonOptions);
                                    if (llmResponse == null)
                                    {
                                        _logger.LogWarning("[PARTITION_CHUNK_{ChunkIndex}_ERROR] Null response after JSON deserialization", i+1);
                                        if (attempt < maxRetries)
                                            continue;
                                        break;
                                    }
                                    
                                    _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_CONVERT] Converting response to analysis. Score: {Score}", 
                                        i+1, llmResponse.NotaGeral);
                                    
                                    var chunkAnalysis = new CodeAnalysis
                                    {
                                        Id = Guid.NewGuid().ToString("N"),
                                        CommitId = commit.Id,
                                        FilePath = $"{filePath}#chunk{chunk.Index}",
                                        Author = commit.Author,
                                        CommitDate = commit.Date,
                                        AnalysisDate = DateTime.UtcNow,
                                        CleanCodeAnalysis = new CleanCodeAnalysis
                                        {
                                            VariableNaming = (int)Math.Round(llmResponse.AnaliseCleanCode.NomeclaturaVariaveis),
                                            NamingConventions = new ScoreItem
                                            {
                                                Score = (int)Math.Round(llmResponse.AnaliseCleanCode.NomeclaturaVariaveis),
                                                Justification = EnsureSafeString(llmResponse.AnaliseCleanCode.JustificativaNomenclatura, 150)
                                            },
                                            FunctionSize = (int)Math.Round(llmResponse.AnaliseCleanCode.TamanhoFuncoes),
                                            CommentUsage = (int)Math.Round(llmResponse.AnaliseCleanCode.UsoDeComentariosRelevantes),
                                            MeaningfulComments = new ScoreItem
                                            {
                                                Score = (int)Math.Round(llmResponse.AnaliseCleanCode.UsoDeComentariosRelevantes),
                                                Justification = EnsureSafeString(llmResponse.AnaliseCleanCode.JustificativaComentarios, 150)
                                            },
                                            MethodCohesion = (int)Math.Round(llmResponse.AnaliseCleanCode.CohesaoDosMetodos),
                                            DeadCodeAvoidance = (int)Math.Round(llmResponse.AnaliseCleanCode.EvitacaoDeCodigoMorto)
                                        },
                                        OverallScore = llmResponse.NotaGeral,
                                        Justification = EnsureSafeString(llmResponse.Justificativa, 500)
                                    };
                                    
                                    // Adicionar justificativas para os outros critérios que não têm objetos ScoreItem
                                    chunkAnalysis.CleanCodeAnalysis.AdditionalCriteria["FunctionSizeJustification"] = 
                                        EnsureSafeString(llmResponse.AnaliseCleanCode.JustificativaFuncoes, 150);
                                    chunkAnalysis.CleanCodeAnalysis.AdditionalCriteria["MethodCohesionJustification"] = 
                                        EnsureSafeString(llmResponse.AnaliseCleanCode.JustificativaCohesao, 150);
                                    chunkAnalysis.CleanCodeAnalysis.AdditionalCriteria["DeadCodeAvoidanceJustification"] = 
                                        EnsureSafeString(llmResponse.AnaliseCleanCode.JustificativaCodigoMorto, 150);
                                    
                                    chunkResults.Add(chunkAnalysis);
                                    _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_SUCCESS] Successfully analyzed chunk", i+1);
                                    
                                    // Sair do loop de retry se tiver sucesso
                                    break;
                                }
                                catch (JsonException jsonEx)
                                {
                                    _logger.LogError(jsonEx, "[PARTITION_CHUNK_{ChunkIndex}_JSON_ERROR] JSON deserialization error: {Message}", 
                                        i+1, jsonEx.Message);
                                    
                                    if (attempt == maxRetries)
                                    {
                                        // Na última tentativa, tentar extrair o que for possível usando regex
                                        try
                                        {
                                            var fallbackAnalysis = CreateFallbackAnalysis(jsonContent, commit, $"{filePath}#chunk{chunk.Index}");
                                            chunkResults.Add(fallbackAnalysis);
                                            _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_FALLBACK] Created fallback analysis", i+1);
                                        }
                                        catch (Exception fallbackEx)
                                        {
                                            _logger.LogError(fallbackEx, "[PARTITION_CHUNK_{ChunkIndex}_FALLBACK_ERROR] Error creating fallback", i+1);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[PARTITION_CHUNK_{ChunkIndex}_ERROR] Error processing chunk: {Message}", 
                                    i+1, ex.Message);
                                
                                if (attempt == maxRetries)
                                {
                                    // Se todas as tentativas falharem, criar um resultado neutro para este chunk
                                    var neutralAnalysis = new CodeAnalysis
                                    {
                                        Id = Guid.NewGuid().ToString("N"),
                                        CommitId = commit.Id,
                                        FilePath = $"{filePath}#chunk{chunk.Index}",
                                        Author = commit.Author,
                                        CommitDate = commit.Date,
                                        AnalysisDate = DateTime.UtcNow,
                                        CleanCodeAnalysis = new CleanCodeAnalysis
                                        {
                                            VariableNaming = 5,
                                            FunctionSize = 5,
                                            CommentUsage = 5,
                                            MethodCohesion = 5,
                                            DeadCodeAvoidance = 5,
                                            NamingConventions = new ScoreItem { Score = 5, Justification = "Análise neutra devido a falha no processamento" },
                                            MeaningfulComments = new ScoreItem { Score = 5, Justification = "Análise neutra devido a falha no processamento" }
                                        },
                                        OverallScore = 5,
                                        Justification = $"Análise neutra: falha ao processar o chunk #{chunk.Index} do arquivo."
                                    };
                                    chunkResults.Add(neutralAnalysis);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[PARTITION_CHUNK_{ChunkIndex}_EXCEPTION] Error processing chunk analysis", i+1);
                    }
                }
                
                if (chunkResults.Count == 0)
                {
                    _logger.LogWarning("[PARTITION_EMPTY] No chunks were successfully analyzed");
                    return Result<CodeAnalysis>.Fail("Nenhum dos chunks foi analisado com sucesso");
                }
                
                // Agregar os resultados dos chunks
                _logger.LogInformation("[PARTITION_AGGREGATE] Aggregating results from {ChunkCount} chunks", chunkResults.Count);
                var aggregatedAnalysis = AggregateChunkResults(chunkResults, commit, filePath);
                
                _logger.LogInformation("[PARTITION_COMPLETE] Completed partitioned analysis for {FilePath}", filePath);
                return Result<CodeAnalysis>.Success(aggregatedAnalysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PARTITION_ERROR] Error in partitioned analysis: {Message}", ex.Message);
                return Result<CodeAnalysis>.Fail(ex);
            }
        }

        /// <summary>
        /// Constrói o prompt para análise de um chunk de código
        /// </summary>
        private string BuildChunkAnalysisPrompt(
            string chunkContent, 
            string codeDiff, 
            string filePath, 
            CommitInfo commit,
            string chunkDescription)
        {
            var fileExtension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            
            var prompt = $@"Analise o seguinte trecho de código-fonte e forneça uma avaliação detalhada de acordo com os princípios de Clean Code.

INFORMAÇÕES DO COMMIT:
- ID do commit: {commit.Id}
- Autor: {commit.Author}
- Mensagem: {commit.Message}
- Data: {commit.Date}
- Arquivo: {filePath}
- Parte do código: {chunkDescription}

CÓDIGO-FONTE (TRECHO):
```{fileExtension}
{chunkContent}
```

OBSERVAÇÃO: Este é apenas um trecho do arquivo completo. Concentre sua análise apenas neste trecho.

Analise o código com base nos seguintes critérios de Clean Code:
1. Nomenclatura de variáveis e métodos (0-10)
2. Tamanho adequado das funções (0-10)
3. Uso de comentários relevantes (0-10)
4. Coesão dos métodos (0-10)
5. Evitação de código morto ou redundante (0-10)

Sua resposta deve ser APENAS um JSON válido com a estrutura exata mostrada abaixo.
IMPORTANTE: 
- Todas as pontuações devem ser números inteiros (não strings)
- Todas as justificativas devem ter no máximo 150 caracteres
- Use apenas caracteres ASCII básicos nas justificativas (sem caracteres especiais ou Unicode)
- EVITE colocar vírgulas após o último item de cada objeto

{{
  ""commit_id"": ""{commit.Id}"",
  ""autor"": ""{commit.Author}"",
  ""analise_clean_code"": {{
    ""nomeclatura_variaveis"": 7,
    ""justificativa_nomenclatura"": ""Curta explicação para a nota (max 150 caracteres)"",
    
    ""tamanho_funcoes"": 8,
    ""justificativa_funcoes"": ""Curta explicação para a nota (max 150 caracteres)"",
    
    ""uso_de_comentarios_relevantes"": 6,
    ""justificativa_comentarios"": ""Curta explicação para a nota (max 150 caracteres)"",
    
    ""cohesao_dos_metodos"": 7,
    ""justificativa_cohesao"": ""Curta explicação para a nota (max 150 caracteres)"",
    
    ""evitacao_de_codigo_morto"": 9,
    ""justificativa_codigo_morto"": ""Curta explicação para a nota (max 150 caracteres)""
  }},
  ""nota_geral"": 7.4,
  ""justificativa"": ""Resumo geral da análise, max 500 caracteres""
}}

Os valores nas notas são apenas exemplos para mostrar que deve usar números, não strings.
Não inclua texto adicional antes ou depois do JSON. Responda apenas com o JSON válido solicitado.";

            return prompt;
        }

        /// <summary>
        /// Agrega as análises de diferentes chunks em uma única análise
        /// </summary>
        private CodeAnalysis AggregateChunkResults(List<CodeAnalysis> chunkAnalyses, CommitInfo commit, string filePath)
        {
            _logger.LogInformation("Agregando análises de {ChunkCount} chunks para o arquivo {FilePath}", 
                chunkAnalyses.Count, filePath);
            
            // Calcular médias de cada métrica
            var variableNaming = (int)Math.Round(chunkAnalyses.Average(a => a.CleanCodeAnalysis.VariableNaming));
            var functionSize = (int)Math.Round(chunkAnalyses.Average(a => a.CleanCodeAnalysis.FunctionSize));
            var commentUsage = (int)Math.Round(chunkAnalyses.Average(a => a.CleanCodeAnalysis.CommentUsage));
            var methodCohesion = (int)Math.Round(chunkAnalyses.Average(a => a.CleanCodeAnalysis.MethodCohesion));
            var deadCodeAvoidance = (int)Math.Round(chunkAnalyses.Average(a => a.CleanCodeAnalysis.DeadCodeAvoidance));
            var overallScore = chunkAnalyses.Average(a => a.OverallScore);
            
            // Concatenar as justificativas
            var justifications = chunkAnalyses.Select((a, i) => $"Parte {i+1}: {a.Justification}").ToList();
            var justification = string.Join("\n\n", justifications);
            
            if (justification.Length > 2000)
            {
                justification = justification.Substring(0, 2000) + "... (justificativa truncada)";
            }
            
            // Criar análise agregada
            var aggregatedAnalysis = new CodeAnalysis
            {
                Id = Guid.NewGuid().ToString("N"),
                CommitId = commit.Id,
                FilePath = filePath,
                Author = commit.Author,
                CommitDate = commit.Date,
                AnalysisDate = DateTime.UtcNow,
                CleanCodeAnalysis = new CleanCodeAnalysis
                {
                    VariableNaming = variableNaming,
                    NamingConventions = new ScoreItem { Score = variableNaming, Justification = "" },
                    FunctionSize = functionSize,
                    CommentUsage = commentUsage,
                    MeaningfulComments = new ScoreItem { Score = commentUsage, Justification = "" },
                    MethodCohesion = methodCohesion,
                    DeadCodeAvoidance = deadCodeAvoidance
                },
                OverallScore = overallScore,
                Justification = $"Análise de {chunkAnalyses.Count} partes do código.\n\n{justification}"
            };
            
            return aggregatedAnalysis;
        }

        /// <summary>
        /// Divide o código em chunks menores para análise
        /// </summary>
        private List<CodeChunk> PartitionCode(string codeContent, string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var maxChunkSize = _options.MaxCodeLength;
            var chunks = new List<CodeChunk>();
            
            // Abordagem de particionamento depende da extensão do arquivo
            switch (extension)
            {
                case ".cs":
                case ".java":
                case ".cpp":
                case ".c":
                    return PartitionCodeByClasses(codeContent, maxChunkSize);
                    
                case ".js":
                case ".ts":
                    return PartitionCodeByFunctions(codeContent, maxChunkSize);
                    
                default:
                    return PartitionCodeByLines(codeContent, maxChunkSize);
            }
        }

        /// <summary>
        /// Particiona o código por classes (para C#, Java, etc.)
        /// </summary>
        private List<CodeChunk> PartitionCodeByClasses(string codeContent, int maxChunkSize)
        {
            var chunks = new List<CodeChunk>();
            
            // Regex para identificar declarações de classe ou interface
            var classRegex = new Regex(@"(public|private|protected|internal|abstract|static|sealed|partial)?\s+(class|interface|struct|enum)\s+(\w+).*?{", RegexOptions.Compiled);
            
            // Tenta encontrar classes/interfaces
            var matches = classRegex.Matches(codeContent);
            
            if (matches.Count > 0)
            {
                // Cabeçalho do arquivo (namespaces, imports, etc)
                var headerEnd = matches[0].Index;
                if (headerEnd > 0)
                {
                    var header = codeContent.Substring(0, headerEnd);
                    chunks.Add(new CodeChunk(0, header, "Cabeçalho do arquivo"));
                }
                
                // Adicionar cada classe como um chunk
                for (int i = 0; i < matches.Count; i++)
                {
                    var currentMatch = matches[i];
                    int startIndex = currentMatch.Index;
                    int endIndex;
                    
                    if (i < matches.Count - 1)
                    {
                        endIndex = matches[i + 1].Index;
                    }
                    else
                    {
                        endIndex = codeContent.Length;
                    }
                    
                    var classContent = codeContent.Substring(startIndex, endIndex - startIndex);
                    var className = currentMatch.Groups[3].Value;
                    
                    // Se a classe for muito grande, particiona por métodos
                    if (classContent.Length > maxChunkSize)
                    {
                        var classChunks = PartitionClassByMethods(classContent, className, maxChunkSize);
                        foreach (var chunk in classChunks)
                        {
                            chunk.Index = chunks.Count;
                            chunks.Add(chunk);
                        }
                    }
                    else
                    {
                        chunks.Add(new CodeChunk(chunks.Count, classContent, $"Classe {className}"));
                    }
                }
            }
            else
            {
                // Fallback para particionamento por linhas se não encontrar classes
                return PartitionCodeByLines(codeContent, maxChunkSize);
            }
            
            return chunks;
        }

        /// <summary>
        /// Particiona uma classe por métodos
        /// </summary>
        private List<CodeChunk> PartitionClassByMethods(string classContent, string className, int maxChunkSize)
        {
            var chunks = new List<CodeChunk>();
            
            // Regex para identificar métodos
            var methodRegex = new Regex(@"(public|private|protected|internal|virtual|abstract|static|override|async)?\s+\w+(\<.*\>)?\s+\w+\s*\(.*\)\s*({|\s*=>)", RegexOptions.Compiled);
            
            // Tentar encontrar métodos
            var matches = methodRegex.Matches(classContent);
            
            if (matches.Count > 0)
            {
                // Parte inicial da classe (campos, propriedades, etc)
                var headerEnd = matches[0].Index;
                if (headerEnd > 0)
                {
                    var header = classContent.Substring(0, headerEnd);
                    chunks.Add(new CodeChunk(0, header, $"Classe {className} - definição e campos"));
                }
                
                // Adicionar cada método como um chunk
                for (int i = 0; i < matches.Count; i++)
                {
                    var currentMatch = matches[i];
                    int startIndex = currentMatch.Index;
                    int endIndex;
                    
                    if (i < matches.Count - 1)
                    {
                        endIndex = matches[i + 1].Index;
                    }
                    else
                    {
                        endIndex = classContent.Length;
                    }
                    
                    var methodContent = classContent.Substring(startIndex, endIndex - startIndex);
                    
                    // Se ainda estiver muito grande, quebrar em partes
                    if (methodContent.Length > maxChunkSize)
                    {
                        var parts = (int)Math.Ceiling((double)methodContent.Length / maxChunkSize);
                        for (int part = 0; part < parts; part++)
                        {
                            int partStart = part * maxChunkSize;
                            int partLength = Math.Min(maxChunkSize, methodContent.Length - partStart);
                            var partContent = methodContent.Substring(partStart, partLength);
                            
                            chunks.Add(new CodeChunk(chunks.Count, partContent, 
                                $"Classe {className} - método grande (parte {part + 1}/{parts})"));
                        }
                    }
                    else
                    {
                        // Extrair o nome do método
                        var methodName = "método";
                        var nameMatch = Regex.Match(methodContent, @"\w+\s+(\w+)\s*\(");
                        if (nameMatch.Success && nameMatch.Groups.Count > 1)
                        {
                            methodName = nameMatch.Groups[1].Value;
                        }
                        
                        chunks.Add(new CodeChunk(chunks.Count, methodContent, $"Classe {className} - método {methodName}"));
                    }
                }
            }
            else
            {
                // Se não encontrou métodos, divide a classe em partes
                var parts = (int)Math.Ceiling((double)classContent.Length / maxChunkSize);
                for (int i = 0; i < parts; i++)
                {
                    int start = i * maxChunkSize;
                    int length = Math.Min(maxChunkSize, classContent.Length - start);
                    var content = classContent.Substring(start, length);
                    
                    chunks.Add(new CodeChunk(i, content, $"Classe {className} - parte {i + 1}/{parts}"));
                }
            }
            
            return chunks;
        }

        /// <summary>
        /// Particiona o código por funções (para JavaScript, TypeScript, etc.)
        /// </summary>
        private List<CodeChunk> PartitionCodeByFunctions(string codeContent, int maxChunkSize)
        {
            var chunks = new List<CodeChunk>();
            
            // Regex para identificar funções e métodos
            var functionRegex = new Regex(@"(function|async function|\w+\s*=\s*function|\w+\s*=\s*\(.*\)\s*=>|const\s+\w+\s*=\s*\(.*\)\s*=>|\w+\s*\(.*\)\s*{)", RegexOptions.Compiled);
            
            // Tentar encontrar funções
            var matches = functionRegex.Matches(codeContent);
            
            if (matches.Count > 0)
            {
                // Parte inicial do arquivo
                var headerEnd = matches[0].Index;
                if (headerEnd > 0)
                {
                    var header = codeContent.Substring(0, headerEnd);
                    chunks.Add(new CodeChunk(0, header, "Código inicial (imports, constantes, etc)"));
                }
                
                // Adicionar cada função como um chunk
                for (int i = 0; i < matches.Count; i++)
                {
                    var currentMatch = matches[i];
                    int startIndex = currentMatch.Index;
                    int endIndex;
                    
                    if (i < matches.Count - 1)
                    {
                        endIndex = matches[i + 1].Index;
                    }
                    else
                    {
                        endIndex = codeContent.Length;
                    }
                    
                    var functionContent = codeContent.Substring(startIndex, endIndex - startIndex);
                    
                    // Se a função for muito grande, dividir em partes
                    if (functionContent.Length > maxChunkSize)
                    {
                        var parts = (int)Math.Ceiling((double)functionContent.Length / maxChunkSize);
                        for (int part = 0; part < parts; part++)
                        {
                            int partStart = part * maxChunkSize;
                            int partLength = Math.Min(maxChunkSize, functionContent.Length - partStart);
                            var partContent = functionContent.Substring(partStart, partLength);
                            
                            chunks.Add(new CodeChunk(chunks.Count, partContent, 
                                $"Função grande (parte {part + 1}/{parts})"));
                        }
                    }
                    else
                    {
                        // Tentar extrair o nome da função
                        var functionName = "função";
                        var nameMatch = Regex.Match(functionContent, @"function\s+(\w+)|const\s+(\w+)|let\s+(\w+)|var\s+(\w+)");
                        if (nameMatch.Success)
                        {
                            for (int g = 1; g < nameMatch.Groups.Count; g++)
                            {
                                if (!string.IsNullOrEmpty(nameMatch.Groups[g].Value))
                                {
                                    functionName = nameMatch.Groups[g].Value;
                                    break;
                                }
                            }
                        }
                        
                        chunks.Add(new CodeChunk(chunks.Count, functionContent, $"Função {functionName}"));
                    }
                }
            }
            else
            {
                // Fallback para particionamento por linhas
                return PartitionCodeByLines(codeContent, maxChunkSize);
            }
            
            return chunks;
        }

        /// <summary>
        /// Particiona o código por linhas (último recurso)
        /// </summary>
        private List<CodeChunk> PartitionCodeByLines(string codeContent, int maxChunkSize)
        {
            var chunks = new List<CodeChunk>();
            var lines = codeContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            // Estimativa do número de chunks necessários
            int totalChunks = (int)Math.Ceiling((double)codeContent.Length / maxChunkSize);
            int linesPerChunk = (int)Math.Ceiling((double)lines.Length / totalChunks);
            
            for (int i = 0; i < totalChunks; i++)
            {
                int startLine = i * linesPerChunk;
                int endLine = Math.Min(startLine + linesPerChunk, lines.Length);
                
                // Concatenar linhas para o chunk atual
                var chunkLines = new List<string>();
                for (int j = startLine; j < endLine; j++)
                {
                    chunkLines.Add(lines[j]);
                }
                
                var chunkContent = string.Join(Environment.NewLine, chunkLines);
                chunks.Add(new CodeChunk(i, chunkContent, $"Parte {i + 1}/{totalChunks} do arquivo"));
            }
            
            return chunks;
        }

        /// <summary>
        /// Classe para representar um chunk de código
        /// </summary>
        private class CodeChunk
        {
            public int Index { get; set; }
            public string Content { get; }
            public string Description { get; }
            
            public CodeChunk(int index, string content, string description)
            {
                Index = index;
                Content = content;
                Description = description;
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

Sua resposta deve ser APENAS um JSON válido com a estrutura exata mostrada abaixo.
IMPORTANTE: 
- Todas as pontuações devem ser números inteiros (não strings)
- Todas as justificativas devem ter no máximo 150 caracteres
- Use apenas caracteres ASCII básicos nas justificativas (sem caracteres especiais ou Unicode)
- EVITE colocar vírgulas após o último item de cada objeto

{{
  ""commit_id"": ""{commit.Id}"",
  ""autor"": ""{commit.Author}"",
  ""analise_clean_code"": {{
    ""nomeclatura_variaveis"": 7,
    ""justificativa_nomenclatura"": ""Curta explicação para a nota (max 150 caracteres)"",
    
    ""tamanho_funcoes"": 8,
    ""justificativa_funcoes"": ""Curta explicação para a nota (max 150 caracteres)"",
    
    ""uso_de_comentarios_relevantes"": 6,
    ""justificativa_comentarios"": ""Curta explicação para a nota (max 150 caracteres)"",
    
    ""cohesao_dos_metodos"": 7,
    ""justificativa_cohesao"": ""Curta explicação para a nota (max 150 caracteres)"",
    
    ""evitacao_de_codigo_morto"": 9,
    ""justificativa_codigo_morto"": ""Curta explicação para a nota (max 150 caracteres)""
  }},
  ""nota_geral"": 7.4,
  ""justificativa"": ""Resumo geral da análise, max 500 caracteres""
}}

Os valores nas notas são apenas exemplos para mostrar que deve usar números, não strings.
Não inclua texto adicional antes ou depois do JSON. Responda apenas com o JSON válido solicitado.";

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
                    return CleanJsonContent(jsonCodeBlockMatch.Groups[1].Value.Trim());
                }
                
                // Tentar extrair o JSON delimitado por ``` e ```
                var codeBlockPattern = @"```\s*([\s\S]*?)\s*```";
                var codeBlockMatch = Regex.Match(text, codeBlockPattern);
                
                if (codeBlockMatch.Success)
                {
                    return CleanJsonContent(codeBlockMatch.Groups[1].Value.Trim());
                }
                
                // Tentar encontrar um objeto JSON 
                var jsonPattern = @"(\{[\s\S]*\})";
                var jsonMatch = Regex.Match(text, jsonPattern);
                
                if (jsonMatch.Success)
                {
                    return CleanJsonContent(jsonMatch.Groups[1].Value.Trim());
                }
                
                // Último recurso: tentar limpar o texto completo e ver se é um JSON
                if (text.Contains("{") && text.Contains("}"))
                {
                    var startIndex = text.IndexOf('{');
                    var endIndex = text.LastIndexOf('}');
                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        var potentialJson = text.Substring(startIndex, endIndex - startIndex + 1);
                        return CleanJsonContent(potentialJson);
                    }
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
        
        /// <summary>
        /// Limpa e corrige problemas comuns em strings JSON geradas por LLMs
        /// </summary>
        private string CleanJsonContent(string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent))
                return jsonContent;
            
            _logger.LogDebug("[JSON_CLEAN] Limpando conteúdo JSON com {Length} caracteres", jsonContent.Length);
            
            try
            {
                // Remove caracteres de formatação invisíveis que possam causar problemas
                var validJson = new StringBuilder();
                foreach (var c in jsonContent)
                {
                    // Aceitar apenas caracteres seguros para JSON
                    if ((c >= ' ' && c <= '~') || c == '\n' || c == '\r' || c == '\t')
                    {
                        validJson.Append(c);
                    }
                    else
                    {
                        _logger.LogDebug("[JSON_CLEAN] Removendo caractere não seguro: 0x{Code:X4}", (int)c);
                    }
                }
                
                jsonContent = validJson.ToString();
                
                // Remove comentários estilo C#/Java
                jsonContent = Regex.Replace(jsonContent, @"//.*?$", "", RegexOptions.Multiline);
                
                // Remove comentários de múltiplas linhas
                jsonContent = Regex.Replace(jsonContent, @"/\*.*?\*/", "", RegexOptions.Singleline);
                
                // Trata problema de vírgulas no final de objetos ou arrays
                jsonContent = Regex.Replace(jsonContent, @",(\s*})", "$1");
                jsonContent = Regex.Replace(jsonContent, @",(\s*])", "$1");
                
                // Corrige aspas no meio de strings (substituição por aspas de escape)
                jsonContent = Regex.Replace(jsonContent, @"(?<=[^\\]""[^""]*)("")", @"\""");
                
                // Converte aspas simples em aspas duplas
                jsonContent = Regex.Replace(jsonContent, @"'([^']*?)'", "\"$1\"");
                
                // Converte valores de texto em valores numéricos quando necessário
                jsonContent = Regex.Replace(jsonContent, @"""(\d+)""", "$1");
                jsonContent = Regex.Replace(jsonContent, @"""(\d+\.\d+)""", "$1");
                
                // Tentar validar se é um JSON válido
                try
                {
                    using (JsonDocument.Parse(jsonContent))
                    {
                        _logger.LogDebug("[JSON_CLEAN] JSON validado com sucesso");
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning("[JSON_CLEAN] JSON inválido após limpeza: {Error}", jsonEx.Message);
                    // Tentar corrigir problemas específicos baseados no erro
                    if (jsonEx.Message.Contains("'0xE2'"))
                    {
                        // Problema com aspas inteligentes ou outro caractere Unicode
                        jsonContent = Regex.Replace(jsonContent, @"[\u201C\u201D]", "\"");
                        jsonContent = Regex.Replace(jsonContent, @"[\u2018\u2019]", "'");
                        _logger.LogDebug("[JSON_CLEAN] Tentativa de correção de caracteres Unicode especiais");
                    }
                }
                
                return jsonContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JSON_CLEAN] Erro ao limpar conteúdo JSON");
                return jsonContent; // Retorna o conteúdo original no caso de erro
            }
        }

        /// <summary>
        /// Garante que uma string seja segura para uso em justificativas
        /// </summary>
        private string EnsureSafeString(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            if (input.Length > maxLength)
                return input.Substring(0, maxLength);

            return input;
        }
    }
} 