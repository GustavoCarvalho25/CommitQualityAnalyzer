using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace RefactorScore.Infrastructure.HealthChecks;

public class MongoDbHealthCheck : IHealthCheck
{
    private readonly IMongoDatabase _mongoDatabase;
    private readonly ILogger<MongoDbHealthCheck> _logger;

    public MongoDbHealthCheck(IMongoDatabase mongoDatabase, ILogger<MongoDbHealthCheck> logger)
    {
        _mongoDatabase = mongoDatabase;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            await _mongoDatabase.RunCommandAsync<object>(new JsonCommand<object>("{ ping: 1 }"), cancellationToken: cancellationToken);
            
            _logger.LogInformation("MongoDb health check passed.");
            return HealthCheckResult.Healthy("MongoDb is ready.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDb health check failed.");
            return HealthCheckResult.Unhealthy("MongoDb is not ready.", ex);
        }
    }
}