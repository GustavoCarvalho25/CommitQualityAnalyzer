using CrossCutting.IoC.DependenceInjection;
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
    Log.Information("🚀 Starting RefactorScore Worker");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    builder.Services.AddRefactorScoreServices(builder.Configuration);

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();
    
    Log.Information("Services configured successfully, starting execution");
    await host.RunAsync();
    
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Fatal error during Worker initialization");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}