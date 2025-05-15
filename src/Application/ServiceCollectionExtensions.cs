using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RefactorScore.Application.Services;
using RefactorScore.Core.Interfaces;

namespace RefactorScore.Application
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adiciona serviços da camada de aplicação ao container de dependências
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Registrar opções de configuração
            services.Configure<CodeAnalyzerOptions>(configuration.GetSection("CodeAnalyzer"));
            
            // Registrar serviços
            services.AddSingleton<ICodeAnalyzerService, CodeAnalyzerService>();
            
            return services;
        }
    }
} 