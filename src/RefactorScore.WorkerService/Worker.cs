using Microsoft.Extensions.Options;
using RefactorScore.Application.ServiceProviders;
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
        
        // Obter intervalo de execução da configuração (padrão: 30 minutos)
        _intervaloMinutos = _configuration.GetValue<int>("Worker:IntervaloMinutos", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("✅ RefactorScore Worker iniciado em: {time}", DateTimeOffset.Now);
        
        try
        {
            // Verificar disponibilidade do LLM antes de começar
            _logger.LogInformation("🔍 Verificando disponibilidade do serviço LLM...");
            bool llmDisponivel = await _llmService.IsAvailableAsync();
            
            if (!llmDisponivel)
            {
                _logger.LogError("❌ Serviço LLM não está disponível. Verifique se o Ollama está em execução.");
                return;
            }
            
            _logger.LogInformation("✅ Serviço LLM está disponível");
            
            // Modelos disponíveis
            var modelos = await _llmService.ObterModelosDisponiveisAsync();
            _logger.LogInformation("📋 Modelos disponíveis: {Modelos}", string.Join(", ", modelos));
            
            // Verificar repositório Git
            _logger.LogInformation("🔍 Verificando repositório Git em: {CaminhoRepositorio}", _gitOptions.RepositoryPath);
            bool repoValido = await _gitRepository.ValidarRepositorioAsync(_gitOptions.RepositoryPath);
            
            if (!repoValido)
            {
                _logger.LogError("❌ Repositório Git não é válido ou não existe em: {CaminhoRepositorio}", 
                    _gitOptions.RepositoryPath);
                return;
            }
            
            _logger.LogInformation("✅ Repositório Git válido encontrado");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecutarAnaliseAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro ao executar análise: {Mensagem}", ex.Message);
                }
                
                // Aguardar intervalo configurado
                _logger.LogInformation("⏱️ Aguardando {Intervalo} minutos até a próxima execução...", 
                    _intervaloMinutos);
                await Task.Delay(TimeSpan.FromMinutes(_intervaloMinutos), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro fatal no Worker: {Mensagem}", ex.Message);
        }
    }
    
    private async Task ExecutarAnaliseAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Iniciando análise em: {time}", DateTimeOffset.Now);
        
        try
        {
            // Obter commits recentes (último dia, máximo 5)
            int diasAnalise = _configuration.GetValue<int>("Analise:QuantidadeDias", 1);
            int maxCommits = _configuration.GetValue<int>("Analise:MaximoCommits", 5);
            
            _logger.LogInformation("🔍 Buscando commits dos últimos {Dias} dias (máximo {MaxCommits})", 
                diasAnalise, maxCommits);
            
            var commits = await _gitRepository.ObterCommitsPorPeriodoAsync(
                DateTime.UtcNow.AddDays(-diasAnalise), 
                DateTime.UtcNow);
            
            if (commits == null || !commits.Any())
            {
                _logger.LogWarning("⚠️ Nenhum commit encontrado no período analisado");
                return;
            }
            
            // Limitar quantidade se necessário
            var commitsParaAnalise = commits;
            if (commits.Count > maxCommits)
            {
                _logger.LogInformation("📋 Encontrados {Total} commits, limitando aos {Limite} mais recentes", 
                    commits.Count, maxCommits);
                commitsParaAnalise = commits.Take(maxCommits).ToList();
            }
            else
            {
                _logger.LogInformation("📋 Encontrados {Total} commits para análise", commits.Count);
            }
            
            // Analisar cada commit
            int commitAtual = 0;
            foreach (var commit in commitsParaAnalise)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;
                
                commitAtual++;
                _logger.LogInformation("🔎 Analisando commit {Atual}/{Total}: {CommitId} - {Mensagem}", 
                    commitAtual, commitsParaAnalise.Count, commit.Id.Substring(0, 7), commit.Mensagem);
                
                try
                {
                    // Obter mudanças para verificar quantidade de arquivos
                    var mudancas = await _gitRepository.ObterMudancasNoCommitAsync(commit.Id);
                    
                    // Filtrar apenas arquivos de código fonte
                    var arquivosCodigo = mudancas.Where(m => m.EhCodigoFonte).ToList();
                    
                    if (arquivosCodigo.Count == 0)
                    {
                        _logger.LogInformation("ℹ️ Commit {CommitId} não contém mudanças em arquivos de código fonte", 
                            commit.Id.Substring(0, 7));
                        continue;
                    }
                    
                    // Log da quantidade de arquivos
                    if (arquivosCodigo.Count > 5)
                    {
                        _logger.LogInformation("ℹ️ Commit com muitos arquivos ({Total}), apenas 5 serão analisados", 
                            arquivosCodigo.Count);
                    }
                    else
                    {
                        _logger.LogInformation("ℹ️ Arquivos de código no commit: {Total}", arquivosCodigo.Count);
                    }
                    
                    // Analisar o commit
                    var analise = await _analisadorCodigo.AnalisarCommitAsync(commit.Id);
                    
                    // Log do resultado da análise
                    _logger.LogInformation("✅ Análise do commit {CommitId} concluída", commit.Id.Substring(0, 7));
                    _logger.LogInformation("📊 Nota geral: {Nota:F1}, Arquivos analisados: {Arquivos}, Recomendações: {Recomendacoes}", 
                        analise.NotaGeral, analise.AnalisesDeArquivos.Count, analise.Recomendacoes.Count);
                    
                    // Log das recomendações principais (top 3)
                    if (analise.Recomendacoes.Any())
                    {
                        _logger.LogInformation("🔄 Recomendações principais:");
                        foreach (var recomendacao in analise.Recomendacoes.Take(3))
                        {
                            _logger.LogInformation("  • {Titulo} ({Prioridade})", 
                                recomendacao.Titulo, recomendacao.Prioridade);
                        }
                        
                        if (analise.Recomendacoes.Count > 3)
                        {
                            _logger.LogInformation("  • ... e mais {Quantidade} recomendações", 
                                analise.Recomendacoes.Count - 3);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro ao analisar commit {CommitId}: {Mensagem}", 
                        commit.Id.Substring(0, 7), ex.Message);
                }
            }
            
            _logger.LogInformation("✅ Análise de commits concluída em: {time}", DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao executar análise: {Mensagem}", ex.Message);
        }
    }
}
