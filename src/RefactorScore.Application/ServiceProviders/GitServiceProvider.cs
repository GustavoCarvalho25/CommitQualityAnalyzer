using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RefactorScore.Application.Options;
using RefactorScore.Domain.Interfaces;
using RefactorScore.Infrastructure.Git;

namespace RefactorScore.Application.ServiceProviders;
public static class GitServiceProvider
{
    public static IServiceCollection AddGitServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GitOptions>(configuration.GetSection("Git"));
            
        services.AddSingleton<IGitRepository>(sp => {
            var options = sp.GetRequiredService<IOptions<GitOptions>>();
            var logger = sp.GetRequiredService<ILogger<GitRepository>>();
            return new GitRepository(options.Value.RepositoryPath, logger);
        });
            
        return services;
    }
}