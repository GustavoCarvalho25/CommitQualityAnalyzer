using Microsoft.Extensions.Options;
using RefactorScore.Application.Options;
using RefactorScore.Core.Interfaces;

namespace RefactorScore.WorkerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IGitRepository _gitRepository;
    private readonly IAnalisadorCodigo _analisadorCodigo;
    private readonly ILLMService _llmService;
    private readonly GitOptions _gitOptions;
    private readonly IConfiguration _configuration;
    private readonly int _intervaloMinutos;
    private const int RECOMMENDED_CHANGES_NUMBER = 3;

    public Worker(
        ILogger<Worker> logger,
        IGitRepository gitRepository,
        IAnalisadorCodigo analisadorCodigo,
        ILLMService llmService,
        IOptions<GitOptions> gitOptions,
        IConfiguration configuration)
    {
        _logger = logger;
        _gitRepository = gitRepository;
        _analisadorCodigo = analisadorCodigo;
        _llmService = llmService;
        _gitOptions = gitOptions.Value;
        _configuration = configuration;
        
        _intervaloMinutos = _configuration.GetValue<int>("Worker:IntervaloMinutos", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RefactorScore Worker iniciado em: {time}", DateTimeOffset.Now);
        
        try
        {
            _logger.LogInformation("Verificando disponibilidade do serviço LLM...");
            bool llmDisponivel = await _llmService.IsAvailableAsync();
            
            if (!llmDisponivel)
            {
                _logger.LogError("Serviço LLM não está disponível. Verifique se o Ollama está em execução.");
                return;
            }
            
            _logger.LogInformation("Serviço LLM está disponível");
            
            var modelos = await _llmService.ObterModelosDisponiveisAsync();
            _logger.LogInformation("Modelos disponíveis: {Modelos}", string.Join(", ", modelos));
            
            _logger.LogInformation("Verificando repositório Git em: {CaminhoRepositorio}", _gitOptions.RepositoryPath);
            bool repoValido = await _gitRepository.ValidarRepositorioAsync(_gitOptions.RepositoryPath);
            
            if (!repoValido)
            {
                _logger.LogError("Repositório Git não é válido ou não existe em: {CaminhoRepositorio}", 
                    _gitOptions.RepositoryPath);
                return;
            }
            
            _logger.LogInformation("Repositório Git válido encontrado");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecutarAnaliseAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao executar análise: {Mensagem}", ex.Message);
                }
                
                _logger.LogInformation("Aguardando {Intervalo} minutos até a próxima execução...", 
                    _intervaloMinutos);
                await Task.Delay(TimeSpan.FromMinutes(_intervaloMinutos), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro fatal no Worker: {Mensagem}", ex.Message);
        }
    }
    
    private async Task ExecutarAnaliseAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando análise em: {time}", DateTimeOffset.Now);
        
        try
        {
            int diasAnalise = _configuration.GetValue<int>("Analise:QuantidadeDias", 1);
            int maxCommits = _configuration.GetValue<int>("Analise:MaximoCommits", 5);
            int maxFiles = _configuration.GetValue<int>("Analise:MaximoArquivosPorCommit", 5);
            
            _logger.LogInformation("Buscando commits dos últimos {Dias} dias (máximo {MaxCommits})", 
                diasAnalise, maxCommits);
            
            var commits = await _gitRepository.ObterCommitsPorPeriodoAsync(
                DateTime.UtcNow.AddDays(-diasAnalise), 
                DateTime.UtcNow);
            
            if (!commits.Any())
            {
                _logger.LogWarning("Nenhum commit encontrado no período analisado");
                return;
            }
            
            var commitsParaAnalise = commits;
            
            if (commits.Count > maxCommits)
            {
                _logger.LogInformation("Encontrados {Total} commits, limitando aos {Limite} mais recentes", 
                    commits.Count, maxCommits);
                commitsParaAnalise = commits.Take(maxCommits).ToList();
            }
            else
            {
                _logger.LogInformation("Encontrados {Total} commits para análise", commits.Count);
            }
            
            int commitAtual = 0;
            foreach (var commit in commitsParaAnalise)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;
                
                commitAtual++;
                _logger.LogInformation("Analisando commit {Atual}/{Total}: {CommitId} - {Mensagem}", 
                    commitAtual, commitsParaAnalise.Count, commit.Id.Substring(0, 7), commit.Mensagem);
                
                try
                {
                    var mudancas = await _gitRepository.ObterMudancasNoCommitAsync(commit.Id);
                    
                    var arquivosCodigo = mudancas.Where(m => m.EhCodigoFonte).ToList();
                    
                    if (arquivosCodigo.Count == 0)
                    {
                        _logger.LogInformation("Commit {CommitId} não contém mudanças em arquivos de código fonte", 
                            commit.Id.Substring(0, 7));
                        continue;
                    }
                    
                    if (arquivosCodigo.Count > maxFiles)
                    {
                        _logger.LogInformation("Commit com muitos arquivos ({Total}), apenas 5 serão analisados", 
                            arquivosCodigo.Count);
                    }
                    else
                    {
                        _logger.LogInformation("Arquivos de código no commit: {Total}", arquivosCodigo.Count);
                    }
                    
                    var analise = await _analisadorCodigo.AnalisarCommitAsync(commit.Id);
                    
                    _logger.LogInformation("Análise do commit {CommitId} concluída", commit.Id.Substring(0, 7));
                    _logger.LogInformation("Nota geral: {Nota:F1}, Arquivos analisados: {Arquivos}, Recomendações: {Recomendacoes}", 
                        analise.NotaGeral, analise.AnalisesDeArquivos.Count, analise.Recomendacoes.Count);
                    
                    if (analise.Recomendacoes.Any())
                    {
                        _logger.LogInformation("Recomendações principais:");
                        foreach (var recomendacao in analise.Recomendacoes.Take(RECOMMENDED_CHANGES_NUMBER))
                        {
                            _logger.LogInformation("  • {Titulo} ({Prioridade})", 
                                recomendacao.Titulo, recomendacao.Prioridade);
                        }
                        
                        if (analise.Recomendacoes.Count > RECOMMENDED_CHANGES_NUMBER)
                        {
                            _logger.LogInformation($"  • ... e mais {analise.Recomendacoes.Count - RECOMMENDED_CHANGES_NUMBER} recomendações");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao analisar commit {CommitId}: {Mensagem}", 
                        commit.Id.Substring(0, 7), ex.Message);
                }
            }
            
            _logger.LogInformation("Análise de commits concluída em: {time}", DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar análise: {Mensagem}", ex.Message);
        }
    }
}
