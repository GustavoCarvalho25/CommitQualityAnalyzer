using RefactorScore.Domain.Services;

namespace RefactorScore.WorkerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ICommitAnalysisService _commitAnalysisService;
    private readonly IGitServiceFacade _gitService;

    public Worker(ILogger<Worker> logger, ICommitAnalysisService commitAnalysisService, IGitServiceFacade gitService)
    {
        _logger = logger;
        _commitAnalysisService = commitAnalysisService;
        _gitService = gitService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Checking if repository is valid...");
                var isValid = await _gitService.ValidateRepositoryAsync();
                if (!isValid)
                {
                    _logger.LogError("Repository is not valid. Exiting worker service.");
                    return;
                }
                
                _logger.LogInformation("Checking MongoDB connection...");
                var isMongoConnected = await _commitAnalysisService.CheckMongoConnectionAsync();
                if (!isMongoConnected)
                {
                    _logger.LogError("MongoDB connection failed. Exiting worker service.");
                    return;
                }
                
                _logger.LogInformation("Checking LLM service connection...");
                var isLLMConnected = await _commitAnalysisService.CheckLLMConnectionAsync();
                if (!isLLMConnected)
                {
                    _logger.LogError("LLM service connection failed. Exiting worker service.");
                    return;
                }
                
                _logger.LogInformation("🚀 Starting commit analysis cycle at: {time}", DateTimeOffset.Now);

                var recentCommits = await _gitService.GetCommitsByPeriodAsync(
                    DateTime.Now.AddDays(-7), 
                    DateTime.Now
                );

                _logger.LogInformation("Found {Count} commits to analyze", recentCommits.Count);

                foreach (var commit in recentCommits.Take(5))
                {
                    try
                    {
                        _logger.LogInformation("Analyzing commit: {CommitId}", commit.Id);
                        
                        var analysis = await _commitAnalysisService.AnalyzeCommitAsync(commit.Id);
                        
                        _logger.LogInformation("Analysis completed for commit {CommitId}. Overall note: {Note}", 
                            commit.Id, analysis.OverallNote);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error analyzing commit {CommitId}", commit.Id);
                    }
                }

                _logger.LogInformation("Waiting 1 hour for next analysis cycle...");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in worker service");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
