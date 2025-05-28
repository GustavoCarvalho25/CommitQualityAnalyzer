using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RefactorScore.Core.Interfaces;

namespace RefactorScore.Infrastructure.MongoDB
{
    /// <summary>
    /// Extensões para registrar serviços do MongoDB
    /// </summary>
    public static class MongoDbServiceCollectionExtensions
    {
        /// <summary>
        /// Adiciona os serviços do MongoDB ao container de DI
        /// </summary>
        /// <param name="services">Collection de serviços</param>
        /// <param name="configuration">Configuração da aplicação</param>
        /// <returns>Collection de serviços modificada</returns>
        public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
        {
            // Configurar opções do MongoDB
            services.Configure<MongoDbOptions>(configuration.GetSection("MongoDB"));
            
            // Registrar o repositório de análises
            services.AddSingleton<IAnaliseRepository, MongoDbAnaliseRepository>();
            
            return services;
        }
    }
} 