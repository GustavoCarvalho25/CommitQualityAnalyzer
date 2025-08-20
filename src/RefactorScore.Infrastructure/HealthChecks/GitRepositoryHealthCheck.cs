using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace RefactorScore.Infrastructure.HealthChecks;

public class GitRepositoryHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GitRepositoryHealthCheck> _logger;

    public GitRepositoryHealthCheck(IConfiguration configuration, ILogger<GitRepositoryHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = _configuration.GetSection("Git:RepositoryPath").Value;
            
            if (string.IsNullOrEmpty(repositoryPath))
            {
                return HealthCheckResult.Unhealthy("Git repository path is not configured");
            }

            if (!Directory.Exists(repositoryPath))
            {
                return HealthCheckResult.Unhealthy($"Git repository path does not exist: {repositoryPath}");
            }

            if (!LibGit2Sharp.Repository.IsValid(repositoryPath))
            {
                return HealthCheckResult.Unhealthy($"Path is not a valid Git repository: {repositoryPath}");
            }

            _logger.LogDebug("Git repository health check passed");
            return HealthCheckResult.Healthy($"Git repository is valid: {repositoryPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git repository health check failed");
            return HealthCheckResult.Unhealthy("Git repository validation failed", ex);
        }
    }
}