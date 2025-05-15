using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RefactorScore.Core.Entities;
using RefactorScore.Core.Interfaces;
using FileChangeType = RefactorScore.Core.Entities.FileChangeType;

namespace RefactorScore.WorkerService.Workers
{
    public class CommitAnalysisWorker : BackgroundService
    {
        private readonly ICodeAnalyzerService _codeAnalyzerService;
        private readonly ILLMService _llmService;
        private readonly IGitRepository _gitRepository;
        private readonly IAnalysisRepository _analysisRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<CommitAnalysisWorker> _logger;
        private readonly WorkerOptions _options;

        public CommitAnalysisWorker(
            ICodeAnalyzerService codeAnalyzerService,
            ILLMService llmService,
            IGitRepository gitRepository,
            IAnalysisRepository analysisRepository,
            ICacheService cacheService,
            IOptions<WorkerOptions> options,
            ILogger<CommitAnalysisWorker> logger)
        {
            _codeAnalyzerService = codeAnalyzerService;
            _llmService = llmService;
            _gitRepository = gitRepository;
            _analysisRepository = analysisRepository;
            _cacheService = cacheService;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[WORKER_START] Starting CommitAnalysisWorker");

            // Verificar se o LLM está disponível
            _logger.LogInformation("[WORKER_LLM_CHECK] Checking if LLM service is available...");
            if (!await IsLLMAvailable())
            {
                _logger.LogWarning("[WORKER_ABORT] LLM service is not available, CommitAnalysisWorker will not start");
                return;
            }
            
            _logger.LogInformation("[WORKER_READY] LLM service is available, CommitAnalysisWorker is starting main loop");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("[WORKER_CYCLE_START] Starting commit analysis cycle (interval: {IntervalMinutes} minutes)",
                        _options.ScanIntervalMinutes);

                    await ProcessCommitsAsync(stoppingToken);

                    _logger.LogInformation("[WORKER_CYCLE_COMPLETE] Completed analysis cycle, waiting for next cycle");
                    
                    // Aguardar pelo próximo ciclo
                    await Task.Delay(TimeSpan.FromMinutes(_options.ScanIntervalMinutes), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[WORKER_CANCELED] Operation canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKER_ERROR] Unhandled error in CommitAnalysisWorker");
            }
            
            _logger.LogInformation("[WORKER_EXIT] CommitAnalysisWorker is exiting");
        }

        private async Task<bool> IsLLMAvailable()
        {
            try
            {
                _logger.LogInformation("[LLM_CHECK_START] Checking LLM service availability...");
                var startTime = DateTime.UtcNow;
                
                var isAvailable = await _llmService.IsAvailableAsync();
                
                var duration = DateTime.UtcNow - startTime;
                
                if (isAvailable)
                {
                    _logger.LogInformation("[LLM_CHECK_SUCCESS] LLM service is available (response time: {Duration}ms)", 
                        duration.TotalMilliseconds);
                    return true;
                }
                else
                {
                    _logger.LogWarning("[LLM_CHECK_FAIL] LLM service is not available (response time: {Duration}ms)", 
                        duration.TotalMilliseconds);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LLM_CHECK_ERROR] Error checking LLM service availability");
                return false;
            }
        }

        private async Task ProcessCommitsAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("[PROCESS_COMMITS_START] Beginning to process commits");
                
                // Obter commits recentes
                _logger.LogInformation("[PROCESS_COMMITS_FETCH] Fetching recent commits...");
                var commitsResult = await _codeAnalyzerService.GetRecentCommitsAsync();

                if (!commitsResult.IsSuccess)
                {
                    foreach (var error in commitsResult.Errors)
                    {
                        _logger.LogError("[PROCESS_COMMITS_ERROR] Error getting recent commits: {ErrorMessage}", error);
                    }
                    return;
                }

                var commits = commitsResult.Data.Take(_options.MaxProcessingCommits).ToList();
                
                if (commits.Count == 0)
                {
                    _logger.LogInformation("[PROCESS_COMMITS_EMPTY] No recent commits found for analysis");
                    return;
                }

                _logger.LogInformation("[PROCESS_COMMITS_COUNT] Found {CommitCount} commits for analysis", commits.Count);

