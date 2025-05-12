using RefactorScore.Application;
using RefactorScore.Infrastructure;
using RefactorScore.WorkerService;
using RefactorScore.WorkerService.Workers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configurar Serilog para logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

try
{
    Log.Information("Iniciando RefactorScore Worker Service");

    // Configurar logging com Serilog
    builder.Services.AddLogging(loggingBuilder =>
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddSerilog(dispose: true);
    });

    // Adicionar configurações do Worker
    builder.Services.Configure<WorkerOptions>(
        builder.Configuration.GetSection("Worker")
    );

    // Registrar as camadas de aplicação e infraestrutura
    builder.Services
        .AddApplicationServices(builder.Configuration)
        .AddInfrastructureServices(builder.Configuration);

    // Registrar o Worker
    builder.Services.AddHostedService<CommitAnalysisWorker>();

    var host = builder.Build();

    await host.RunAsync();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "O RefactorScore Worker Service falhou ao iniciar");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
