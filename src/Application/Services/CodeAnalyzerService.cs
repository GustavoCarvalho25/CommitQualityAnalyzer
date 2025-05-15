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
                
                // Processar com o LLM
                _logger.LogInformation("[ANALYZER_LLM_CALL] Sending prompt to LLM service using model {ModelName}...", 
                    _options.ModelName);
                var startTime = DateTime.UtcNow;
                
                var responseText = await _llmService.ProcessPromptAsync(prompt, _options.ModelName);
                
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("[ANALYZER_LLM_RESPONSE] Received response from LLM after {Duration}ms. " +
                    "Response length: {ResponseLength} chars", 
                    duration.TotalMilliseconds, responseText?.Length ?? 0);
                
                // Extrair JSON da resposta
                _logger.LogInformation("[ANALYZER_EXTRACT_JSON] Extracting JSON from LLM response...");
                var jsonContent = ExtractJsonFromText(responseText);
                
                if (string.IsNullOrEmpty(jsonContent))
                {
                    _logger.LogWarning("[ANALYZER_ERROR] Could not extract JSON from response");
                    return Result<CodeAnalysis>.Fail("Não foi possível extrair JSON da resposta");
                }
                
                _logger.LogInformation("[ANALYZER_JSON] Successfully extracted JSON, length: {JsonLength} chars", 
                    jsonContent.Length);
                
                try
                {
                    // Converter JSON para objeto de análise
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
                    
                    _logger.LogInformation("[ANALYZER_SUCCESS] Successfully analyzed file {FilePath}", filePath);
                    return Result<CodeAnalysis>.Success(analysis);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "[ANALYZER_JSON_ERROR] Error deserializing JSON: {Message}", ex.Message);
                    return Result<CodeAnalysis>.Fail($"Erro ao deserializar JSON: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ANALYZER_EXCEPTION] Error analyzing code with LLM");
                return Result<CodeAnalysis>.Fail(ex);
            }
        }

        /// <summary>
        /// Analisa código particionado em chunks menores
        /// </summary>
        private async Task<Result<CodeAnalysis>> AnalyzePartitionedCodeAsync(
            string codeContent,
            string codeDiff,
            string filePath,
            CommitInfo commit)
        {
            try
            {
                _logger.LogInformation("[PARTITION_START] Analyzing partitioned code for {FilePath}", filePath);
                
                // Dividir o código em chunks menores
                _logger.LogInformation("[PARTITION_SPLIT] Splitting code into chunks for {FilePath}", filePath);
                var chunks = PartitionCode(codeContent, filePath);
                _logger.LogInformation("[PARTITION_CHUNKS] Code divided into {ChunkCount} partitions", chunks.Count);
                
                // Resultados de análise para cada chunk
                var chunkResults = new List<CodeAnalysis>();
                
                // Analisar cada chunk separadamente
                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}] Starting analysis of chunk {ChunkIndex}/{ChunkCount} ({Description}) for file {FilePath}", 
                        i+1, i+1, chunks.Count, chunk.Description, filePath);
                    
                    _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_PROMPT] Building prompt for chunk", i+1);
                    var chunkPrompt = BuildChunkAnalysisPrompt(chunk.Content, codeDiff, filePath, commit, chunk.Description);
                    _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_PROMPT] Prompt built, length: {PromptLength} chars", 
                        i+1, chunkPrompt.Length);
                    
                    _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_LLM] Sending chunk to LLM service...", i+1);
                    var startTime = DateTime.UtcNow;
                    var responseText = await _llmService.ProcessPromptAsync(chunkPrompt, _options.ModelName);
                    var duration = DateTime.UtcNow - startTime;
                    
                    _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_LLM_RESPONSE] Received response after {Duration}ms. Length: {ResponseLength} chars", 
                        i+1, duration.TotalMilliseconds, responseText?.Length ?? 0);
                    
                    _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_JSON] Extracting JSON from response", i+1);
                    var jsonContent = ExtractJsonFromText(responseText);
                    if (string.IsNullOrEmpty(jsonContent))
                    {
                        _logger.LogWarning("[PARTITION_CHUNK_{ChunkIndex}_ERROR] Could not extract JSON from response", i+1);
                        continue;
                    }
                    
                    try
                    {
                        _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_DESERIALIZE] Deserializing JSON", i+1);
                        var llmResponse = JsonSerializer.Deserialize<LlmAnalysisResponse>(jsonContent, _jsonOptions);
                        if (llmResponse == null)
                        {
                            _logger.LogWarning("[PARTITION_CHUNK_{ChunkIndex}_ERROR] Null response after JSON deserialization", i+1);
                            continue;
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
                                FunctionSize = (int)Math.Round(llmResponse.AnaliseCleanCode.TamanhoFuncoes),
                                CommentUsage = (int)Math.Round(llmResponse.AnaliseCleanCode.UsoDeComentariosRelevantes),
                                MethodCohesion = (int)Math.Round(llmResponse.AnaliseCleanCode.CohesaoDosMetodos),
                                DeadCodeAvoidance = (int)Math.Round(llmResponse.AnaliseCleanCode.EvitacaoDeCodigoMorto)
                            },
                            OverallScore = llmResponse.NotaGeral,
                            Justification = llmResponse.Justificativa
                        };
                        
                        chunkResults.Add(chunkAnalysis);
                        _logger.LogInformation("[PARTITION_CHUNK_{ChunkIndex}_SUCCESS] Successfully analyzed chunk", i+1);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[PARTITION_CHUNK_{ChunkIndex}_EXCEPTION] Error processing chunk analysis", i+1);
                    }
                }
                
                if (chunkResults.Count == 0)
                {
                    _logger.LogWarning("[PARTITION_ERROR] No chunks could be analyzed for file {FilePath}", filePath);
                    return Result<CodeAnalysis>.Fail("Não foi possível analisar nenhuma parte do código");
                }
                
                // Agregar os resultados dos chunks
                _logger.LogInformation("[PARTITION_AGGREGATE] Aggregating results from {ChunkCount} chunks", chunkResults.Count);
                var result = Result<CodeAnalysis>.Success(AggregateChunkAnalyses(chunkResults, filePath, commit));
                _logger.LogInformation("[PARTITION_COMPLETE] Completed partitioned analysis for {FilePath}", filePath);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PARTITION_EXCEPTION] Error analyzing partitioned code");
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
IMPORTANTE: Todas as pontuações devem ser números inteiros (não strings).
EVITE colocar vírgulas após o último item de cada objeto.

