using CommitQualityAnalyzer.Core.Models;
using CommitQualityAnalyzer.Core.Repositories;
using CommitQualityAnalyzer.Worker.Services.CommitAnalysis.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Serviço principal para análise de qualidade de commits
    /// </summary>
    public class CommitAnalyzerService
    {
        private readonly string _repoPath;
        private readonly ILogger<CommitAnalyzerService> _logger;
        private readonly ICodeAnalysisRepository _repository;
        private readonly IConfiguration _configuration;
        private readonly GitDiffService _gitDiffService;
        private readonly OllamaService _ollamaService;
        private readonly ResponseAnalysisService _responseAnalysisService;
        private readonly PromptBuilderService _promptBuilderService;
        private readonly AnalysisMapperService _analysisMapperService;
        private readonly CommitSchedulerService _commitSchedulerService;
        private readonly GitRepositoryWrapper _gitRepositoryWrapper;
        private readonly string _defaultModelName;

        public CommitAnalyzerService(
            string repoPath,
            ILogger<CommitAnalyzerService> logger,
            ICodeAnalysisRepository repository,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<GitDiffService> gitDiffLogger,
            ILogger<OllamaService> ollamaLogger,
            ILogger<ResponseAnalysisService> responseAnalysisLogger,
            ILogger<PromptBuilderService> promptBuilderLogger,
            ILogger<AnalysisMapperService> analysisMapperLogger,
            ILogger<CommitSchedulerService> commitSchedulerLogger,
            ILogger<GitRepositoryWrapper> gitRepositoryWrapperLogger)
        {
            _repoPath = repoPath;
            _logger = logger;
            _repository = repository;
            _configuration = configuration;
            _defaultModelName = _configuration.GetValue<string>("Ollama:ModelName", "codellama");
            
            // Inicializar serviços auxiliares
            _gitRepositoryWrapper = new GitRepositoryWrapper(gitRepositoryWrapperLogger, repoPath);
            _gitDiffService = new GitDiffService(gitDiffLogger, _gitRepositoryWrapper, repoPath);
            _ollamaService = new OllamaService(ollamaLogger, configuration, httpClientFactory.CreateClient());
            _responseAnalysisService = new ResponseAnalysisService(responseAnalysisLogger);
            _promptBuilderService = new PromptBuilderService(promptBuilderLogger);
            _analysisMapperService = new AnalysisMapperService(analysisMapperLogger);
            _commitSchedulerService = new CommitSchedulerService(repoPath, commitSchedulerLogger, _gitRepositoryWrapper);
        }

        /// <summary>
        /// Analisa os commits do último dia
        /// </summary>
        public async Task AnalyzeLastDayCommits()
        {
            _logger.LogInformation("Iniciando análise do repositório: {RepoPath}", _repoPath);
            
            // Obter commits do último dia
            var commits = _commitSchedulerService.GetLastDayCommits();
            
            // Processar cada commit
            await _commitSchedulerService.ProcessCommitsWithLogging(commits, async (commit) => {
                await ProcessCommitFiles(commit);
            });
        }

        /// <summary>
        /// Analisa um commit específico
        /// </summary>
        public async Task AnalyzeCommit(string commitId)
        {
            _logger.LogInformation("Iniciando análise do commit específico: {CommitId}", commitId);
            
            // Obter commit pelo ID
            var commit = _commitSchedulerService.GetCommitById(commitId);
            
            if (commit == null)
            {
                return;
            }
            
            // Processar o commit
            await ProcessCommitFiles(commit);
        }
        
        /// <summary>
        /// Processa os arquivos modificados em um commit
        /// </summary>
        private async Task ProcessCommitFiles(CommitInfo commit)
        {
            try
            {
                // Obter as mudanças do commit usando o wrapper do repositório Git
                var changes = _gitRepositoryWrapper.GetCommitChangesWithDiff(commit.Sha);
                
                _logger.LogInformation("Encontradas {ChangeCount} alterações no commit {CommitId}", 
                    changes.Count, commit.Sha.Substring(0, Math.Min(8, commit.Sha.Length)));
                
                // Filtrar apenas arquivos C#
                var csharpFiles = changes
                    .Where(c => Path.GetExtension(c.FilePath).ToLower() == ".cs")
                    .ToList();
                
                _logger.LogInformation("Encontrados {FileCount} arquivos C# para analisar", csharpFiles.Count);
                
                // Processar cada arquivo modificado sequencialmente
                foreach (var change in csharpFiles)
                {
                    // Verificar se o arquivo já foi analisado para este commit
                    var existingAnalysis = await _repository.GetAnalysisByCommitAndFileAsync(commit.Sha, change.FilePath);
                    if (existingAnalysis != null)
                    {
                        _logger.LogInformation("Arquivo {FilePath} já foi analisado anteriormente para o commit {CommitId}", 
                            change.FilePath, commit.Sha.Substring(0, Math.Min(8, commit.Sha.Length)));
                        continue;
                    }
                    
                    _logger.LogInformation("Analisando arquivo: {FilePath}", change.FilePath);
                    
                    try
                    {
                        // Verificar se o arquivo é muito grande (tamanho em bytes ou conteúdo)
                        int maxFileSizeBytes = _configuration.GetValue<int>("Analysis:MaxFileSizeBytes", 500000); // 500KB por padrão
                        int maxContentLength = _configuration.GetValue<int>("Analysis:MaxContentLength", 50000); // 50K caracteres por padrão
                        
                        if (change.FileSize > maxFileSizeBytes)
                        {
                            _logger.LogWarning("Arquivo {FilePath} muito grande para análise. Tamanho: {Size} bytes (limite: {MaxSize} bytes)", 
                                change.FilePath, change.FileSize, maxFileSizeBytes);
                            continue;
                        }
                        
                        if (change.ModifiedContent.Length > maxContentLength || change.OriginalContent.Length > maxContentLength)
                        {
                            _logger.LogWarning("Conteúdo do arquivo {FilePath} muito grande para análise. Tamanho: {Size} caracteres (limite: {MaxSize} caracteres)", 
                                change.FilePath, Math.Max(change.ModifiedContent.Length, change.OriginalContent.Length), maxContentLength);
                            continue;
                        }
                        
                        // Verificar se há conteúdo para analisar
                        if (string.IsNullOrWhiteSpace(change.DiffText))
                        {
                            _logger.LogInformation("Arquivo {FilePath} não possui diferenças significativas para análise", change.FilePath);
                            continue;
                        }
                        
                        // Analisar as diferenças do arquivo
                        var analysis = await AnalyzeCodeDiff(change, commit);
                        
                        // Mapear e salvar a análise no repositório
                        var codeAnalysis = _analysisMapperService.MapToCodeAnalysis(analysis, commit, change.FilePath);
                        await _repository.SaveAnalysisAsync(codeAnalysis);
                        
                        _logger.LogInformation("Análise salva com sucesso para {FilePath} no commit {CommitId}", 
                            change.FilePath, commit.Sha.Substring(0, Math.Min(8, commit.Sha.Length)));
                        
                        // Aguardar um pouco antes de processar o próximo arquivo para evitar sobrecarga
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao analisar arquivo {FilePath}: {ErrorMessage}", 
                            change.FilePath, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar commit {CommitId}: {ErrorMessage}", 
                    commit.Sha, ex.Message);
            }
        }

        /// <summary>
        /// Analisa as diferenças de código em um arquivo
        /// </summary>
        private async Task<CommitAnalysisResult> AnalyzeCodeDiff(CommitChangeInfo change, CommitInfo commit)
        {
            _logger.LogInformation("Analisando diferenças para {FilePath}", change.FilePath);
            
            try
            {
                // Verificar se temos diferenças para analisar
                if (string.IsNullOrEmpty(change.DiffText))
                {
                    _logger.LogWarning("Sem diferenças significativas para analisar em {FilePath}", change.FilePath);
                    return _responseAnalysisService.CreateFallbackAnalysisResult();
                }
                
                // Registrar informações detalhadas para debug
                _logger.LogDebug("Analisando arquivo {FilePath} com {ChangeType}, tamanho: {FileSize} bytes, conteúdo modificado: {ContentLength} caracteres", 
                    change.FilePath, change.ChangeType, change.FileSize, change.ModifiedContent.Length);
                
                // Construir o prompt para análise
                var prompt = _promptBuilderService.BuildDiffAnalysisPrompt(change.FilePath, change.DiffText, commit);
                
                // Testar conexão com o modelo
                var modelName = _configuration.GetValue<string>("Ollama:ModelName", "codellama");
                var isConnected = await _ollamaService.TestOllamaConnection(modelName);
                
                if (!isConnected)
                {
                    _logger.LogError("Não foi possível conectar ao modelo Ollama");
                    return _responseAnalysisService.CreateFallbackAnalysisResult();
                }
                
                // Processar o prompt com o modelo
                var response = await _ollamaService.ProcessPrompt(prompt, modelName);
                
                if (string.IsNullOrEmpty(response))
                {
                    _logger.LogError("Resposta vazia do modelo Ollama");
                    return _responseAnalysisService.CreateFallbackAnalysisResult();
                }
                
                // Converter a resposta em um objeto estruturado
                var analysisResult = _responseAnalysisService.ConvertTextResponseToJson(response);
                
                _logger.LogInformation("Análise concluída com sucesso para {FilePath}", change.FilePath);
                
                return analysisResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar diferenças para {FilePath}: {ErrorMessage}", 
                    change.FilePath, ex.Message);
                return _responseAnalysisService.CreateFallbackAnalysisResult();
            }
        }
    }
}
