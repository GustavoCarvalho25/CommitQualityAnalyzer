using CommitQualityAnalyzer.Worker.Services;
using CommitQualityAnalyzer.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace CommitQualityAnalyzer.Worker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configurar Serilog
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                    .Build())
                .CreateLogger();

            try
            {
                Log.Information("Iniciando CommitQualityAnalyzer");

                var builder = Host.CreateApplicationBuilder(args);

                // Configurar Serilog como provedor de log
                builder.Services.AddLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.AddSerilog(dispose: true);
                });

                // Registrar serviços
                builder.Services.AddSingleton<ICodeAnalysisRepository, MongoCodeAnalysisRepository>();
                builder.Services.AddSingleton(sp => new CommitAnalyzerService(
                    sp.GetRequiredService<IConfiguration>().GetValue<string>("GitRepository:Path"),
                    sp.GetRequiredService<ILogger<CommitAnalyzerService>>(),
                    sp.GetRequiredService<ICodeAnalysisRepository>(),
                    sp.GetRequiredService<IConfiguration>()
                ));

                // Em .NET 8, a configuração do Serilog é feita diretamente nos serviços
                builder.Logging.AddSerilog(dispose: true);

                var host = builder.Build();

                // Executar a análise
                var analyzer = host.Services.GetRequiredService<CommitAnalyzerService>();
                try
                {
                    Log.Information("Iniciando análise de commits das últimas 24 horas");
                    await analyzer.AnalyzeLastDayCommits();
                    Log.Information("Análise de commits concluída com sucesso");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Erro ao analisar commits");
                }

                await host.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Aplicação encerrada inesperadamente");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
