using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RefactorScore.Application.Options;
using RefactorScore.Core.Interfaces;
using RefactorScore.Infrastructure.Git;

namespace RefactorScore.Application.ServiceProviders
{
    /// <summary>
    /// Provedor de serviços Git para injeção de dependência
    /// </summary>
    public static class GitServiceProvider
    {
        /// <summary>
        /// Adiciona os serviços Git ao contêiner de serviços
        /// </summary>
        /// <param name="services">Coleção de serviços</param>
        /// <param name="configuration">Configuração da aplicação</param>
        /// <returns>Coleção de serviços atualizada</returns>
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
} 