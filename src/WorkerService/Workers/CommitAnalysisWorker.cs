using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RefactorScore.Core.Entities;
using RefactorScore.Core.Interfaces;
using RefactorScore.Core.Specifications;

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
            _logger.LogInformation("Starting CommitAnalysisWorker");

            // Verificar se o LLM está disponível
            if (!await IsLLMAvailable())
            {
                _logger.LogWarning("LLM service is not available, CommitAnalysisWorker will not start");
                return;
            }

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Running commit analysis cycle (interval: {IntervalMinutes} minutes)",
                        _options.ScanIntervalMinutes);

                    await ProcessCommitsAsync(stoppingToken);

                    // Aguardar pelo próximo ciclo
                    await Task.Delay(TimeSpan.FromMinutes(_options.ScanIntervalMinutes), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operation canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in CommitAnalysisWorker");
            }
        }

        private async Task<bool> IsLLMAvailable()
        {
            try
            {
                _logger.LogInformation("Checking LLM service availability...");
                var isAvailable = await _llmService.IsAvailableAsync();
                
                if (isAvailable)
                {
                    _logger.LogInformation("LLM service is available");
                    return true;
                }
                else
                {
                    _logger.LogWarning("LLM service is not available");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking LLM service availability");
                return false;
            }
        }

        private async Task ProcessCommitsAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Obter commits recentes
                var commitsResult = await _codeAnalyzerService.GetRecentCommitsAsync();

                if (!commitsResult.IsSuccess)
                {
                    foreach (var error in commitsResult.Errors)
                    {
                        _logger.LogError("Error getting recent commits: {ErrorMessage}", error);
                    }
                    return;
                }

                var commits = commitsResult.Data.Take(_options.MaxProcessingCommits).ToList();
                
                if (commits.Count == 0)
                {
                    _logger.LogInformation("No recent commits found for analysis");
                    return;
                }

                _logger.LogInformation("Found {CommitCount} commits for analysis", commits.Count);

                // Usar processamento paralelo com um limite de concorrência
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 4),
                    CancellationToken = stoppingToken
                };

                // Rastrear análises realizadas por commit
                var analysisResults = new ConcurrentDictionary<string, int>();

                await Parallel.ForEachAsync(commits, options, async (commit, token) =>
                {
                    await ProcessCommitAsync(commit, analysisResults, token);
                });

                // Registrar resultados
                foreach (var result in analysisResults)
                {
                    _logger.LogInformation("Commit {CommitId}: {AnalysisCount} files analyzed", 
                        result.Key.Substring(0, Math.Min(8, result.Key.Length)), result.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing commits");
            }
        }

        private async Task ProcessCommitAsync(
            CommitInfo commit, 
            ConcurrentDictionary<string, int> analysisResults,
            CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Processing commit: {CommitId} by {Author} - {Message}", 
                    commit.Id.Substring(0, Math.Min(8, commit.Id.Length)), 
                    commit.Author, 
                    commit.Message);

                // Obter alterações no commit
                var changesResult = await _codeAnalyzerService.GetCommitChangesAsync(commit.Id);

                if (!changesResult.IsSuccess)
                {
                    foreach (var error in changesResult.Errors)
                    {
                        _logger.LogError("Error getting changes for commit {CommitId}: {ErrorMessage}", 
                            commit.Id, error);
                    }
                    return;
                }

                // Analisar apenas arquivos que foram adicionados ou modificados (não deletados)
                var filesToAnalyze = changesResult.Data
                    .Where(f => f.Status != FileChangeType.Deleted && IsCodeFile(f.Path != null && !string.IsNullOrEmpty(f.Path) ? f.Path : f.FilePath))
                    .ToList();

                if (filesToAnalyze.Count == 0)
                {
                    _logger.LogInformation("Commit {CommitId} does not contain any files for analysis", commit.Id);
                    analysisResults.TryAdd(commit.Id, 0);
                    return;
                }

                _logger.LogInformation("Commit {CommitId} contains {FileCount} files for analysis", 
                    commit.Id.Substring(0, Math.Min(8, commit.Id.Length)), filesToAnalyze.Count);

                int analysisCount = 0;

                // Analisar arquivos sequencialmente para evitar sobrecarga do LLM
                foreach (var file in filesToAnalyze)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    string filePath = file.Path != null && !string.IsNullOrEmpty(file.Path) ? file.Path : file.FilePath;
                    
                    try
                    {
                        var analysisResult = await _codeAnalyzerService.AnalyzeCommitFileAsync(commit.Id, filePath);
                        
                        if (analysisResult.IsSuccess)
                        {
                            analysisCount++;
                            
                            _logger.LogInformation("File {FilePath} successfully analyzed. Score: {Score}/10", 
                                filePath, analysisResult.Data.OverallScore);
                        }
                        else
                        {
                            foreach (var error in analysisResult.Errors)
                            {
                                _logger.LogWarning("Could not analyze {FilePath}: {ErrorMessage}", 
                                    filePath, error);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error analyzing file {FilePath}", filePath);
                    }
                }

                analysisResults.TryAdd(commit.Id, analysisCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing commit {CommitId}", commit.Id);
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