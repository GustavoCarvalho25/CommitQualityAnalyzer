using CommitQualityAnalyzer.Worker.Services;
using CommitQualityAnalyzer.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace CommitQualityAnalyzer.Worker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configurar o Serilog
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "commit-analyzer-.log");
            
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("Iniciando CommitQualityAnalyzer");

                var builder = Host.CreateApplicationBuilder(args);

                // Adicionar Serilog
                builder.Services.AddSerilog();

                // Configurações são carregadas automaticamente do appsettings.json pelo CreateApplicationBuilder
                
                // Registrar serviços
                builder.Services.AddSingleton<ICodeAnalysisRepository, MongoCodeAnalysisRepository>();
                builder.Services.AddSingleton(sp => new CommitAnalyzerService(
                    sp.GetRequiredService<IConfiguration>().GetValue<string>("GitRepository:Path"),
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
