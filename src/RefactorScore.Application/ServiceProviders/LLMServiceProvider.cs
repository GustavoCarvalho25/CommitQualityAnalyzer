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
    /// <summary>
    /// Provedor de serviços LLM para injeção de dependência
    /// </summary>
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
            // Obter a seção de configuração do Ollama
            var ollamaConfig = configuration.GetSection("Ollama");
            
            // Registrar configurações do Ollama
            services.Configure<OllamaOptions>(ollamaConfig);
            
            // Obter o valor do timeout diretamente da configuração
            int timeoutSegundos = ollamaConfig.GetValue<int>("TimeoutSegundos");
            
            // Registrar templates de prompts
            services.AddSingleton<PromptTemplates>(serviceProvider =>
            {
                var templateConfig = configuration.GetSection("PromptTemplates");
                
                return new PromptTemplates
                {
                    AnaliseCodigo = templateConfig["AnaliseCodigo"] ?? new PromptTemplates().AnaliseCodigo,
                    Recomendacoes = templateConfig["Recomendacoes"] ?? new PromptTemplates().Recomendacoes
                };
            });
            
            // Registrar cliente HTTP para Ollama como singleton
            services.AddHttpClient<OllamaService>((serviceProvider, client) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<OllamaService>>();
                var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;
                
                client.BaseAddress = new Uri(options.BaseUrl);
                
                // Aplicar o timeout da configuração
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSegundos);
                
                logger.LogInformation("🔧 Configurando HttpClient com timeout de {Timeout} segundos", options.TimeoutSegundos);
            });
            services.AddSingleton<ILLMService, OllamaService>();
            
            // Registrar serviço de análise de código como singleton
            services.AddSingleton<IAnalisadorCodigo, AnalisadorCodigo>();
            
            return services;
        }
    }
} 