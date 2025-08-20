using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RefactorScore.Infrastructure.HealthChecks;

namespace RefactorScore.CrossCutting.IoC.DependenceInjection;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>("mongodb", HealthStatus.Unhealthy, tags: new[] { "database" })
            .AddCheck<OllamaHealthCheck>("ollama", HealthStatus.Unhealthy, tags: new[] { "llm" })
            .AddCheck<GitRepositoryHealthCheck>("git-repository", HealthStatus.Unhealthy, tags: new[] { "git" });
        
        services.AddSingleton<MongoDbHealthCheck>();
        services.AddSingleton<OllamaHealthCheck>();
        services.AddSingleton<GitRepositoryHealthCheck>();
        
        return services;
    }
}