{{
  ""commit_id"": ""{commit.Id}"",
  ""autor"": ""{commit.Author}"",
  ""analise_clean_code"": {{
    ""nomeclatura_variaveis"": 7,
    ""tamanho_funcoes"": 8,
    ""uso_de_comentarios_relevantes"": 6,
    ""cohesao_dos_metodos"": 7,
    ""evitacao_de_codigo_morto"": 9
  }},
  ""nota_geral"": 7.4,
  ""justificativa"": ""Justificativa da análise aqui""
}}

Os valores nas notas são apenas exemplos para mostrar que deve usar números, não strings.
Não inclua texto adicional antes ou depois do JSON. Responda apenas com o JSON válido solicitado.";

            return prompt;
        }

        /// <summary>
        /// Agrega as análises de diferentes chunks em uma única análise
        /// </summary>
        private CodeAnalysis AggregateChunkAnalyses(List<CodeAnalysis> chunkAnalyses, string filePath, CommitInfo commit)
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
IMPORTANTE: Todas as pontuações devem ser números inteiros (não strings).
EVITE colocar vírgulas após o último item de cada objeto.

{{
  ""commit_id"": ""{commit.Id}"",
  ""autor"": ""{commit.Author}"",
  ""analise_clean_code"": {{
    ""nomeclatura_variaveis"": 7,
    ""tamanho_funcoes"": 8,
    ""uso_de_comentarios_relevantes"": 6,
    ""cohesao_dos_metodos"": 7,
    ""evitacao_de_codigo_morto"": 9
  }},
  ""nota_geral"": 7.4,
  ""justificativa"": ""Justificativa da análise aqui""
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
                
            // Remove comentários estilo C#/Java
            jsonContent = Regex.Replace(jsonContent, @"//.*?$", "", RegexOptions.Multiline);
            
            // Remove comentários de múltiplas linhas
            jsonContent = Regex.Replace(jsonContent, @"/\*.*?\*/", "", RegexOptions.Singleline);
            
            // Trata problema de vírgulas no final de objetos ou arrays
            jsonContent = Regex.Replace(jsonContent, @",(\s*})", "$1");
            jsonContent = Regex.Replace(jsonContent, @",(\s*])", "$1");
            
            // Converte aspas simples em aspas duplas
            jsonContent = Regex.Replace(jsonContent, @"'([^']*?)'", "\"$1\"");
            
            // Converte valores de texto em valores numéricos quando necessário
            jsonContent = Regex.Replace(jsonContent, @"""(\d+)""", "$1");
            jsonContent = Regex.Replace(jsonContent, @"""(\d+\.\d+)""", "$1");
            
            return jsonContent;
        }
    }
} 