using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RefactorScore.Core.Entities;

namespace RefactorScore.Core.Interfaces
{
    /// <summary>
    /// Interface para o repositório de análises de commits
    /// </summary>
    public interface IAnaliseRepository : IRepository<AnaliseDeCommit>
    {
        /// <summary>
        /// Obtém todas as análises para um commit específico
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <returns>Lista de análises</returns>
        Task<List<AnaliseDeCommit>> ObterAnalisesPorCommitAsync(string commitId);
        
        /// <summary>
        /// Obtém todas as análises para um autor específico
        /// </summary>
        /// <param name="autor">Nome do autor</param>
        /// <returns>Lista de análises</returns>
        Task<List<AnaliseDeCommit>> ObterAnalisesPorAutorAsync(string autor);
        
        /// <summary>
        /// Obtém todas as análises realizadas em um período específico
        /// </summary>
        /// <param name="dataInicio">Data de início</param>
        /// <param name="dataFim">Data de fim</param>
        /// <returns>Lista de análises</returns>
        Task<List<AnaliseDeCommit>> ObterAnalisesPorPeriodoAsync(DateTime dataInicio, DateTime dataFim);
        
        /// <summary>
        /// Obtém a análise mais recente para um commit
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <returns>Análise mais recente ou null se não encontrada</returns>
        Task<AnaliseDeCommit> ObterAnaliseRecentePorCommitAsync(string commitId);
        
        /// <summary>
        /// Obtém análises com nota geral acima de um valor mínimo
        /// </summary>
        /// <param name="notaMinima">Nota mínima</param>
        /// <returns>Lista de análises</returns>
        Task<List<AnaliseDeCommit>> ObterAnalisesPorNotaMinimaAsync(double notaMinima);
        
        /// <summary>
        /// Salva uma análise de arquivo específica
        /// </summary>
        /// <param name="analiseArquivo">Análise de arquivo</param>
        /// <returns>True se salvo com sucesso, False caso contrário</returns>
        Task<bool> SalvarAnaliseArquivoAsync(AnaliseDeArquivo analiseArquivo);
        
        /// <summary>
        /// Obtém análises de arquivo para um commit específico
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <returns>Lista de análises de arquivo</returns>
        Task<List<AnaliseDeArquivo>> ObterAnalisesArquivoPorCommitAsync(string commitId);
    }
} 