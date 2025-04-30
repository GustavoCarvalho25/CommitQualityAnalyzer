using Microsoft.Extensions.Logging;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Serviço responsável por agendar e selecionar commits para análise
    /// </summary>
    public class CommitSchedulerService
    {
        private readonly string _repoPath;
        private readonly ILogger<CommitSchedulerService> _logger;
        private readonly GitRepositoryWrapper _gitRepositoryWrapper;

        public CommitSchedulerService(
            string repoPath,
            ILogger<CommitSchedulerService> logger,
            GitRepositoryWrapper gitRepositoryWrapper,
            CodeChunkerService chunkerService = null)
        {
            _repoPath = repoPath;
            _logger = logger;
            _gitRepositoryWrapper = gitRepositoryWrapper;
        }

        /// <summary>
        /// Obtém os commits do último dia de forma segura
        /// </summary>
        public List<CommitInfo> GetLastDayCommits()
        {
            _logger.LogInformation("Obtendo commits do último dia no repositório: {RepoPath}", _repoPath);
            
            // Usar o wrapper seguro para evitar erros de violação de acesso
            var commits = _gitRepositoryWrapper.GetLastDayCommits();
            
            _logger.LogInformation("Encontrados {CommitCount} commits nas últimas 24 horas", commits.Count);
            
            return commits;
        }

        /// <summary>
        /// Obtém informações de um commit específico pelo ID
        /// </summary>
        public CommitInfo GetCommitById(string commitId)
        {
            _logger.LogInformation("Obtendo commit específico: {CommitId}", commitId);
            
            return _gitRepositoryWrapper.GetCommitInfoById(commitId);
        }

        /// <summary>
        /// Executa uma ação para cada commit com contexto de log apropriado
        /// </summary>
        public async Task ProcessCommitsWithLogging(IEnumerable<CommitInfo> commits, Func<CommitInfo, Task> processAction)
        {
            foreach (var commit in commits)
            {
                using (LogContext.PushProperty("CommitId", commit.Sha))
                using (LogContext.PushProperty("Author", commit.AuthorName))
                using (LogContext.PushProperty("CommitDate", commit.AuthorDate))
                {
                    _logger.LogInformation("Processando commit: {CommitId} de {Author} em {CommitDate}", 
                        commit.Sha.Substring(0, Math.Min(8, commit.Sha.Length)), commit.AuthorName, commit.AuthorDate);
                    
                    try
                    {
                        await processAction(commit);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar commit {CommitId}: {ErrorMessage}", 
                            commit.Sha, ex.Message);
                    }
                }
            }
        }
    }
}
