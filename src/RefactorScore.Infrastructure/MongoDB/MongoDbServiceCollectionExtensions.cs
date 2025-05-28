using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
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
        
        /// <summary>
        /// Verifica se a conexão com o MongoDB está funcionando
        /// </summary>
        /// <param name="serviceProvider">Provider de serviços</param>
        /// <returns>True se a conexão estiver ok, False caso contrário</returns>
        public static async Task<bool> VerificarConexaoMongoDbAsync(this IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<MongoDbAnaliseRepository>>();
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoDbOptions>>().Value;
            
            try
            {
                logger.LogInformation("🔍 Verificando conexão com MongoDB: {ConnectionString}", options.ConnectionString);
                
                var client = new MongoClient(options.ConnectionString);
                var database = client.GetDatabase(options.DatabaseName);
                
                // Criar collections se não existirem
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
                
                logger.LogInformation("✅ Conexão com MongoDB estabelecida e verificada com sucesso");
                return true;
            }
            catch (MongoException ex)
            {
                logger.LogError(ex, "❌ ERRO CRÍTICO: Falha ao conectar com MongoDB: {Mensagem}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ ERRO CRÍTICO: Erro ao verificar conexão com MongoDB: {Mensagem}", ex.Message);
                return false;
            }
        }
    }
} 