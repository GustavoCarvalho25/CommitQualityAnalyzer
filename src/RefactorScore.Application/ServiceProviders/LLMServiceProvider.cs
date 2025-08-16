using System;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RefactorScore.Core.Interfaces;
using RefactorScore.Infrastructure.LLM;

namespace RefactorScore.Application.ServiceProviders
{
    public static class LLMServiceProvider
    {
        /// <summary>
        /// Adiciona os serviços LLM ao contêiner de serviços
        /// </summary>
        /// <param name="services">Coleção de serviços</param>
        /// <param name="configuration">Configuração da aplicação</param>
        /// <returns>Coleção de serviços atualizada</returns>
        public static IServiceCollection AddLLMServices(this IServiceCollection services, IConfiguration configuration)
        {
            var ollamaConfig = configuration.GetSection("Ollama");
            
            services.Configure<OllamaOptions>(ollamaConfig);
            
            int timeoutSegundos = ollamaConfig.GetValue<int>("TimeoutSegundos");
            
            services.AddSingleton<PromptTemplates>(serviceProvider =>
            {
                var templateConfig = configuration.GetSection("PromptTemplates");
                
                return new PromptTemplates
                {
                    AnaliseCodigo = templateConfig["AnaliseCodigo"] ?? new PromptTemplates().AnaliseCodigo,
                    Recomendacoes = templateConfig["Recomendacoes"] ?? new PromptTemplates().Recomendacoes
                };
            });
            
            services.AddHttpClient<OllamaService>((serviceProvider, client) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<OllamaService>>();
                var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;
                
                client.BaseAddress = new Uri(options.BaseUrl);
                
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSegundos);
                
                logger.LogInformation("Configurando HttpClient com timeout de {Timeout} segundos", options.TimeoutSegundos);
            });
            
            services.AddSingleton<ILLMService, OllamaService>();
            
            services.AddSingleton<IAnalisadorCodigo, AnalisadorCodigo>(sp => {
                var llmService = sp.GetRequiredService<ILLMService>();
                var gitRepository = sp.GetRequiredService<IGitRepository>();
                var logger = sp.GetRequiredService<ILogger<AnalisadorCodigo>>();
                var analiseRepository = sp.GetRequiredService<IAnaliseRepository>();
                
                return new AnalisadorCodigo(llmService, gitRepository, logger, analiseRepository);
            });
            
            return services;
        }
    }
} 