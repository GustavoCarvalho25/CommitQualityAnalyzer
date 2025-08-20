using Microsoft.Extensions.Diagnostics.HealthChecks;
using RefactorScore.CrossCutting.IoC.DependenceInjection;
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
    Log.Information("Starting RefactorScore Worker");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

    builder.Services.AddRefactorScoreServices(builder.Configuration);

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    using (var scope = host.Services.CreateScope())
    {
        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();
        var healthReport = await healthCheckService.CheckHealthAsync();
        
        if (healthReport.Status != HealthStatus.Healthy)
        {
            Log.Fatal("Health check failed, shutting down.");
            
            foreach (var (key, value) in healthReport.Entries)
            {
                if (value.Status != HealthStatus.Healthy)
                {
                    Log.Fatal("  • {Service}: {Status} - {Description}", 
                        key, value.Status, value.Description);

                    if (value.Exception != null)
                    {
                        Log.Fatal("    Exception: {Exception}", value.Exception.Message);
                    }
                }
            }
            
            return 1;
        }
        
        Log.Information("All health checks passed successfully");
        
        foreach (var (key, value) in healthReport.Entries)
        {
            Log.Information("  • {Service}: {Status}", key, value.Status);
        }
    }
    
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