using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            // Configurar opções do Git
            services.Configure<GitOptions>(configuration.GetSection("Git"));
            
            // Registrar repositório Git como singleton
            services.AddSingleton<IGitRepository>(sp => {
                var options = sp.GetRequiredService<IOptions<GitOptions>>();
                var logger = sp.GetRequiredService<ILogger<GitRepository>>();
                return new GitRepository(options.Value.RepositoryPath, logger);
            });
            
            return services;
        }
    }
    
    /// <summary>
    /// Opções de configuração para o repositório Git
    /// </summary>
    public class GitOptions
    {
        public string RepositoryPath { get; set; } = "./repositorio";
        public string DefaultBranch { get; set; } = "main";
        public string UserName { get; set; } = "RefactorScore";
        public string UserEmail { get; set; } = "refactorscore@example.com";
    }
} 