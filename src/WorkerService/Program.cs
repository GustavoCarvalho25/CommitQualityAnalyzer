using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RefactorScore.Application;
using RefactorScore.Application.Services;
using RefactorScore.Core.Interfaces;
using RefactorScore.Infrastructure;
using RefactorScore.Infrastructure.Ollama;
using RefactorScore.WorkerService;
using RefactorScore.WorkerService.Workers;
using Serilog;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

// Configurar Serilog para logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("[STARTUP] Starting RefactorScore Worker Service");
    Log.Information("[STARTUP] Environment: {Environment}", 
        builder.Environment.EnvironmentName);

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

    Log.Information("[STARTUP] Loading configurations...");
    
    // Log key configuration values
    var ollamaConfig = builder.Configuration.GetSection("Ollama");
    Log.Information("[STARTUP_CONFIG] Ollama Settings: BaseUrl={BaseUrl}, Model={Model}, Timeout={Timeout}s", 
        ollamaConfig["BaseUrl"], 
        ollamaConfig["DefaultModel"],
        ollamaConfig["Timeout"]);
    
    var mongoConfig = builder.Configuration.GetSection("MongoDB");
    Log.Information("[STARTUP_CONFIG] MongoDB Settings: Database={Database}, Collection={Collection}", 
        mongoConfig["DatabaseName"], 
        mongoConfig["CollectionName"]);
    
    var analyzerConfig = builder.Configuration.GetSection("CodeAnalyzer");
    Log.Information("[STARTUP_CONFIG] CodeAnalyzer Settings: Model={Model}, MaxCodeLength={MaxCodeLength} chars, MaxDiffLength={MaxDiffLength} chars", 
        analyzerConfig["ModelName"], 
        analyzerConfig["MaxCodeLength"], 
        analyzerConfig["MaxDiffLength"]);
    
    var workerConfig = builder.Configuration.GetSection("Worker");
    Log.Information("[STARTUP_CONFIG] Worker Settings: ScanInterval={ScanInterval}min, MaxCommits={MaxCommits}, MaxFiles={MaxFiles}", 
        workerConfig["ScanIntervalMinutes"], 
        workerConfig["MaxProcessingCommits"], 
        workerConfig["MaxFilesPerCommit"]);

    // Registrar as camadas de aplicação e infraestrutura
    Log.Information("[STARTUP] Registering services...");
    builder.Services
        .AddApplicationServices(builder.Configuration)
        .AddInfrastructureServices(builder.Configuration);

    // Registrar o Worker
    builder.Services.AddHostedService<CommitAnalysisWorker>();

    Log.Information("[STARTUP] Building service provider...");
    var host = builder.Build();

    // Log initial service health check
    using (var scope = host.Services.CreateScope())
    {
        var serviceProvider = scope.ServiceProvider;
        
        Log.Information("[STARTUP_CHECK] Checking services availability...");
        
        try
        {
            var llmService = serviceProvider.GetRequiredService<ILLMService>();
            var isLlmAvailable = await llmService.IsAvailableAsync();
            Log.Information("[STARTUP_CHECK] LLM Service availability: {IsAvailable}", isLlmAvailable);
            
            if (isLlmAvailable)
            {
                var models = await llmService.GetAvailableModelsAsync();
                Log.Information("[STARTUP_CHECK] Available LLM models: {Models}", 
                    string.Join(", ", models));
                
                var ollamaOptions = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>();
                var modelName = ollamaOptions.Value.DefaultModel;
                var isModelAvailable = models.Contains(modelName);
                Log.Information("[STARTUP_CHECK] Required model '{Model}' availability: {IsAvailable}", 
                    modelName, isModelAvailable);
            }
            
            var cacheService = serviceProvider.GetRequiredService<ICacheService>();
            var isCacheAvailable = await cacheService.IsAvailableAsync();
            Log.Information("[STARTUP_CHECK] Cache Service availability: {IsAvailable}", isCacheAvailable);
            
            var repoService = serviceProvider.GetRequiredService<IAnalysisRepository>();
            Log.Information("[STARTUP_CHECK] Analysis Repository service loaded");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[STARTUP_CHECK] Error during service check");
        }
    }

    Log.Information("[STARTUP] Starting host...");
    await host.RunAsync();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "[STARTUP_FAIL] The RefactorScore Worker Service failed to start");
    return 1;
}
finally
{
    Log.Information("[SHUTDOWN] RefactorScore Worker Service is shutting down");
    Log.CloseAndFlush();
}
