using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RefactorScore.Core.Interfaces;
using RefactorScore.Infrastructure.GitIntegration;
using RefactorScore.Infrastructure.MongoDB;
using RefactorScore.Infrastructure.Ollama;
using RefactorScore.Infrastructure.RedisCache;
using StackExchange.Redis;

namespace RefactorScore.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adiciona serviços de infraestrutura ao container de dependências
        /// </summary>
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configurar opções
            services.Configure<GitRepositoryOptions>(configuration.GetSection("GitRepository"));
            services.Configure<RedisCacheOptions>(configuration.GetSection("RedisCache"));
            services.Configure<MongoDbOptions>(configuration.GetSection("MongoDB"));
            services.Configure<OllamaOptions>(configuration.GetSection("Ollama"));
            
            // Adicionar serviços
            services.AddSingleton<IGitRepository, GitRepository>();
            
            // Registrar Redis connection multiplexer
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var connString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
                return ConnectionMultiplexer.Connect(connString);
            });
            
            services.AddSingleton<ICacheService, RedisCacheService>();
            services.AddSingleton<IAnalysisRepository, MongoDbAnalysisRepository>();
            
            // Registrar HttpClient para o serviço Ollama
            services.AddHttpClient<ILLMService, OllamaService>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>();
                client.BaseAddress = new Uri(options.Value.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(5); // Timeout maior para modelos grandes
            });
            
            // Configurar conexão com Redis
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis") ?? "localhost:6379";
                options.InstanceName = "RefactorScore:";
            });
            
            return services;
        }
    }
} 