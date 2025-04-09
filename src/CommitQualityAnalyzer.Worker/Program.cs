using CommitQualityAnalyzer.Worker.Services;
using CommitQualityAnalyzer.Worker.Services.CommitAnalysis;
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
                builder.Services.AddHttpClient();
                
                // Usar o caminho correto para o arquivo de configuração
                var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (!File.Exists(appSettingsPath))
                {
                    // Tentar encontrar o arquivo no diretório do projeto
                    appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                    if (!File.Exists(appSettingsPath))
                    {
                        // Tentar encontrar o arquivo no diretório do projeto Worker
                        var workerDir = Path.GetDirectoryName(typeof(Program).Assembly.Location);
                        appSettingsPath = Path.Combine(workerDir, "appsettings.json");
                    }
                }
                
                Log.Information("Usando arquivo de configuração: {ConfigPath}", appSettingsPath);
                
                if (File.Exists(appSettingsPath))
                {
                    builder.Configuration.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true);
                }
                else
                {
                    Log.Warning("Arquivo de configuração não encontrado. Usando valores padrão.");
                }
                
                // Depurar configuração
                foreach (var configItem in builder.Configuration.AsEnumerable())
                {
                    Log.Information("Config: {Key} = {Value}", configItem.Key, configItem.Value);
                }
                
                // Registrar serviços de análise de commits
                var repoPath = builder.Configuration.GetValue<string>("GitRepository:Path");
                Log.Information("Caminho do repositório: {RepoPath}", repoPath);
                
                if (string.IsNullOrEmpty(repoPath))
                {
                    // Usar um caminho padrão se não estiver configurado
                    repoPath = Directory.GetCurrentDirectory();
                    Log.Warning("Caminho do repositório não configurado. Usando o diretório atual: {RepoPath}", repoPath);
                }
                builder.Services.AddCommitAnalysisServices(repoPath);

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
