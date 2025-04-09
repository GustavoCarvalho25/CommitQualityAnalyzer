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
                // Obter as mudanças do commit usando o serviço modificado
                var changes = _gitDiffService.GetCommitChangesWithDiff(commit.Sha);
                
                // Processar cada arquivo modificado sequencialmente
                foreach (var change in changes)
                {
                    if (Path.GetExtension(change.Path).ToLower() == ".cs")
                    {
                        _logger.LogInformation("Analisando arquivo: {FilePath}", change.Path);
                        
                        try
                        {
                            // Analisar as diferenças do arquivo
                            var analysis = await AnalyzeCodeDiff(change, commit);
                            
                            // Mapear e salvar a análise no repositório
                            var codeAnalysis = _analysisMapperService.MapToCodeAnalysis(analysis, commit, change.Path);
                            await _repository.SaveAnalysisAsync(codeAnalysis);
                            
                            _logger.LogInformation("Análise salva com sucesso para {FilePath} no commit {CommitId}", 
                                change.Path, commit.Sha.Substring(0, Math.Min(8, commit.Sha.Length)));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao analisar arquivo {FilePath}: {ErrorMessage}", 
                                change.Path, ex.Message);
                        }
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
            _logger.LogInformation("Analisando diferenças para {FilePath}", change.Path);
            
            try
            {
                // Verificar se temos diferenças para analisar
                if (string.IsNullOrEmpty(change.DiffText))
                {
                    _logger.LogWarning("Sem diferenças significativas para analisar em {FilePath}", change.Path);
                    return _responseAnalysisService.CreateFallbackAnalysisResult();
                }
                
                // Construir o prompt para análise
                var prompt = _promptBuilderService.BuildDiffAnalysisPrompt(change.Path, change.DiffText, commit);
                
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
                
                _logger.LogInformation("Análise concluída com sucesso para {FilePath}", change.Path);
                
                return analysisResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar diferenças para {FilePath}: {ErrorMessage}", 
                    change.Path, ex.Message);
                return _responseAnalysisService.CreateFallbackAnalysisResult();
            }
        }
    }
}
