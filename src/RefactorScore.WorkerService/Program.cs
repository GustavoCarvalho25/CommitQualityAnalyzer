using RefactorScore.Application.ServiceProviders;
using RefactorScore.Infrastructure.MongoDB;
using RefactorScore.WorkerService;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/refactorscore-.log", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("🚀 Iniciando RefactorScore Worker");

    var builder = Host.CreateApplicationBuilder(args);

    // Usar Serilog
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    // Adicionar serviços do Git
    builder.Services.AddGitServices(builder.Configuration);

    // Adicionar serviços LLM
    builder.Services.AddLLMServices(builder.Configuration);
    
    // Adicionar serviços MongoDB
    builder.Services.AddMongoDb(builder.Configuration);

    // Adicionar worker
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();
    
    Log.Information("✅ Serviços configurados, iniciando execução");
    host.Run();
    
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Erro fatal na inicialização do Worker");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
