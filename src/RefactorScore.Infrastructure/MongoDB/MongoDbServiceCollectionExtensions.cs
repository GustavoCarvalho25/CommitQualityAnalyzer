using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RefactorScore.Core.Interfaces;

namespace RefactorScore.Infrastructure.MongoDB
{
    public static class MongoDbServiceCollectionExtensions
    {
        public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MongoDbOptions>(configuration.GetSection("MongoDB"))
                .AddSingleton<IAnaliseRepository, MongoDbAnaliseRepository>();
            
            return services;
        }
        
        public static async Task<bool> VerificarConexaoMongoDbAsync(this IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<MongoDbAnaliseRepository>>();
            var options = serviceProvider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            
            try
            {
                //TODO: remover connection string do log após fase de desenvolvimento
                logger.LogInformation("Verificando conexão com MongoDB: {ConnectionString}", options.ConnectionString);
                
                var client = new MongoClient(options.ConnectionString);
                var database = client.GetDatabase(options.DatabaseName);
                
                var collections = await database.ListCollectionNames().ToListAsync();
                
                if (!collections.Contains(options.AnaliseCommitCollectionName))
                {
                    logger.LogInformation("➕ Criando collection {Collection}", options.AnaliseCommitCollectionName);
                    await database.CreateCollectionAsync(options.AnaliseCommitCollectionName);
                }
                
                if (!collections.Contains(options.AnaliseArquivoCollectionName))
                {
                    logger.LogInformation("➕ Criando collection {Collection}", options.AnaliseArquivoCollectionName);
                    await database.CreateCollectionAsync(options.AnaliseArquivoCollectionName);
                }
                
                if (!collections.Contains(options.RecomendacoesCollectionName))
                {
                    logger.LogInformation("➕ Criando collection {Collection}", options.RecomendacoesCollectionName);
                    await database.CreateCollectionAsync(options.RecomendacoesCollectionName);
                }
                
                logger.LogInformation("Conexão com MongoDB estabelecida e verificada com sucesso");
                return true;
            }
            catch (MongoException ex)
            {
                logger.LogError(ex, "ERRO CRÍTICO: Falha ao conectar com MongoDB: {Mensagem}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ERRO CRÍTICO: Erro ao verificar conexão com MongoDB: {Mensagem}", ex.Message);
                return false;
            }
        }
    }
} 