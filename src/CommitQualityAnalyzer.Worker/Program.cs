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

            // Configurar o caminho do repositório
            var repoPath = "D:\\Estudos\\Projects\\DevFreela";

            // Configurar a conexão com o MongoDB
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"ConnectionStrings:MongoDB", "mongodb://admin:admin123@localhost:27017"}
                })
                .Build();

            builder.Services.AddSingleton<IConfiguration>(configuration);

            // Registrar serviços
            builder.Services.AddSingleton<ICodeAnalysisRepository, MongoCodeAnalysisRepository>();
            builder.Services.AddSingleton(sp => new CommitAnalyzerService(
                repoPath,
                sp.GetRequiredService<ILogger<CommitAnalyzerService>>(),
                sp.GetRequiredService<ICodeAnalysisRepository>()
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
