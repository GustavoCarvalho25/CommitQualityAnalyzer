using CrossCutting.IoC.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RefactorScore.Application.Services;
using RefactorScore.Domain.Repositories;
using RefactorScore.Domain.Services;
using RefactorScore.Infrastructure.Mappers;
using RefactorScore.Infrastructure.MongoDB;
using RefactorScore.Infrastructure.Repositories;
using RefactorScore.Infrastructure.Services;

namespace CrossCutting.IoC.DependenceInjection;

public static class InfraestructureServiceExtensions
{
    public static IServiceCollection AddInfraestructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var mongoSettings = configuration.GetSection("MongoDB").Get<MongoDbSettings>(); 
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));
        
        services.AddSingleton<IMongoClient>(sp => new MongoClient(mongoSettings.ConnectionString));
        services.AddScoped<IMongoDatabase>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            return client.GetDatabase(mongoSettings.DatabaseName);
        });
        
        services.AddScoped<ICommitAnalysisRepository, CommitAnalysisRepository>();

        services.AddScoped<IGitServiceFacade>(sp =>
        {
            var gitSettings = configuration.GetSection("Git").Get<GitSettings>();
            var mapper = sp.GetRequiredService<GitMapper>();
            var logger = sp.GetRequiredService<ILogger<GitServiceFacade>>();
            return new GitServiceFacade(gitSettings.RepositoryPath, mapper, logger);
        });
        
        services.AddScoped<GitMapper>();
        
        services.AddHttpClient();

        services.AddScoped<ILLMServiceNew>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var logger = sp.GetRequiredService<ILogger<OllamaLLMService>>();
            var ollamaSettings = configuration.GetSection("Ollama").Get<OllamaSettings>();
            return new OllamaLLMService(logger, httpClient, ollamaSettings.BaseUrl);
        });

        return services;
    }
}