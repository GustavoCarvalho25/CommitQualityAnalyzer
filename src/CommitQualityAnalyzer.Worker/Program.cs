using CommitQualityAnalyzer.Worker.Services;
using CommitQualityAnalyzer.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace CommitQualityAnalyzer.Worker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Configurações são carregadas automaticamente do appsettings.json pelo CreateApplicationBuilder
            
            // Registrar serviços
            builder.Services.AddSingleton<ICodeAnalysisRepository, MongoCodeAnalysisRepository>();
            builder.Services.AddSingleton(sp => new CommitAnalyzerService(
                sp.GetRequiredService<IConfiguration>().GetValue<string>("GitRepository:Path"),
                sp.GetRequiredService<ILogger<CommitAnalyzerService>>(),
                sp.GetRequiredService<ICodeAnalysisRepository>(),
                sp.GetRequiredService<IConfiguration>()
            ));

            var host = builder.Build();

            // Executar a análise
            var analyzer = host.Services.GetRequiredService<CommitAnalyzerService>();
            try
            {
                await analyzer.AnalyzeLastDayCommits();
            }
            catch (Exception ex)
            {
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Erro ao analisar commits");
            }

            await host.RunAsync();
        }
    }
}
