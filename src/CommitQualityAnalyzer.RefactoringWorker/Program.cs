using CommitQualityAnalyzer.Core.Repositories;
using CommitQualityAnalyzer.RefactoringWorker;
using CommitQualityAnalyzer.RefactoringWorker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Iniciando RefactoringWorker");

try
{
    var builder = Host.CreateDefaultBuilder()
        .UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext())
        .ConfigureServices((hostContext, services) =>
        {
            // Adicionar serviços
            services.AddSingleton<ICodeAnalysisRepository>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                return new MongoCodeAnalysisRepository(configuration);
            });
            
            services.AddSingleton<RefactoringService>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var repoPath = configuration["Git:RepositoryPath"];
                var repository = sp.GetRequiredService<ICodeAnalysisRepository>();
                
                return new RefactoringService(repoPath!, repository, configuration);
            });
            
            services.AddHostedService<Worker>();
        });

    var host = builder.Build();
    host.Run();
    
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host encerrado inesperadamente");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
