using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace RefactorScore.Infrastructure.HealthChecks;

public class OllamaHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private IConfiguration _configuration;
    private readonly ILogger<OllamaHealthCheck> _logger;

    public OllamaHealthCheck(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaHealthCheck> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var ollamaUrl = _configuration.GetSection("Ollama:BaseUrl").Value ?? "http://localhost:11434";
            var response = await _httpClient.GetAsync($"{ollamaUrl}/api/tags", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Ollama health check passed");
                return HealthCheckResult.Healthy("Ollama is responding");
            }
            
            _logger.LogWarning("Ollama health check failed with status: {StatusCode}", response.StatusCode);
            return HealthCheckResult.Unhealthy($"Ollama returned status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama health check failed");
            return HealthCheckResult.Unhealthy("Ollama is not responding", ex);
        }
    }
}