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
            _logger.LogInformation("Iniciando CommitAnalysisWorker");

            // Verificar se o LLM está disponível
            if (!await IsLLMAvailable())
            {
                _logger.LogWarning("Serviço LLM não está disponível, CommitAnalysisWorker não será iniciado");
                return;
            }

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Executando ciclo de análise de commits (intervalo: {IntervalMinutes} minutos)",
                        _options.ScanIntervalMinutes);

                    await ProcessCommitsAsync(stoppingToken);

                    // Aguardar pelo próximo ciclo
                    await Task.Delay(TimeSpan.FromMinutes(_options.ScanIntervalMinutes), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operação cancelada");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro não tratado no CommitAnalysisWorker");
            }
        }

        private async Task<bool> IsLLMAvailable()
        {
            try
            {
                _logger.LogInformation("Verificando disponibilidade do serviço LLM...");
                var isAvailable = await _llmService.IsAvailableAsync();
                
                if (isAvailable)
                {
                    _logger.LogInformation("Serviço LLM está disponível");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Serviço LLM não está disponível");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar disponibilidade do serviço LLM");
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
                        _logger.LogError("Erro ao obter commits recentes: {ErrorMessage}", error.Message);
                    }
                    return;
                }

                var commits = commitsResult.Data.Take(_options.MaxProcessingCommits).ToList();
                
                if (commits.Count == 0)
                {
                    _logger.LogInformation("Nenhum commit recente encontrado para análise");
                    return;
                }

                _logger.LogInformation("Encontrados {CommitCount} commits para análise", commits.Count);

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
                    _logger.LogInformation("Commit {CommitId}: {AnalysisCount} arquivos analisados", 
                        result.Key.Substring(0, Math.Min(8, result.Key.Length)), result.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar commits");
            }
        }

        private async Task ProcessCommitAsync(
            CommitInfo commit, 
            ConcurrentDictionary<string, int> analysisResults,
            CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Processando commit: {CommitId} por {Author} - {Message}", 
                    commit.Id.Substring(0, Math.Min(8, commit.Id.Length)), 
                    commit.Author, 
                    commit.Message);

                // Obter alterações no commit
                var changesResult = await _codeAnalyzerService.GetCommitChangesAsync(commit.Id);

                if (!changesResult.IsSuccess)
                {
                    foreach (var error in changesResult.Errors)
                    {
                        _logger.LogError("Erro ao obter alterações do commit {CommitId}: {ErrorMessage}", 
                            commit.Id, error.Message);
                    }
                    return;
                }

                // Analisar apenas arquivos que foram adicionados ou modificados (não deletados)
                var filesToAnalyze = changesResult.Data
                    .Where(f => f.Status != FileChangeType.Deleted && IsCodeFile(f.Path))
                    .ToList();

                if (filesToAnalyze.Count == 0)
                {
                    _logger.LogInformation("Commit {CommitId} não contém arquivos para análise", commit.Id);
                    analysisResults.TryAdd(commit.Id, 0);
                    return;
                }

                _logger.LogInformation("Commit {CommitId} contém {FileCount} arquivos para análise", 
                    commit.Id.Substring(0, Math.Min(8, commit.Id.Length)), filesToAnalyze.Count);

                int analysisCount = 0;

                // Analisar arquivos sequencialmente para evitar sobrecarga do LLM
                foreach (var file in filesToAnalyze)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        var analysisResult = await _codeAnalyzerService.AnalyzeCommitFileAsync(commit.Id, file.Path);
                        
                        if (analysisResult.IsSuccess)
                        {
                            analysisCount++;
                            
                            _logger.LogInformation("Arquivo {FilePath} analisado com sucesso. Nota: {Score}/10", 
                                file.Path, analysisResult.Data.OverallScore);
                        }
                        else
                        {
                            foreach (var error in analysisResult.Errors)
                            {
                                _logger.LogWarning("Não foi possível analisar {FilePath}: {ErrorMessage}", 
                                    file.Path, error.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao analisar arquivo {FilePath}", file.Path);
                    }
                }

                analysisResults.TryAdd(commit.Id, analysisCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar commit {CommitId}", commit.Id);
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