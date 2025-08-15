using RefactorScore.Application.ServiceProviders;
using RefactorScore.Infrastructure.MongoDB;
using RefactorScore.WorkerService;
using Serilog;

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
    Log.Information("Iniciando RefactorScore Worker");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    builder.Services.AddGitServices(builder.Configuration);
    builder.Services.AddLLMServices(builder.Configuration);
    builder.Services.AddMongoDb(builder.Configuration);

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();
    
    using (var scope = host.Services.CreateScope())
    {
        var serviceProvider = scope.ServiceProvider;
        
        Log.Information("Verificando conexão com banco de dados MongoDB...");
        bool conexaoMongoDB = await serviceProvider.VerificarConexaoMongoDbAsync();
        
        if (!conexaoMongoDB)
        {
            Log.Fatal("ERRO CRÍTICO: Não foi possível conectar ao MongoDB. Verifique se o servidor está rodando e as credenciais estão corretas.");
            return 1;
        }
    }
    
    Log.Information("Serviços configurados, iniciando execução");
    host.Run();
    
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Erro fatal na inicialização do Worker");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