                // Usar processamento paralelo com um limite de concorrência
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 4),
                    CancellationToken = stoppingToken
                };

                // Rastrear análises realizadas por commit
                var analysisResults = new ConcurrentDictionary<string, int>();
                _logger.LogInformation("[PROCESS_COMMITS_PARALLEL] Starting parallel processing of {CommitCount} commits " +
                    "with max parallelism of {MaxParallelism}", commits.Count, options.MaxDegreeOfParallelism);

                await Parallel.ForEachAsync(commits, options, async (commit, token) =>
                {
                    await ProcessCommitAsync(commit, analysisResults, token);
                });

                // Registrar resultados
                _logger.LogInformation("[PROCESS_COMMITS_SUMMARY] Analysis summary:");
                foreach (var result in analysisResults)
                {
                    _logger.LogInformation("[PROCESS_COMMITS_RESULT] Commit {CommitId}: {AnalysisCount} files analyzed", 
                        result.Key.Substring(0, Math.Min(8, result.Key.Length)), result.Value);
                }
                
                _logger.LogInformation("[PROCESS_COMMITS_COMPLETE] Completed processing all commits");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PROCESS_COMMITS_ERROR] Error processing commits");
            }
        }

        private async Task ProcessCommitAsync(
            CommitInfo commit, 
            ConcurrentDictionary<string, int> analysisResults,
            CancellationToken stoppingToken)
        {
            try
            {
                var shortCommitId = commit.Id.Substring(0, Math.Min(8, commit.Id.Length));
                _logger.LogInformation("[COMMIT_{CommitId}_START] Processing commit: {CommitId} by {Author} - {Message}", 
                    shortCommitId, shortCommitId, commit.Author, commit.Message);

                // Obter alterações no commit
                _logger.LogInformation("[COMMIT_{CommitId}_CHANGES] Fetching changes for commit", shortCommitId);
                var changesResult = await _codeAnalyzerService.GetCommitChangesAsync(commit.Id);

                if (!changesResult.IsSuccess)
                {
                    foreach (var error in changesResult.Errors)
                    {
                        _logger.LogError("[COMMIT_{CommitId}_ERROR] Error getting changes: {ErrorMessage}", 
                            shortCommitId, error);
                    }
                    return;
                }

                // Analisar apenas arquivos que foram adicionados ou modificados (não deletados)
                var filesToAnalyze = changesResult.Data
                    .Where(f => f.Status != FileChangeType.Deleted && IsCodeFile(f.Path != null && !string.IsNullOrEmpty(f.Path) ? f.Path : f.FilePath))
                    // Filtrar arquivos muito grandes
                    .Where(f => (f.ModifiedContent?.Length ?? 0) <= _options.MaxFileSizeKB * 1024)
                    .ToList();

                if (filesToAnalyze.Count == 0)
                {
                    _logger.LogInformation("[COMMIT_{CommitId}_EMPTY] Commit does not contain any files for analysis", shortCommitId);
                    analysisResults.TryAdd(commit.Id, 0);
                    return;
                }

                // Limitar o número de arquivos por commit
                if (filesToAnalyze.Count > _options.MaxFilesPerCommit)
                {
                    _logger.LogWarning("[COMMIT_{CommitId}_LIMIT] Commit has {FileCount} files, limiting to {MaxFiles}",
                        shortCommitId, filesToAnalyze.Count, _options.MaxFilesPerCommit);
                    
                    filesToAnalyze = filesToAnalyze
                        .Take(_options.MaxFilesPerCommit)
                        .ToList();
                }

                _logger.LogInformation("[COMMIT_{CommitId}_FILES] Commit contains {FileCount} files for analysis", 
                    shortCommitId, filesToAnalyze.Count);

                int analysisCount = 0;

                // Analisar arquivos sequencialmente para evitar sobrecarga do LLM
                for (int i = 0; i < filesToAnalyze.Count; i++)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("[COMMIT_{CommitId}_CANCELED] Processing canceled", shortCommitId);
                        break;
                    }

                    var file = filesToAnalyze[i];
                    string filePath = file.Path != null && !string.IsNullOrEmpty(file.Path) ? file.Path : file.FilePath;
                    
                    try
                    {
                        _logger.LogInformation("[COMMIT_{CommitId}_FILE_{FileIndex}] Analyzing file {FilePath} ({FileIndex}/{TotalFiles})", 
                            shortCommitId, i+1, filePath, i+1, filesToAnalyze.Count);
                        
                        var startTime = DateTime.UtcNow;
                        var analysisResult = await _codeAnalyzerService.AnalyzeCommitFileAsync(commit.Id, filePath);
                        var duration = DateTime.UtcNow - startTime;
                        
                        if (analysisResult.IsSuccess)
                        {
                            analysisCount++;
                            
                            _logger.LogInformation("[COMMIT_{CommitId}_FILE_{FileIndex}_SUCCESS] File {FilePath} successfully analyzed " + 
                                "in {Duration}ms. Score: {Score}/10", 
                                shortCommitId, i+1, filePath, duration.TotalMilliseconds, analysisResult.Data.OverallScore);
                        }
                        else
                        {
                            foreach (var error in analysisResult.Errors)
                            {
                                _logger.LogWarning("[COMMIT_{CommitId}_FILE_{FileIndex}_FAIL] Could not analyze {FilePath}: {ErrorMessage}", 
                                    shortCommitId, i+1, filePath, error);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[COMMIT_{CommitId}_FILE_{FileIndex}_ERROR] Error analyzing file {FilePath}", 
                            shortCommitId, i+1, filePath);
                    }
                }

                analysisResults.TryAdd(commit.Id, analysisCount);
                _logger.LogInformation("[COMMIT_{CommitId}_COMPLETE] Completed processing commit with {AnalysisCount}/{TotalCount} files analyzed", 
                    shortCommitId, analysisCount, filesToAnalyze.Count);
            }
            catch (Exception ex)
            {
                var shortCommitId = commit.Id.Substring(0, Math.Min(8, commit.Id.Length));
                _logger.LogError(ex, "[COMMIT_{CommitId}_ERROR] Error processing commit", shortCommitId);
                analysisResults.TryAdd(commit.Id, 0);
            }
        }

        private bool IsCodeFile(string filePath)
        {
            // Lista de extensões de arquivo para analisar
            var codeExtensions = new[]
            {
                ".cs", ".fs", ".vb", // .NET
                ".js", ".ts", ".jsx", ".tsx", // JavaScript/TypeScript
                ".java", ".kt", // Java/Kotlin
                ".py", // Python
                ".rb", // Ruby
                ".php", // PHP
                ".go", // Go
                ".rs", // Rust
                ".swift", // Swift
                ".c", ".cpp", ".h", ".hpp", // C/C++
                ".m", ".mm" // Objective-C
            };

            // Verificar se o arquivo possui uma extensão de código
            return codeExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
} 