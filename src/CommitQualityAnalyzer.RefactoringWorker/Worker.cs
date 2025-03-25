using CommitQualityAnalyzer.RefactoringWorker.Services;
using Serilog;

namespace CommitQualityAnalyzer.RefactoringWorker;

public class Worker : BackgroundService
{
    private readonly RefactoringService _refactoringService;
    private readonly IConfiguration _configuration;

    public Worker(RefactoringService refactoringService, IConfiguration configuration)
    {
        _refactoringService = refactoringService;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Log.Information("Worker de Refatoração iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                Log.Information("Iniciando geração de propostas de refatoração");
                
                try
                {
                    await _refactoringService.GenerateRefactoringProposalsForLastDay();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Erro ao gerar propostas de refatoração");
                }
                
                // Aguardar 24 horas antes da próxima execução
                Log.Information("Próxima execução em 24 horas");
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro não tratado no worker de refatoração");
        }
        finally
        {
            Log.Information("Worker de Refatoração finalizado");
        }
    }
}
