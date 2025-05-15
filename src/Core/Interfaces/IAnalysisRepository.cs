using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RefactorScore.Core.Entities;

namespace RefactorScore.Core.Interfaces
{
    /// <summary>
    /// Interface para o repositório de persistência de análises de código
    /// </summary>
    public interface IAnalysisRepository
    {
        /// <summary>
        /// Obtém todas as análises armazenadas
        /// </summary>
        /// <returns>Lista de todas as análises</returns>
        Task<IEnumerable<CodeAnalysis>> GetAllAnalysesAsync();
        
        /// <summary>
        /// Salva uma análise de código no repositório
        /// </summary>
        /// <param name="analysis">Análise a ser salva</param>
        /// <returns>ID da análise salva</returns>
        Task<string> SaveAnalysisAsync(CodeAnalysis analysis);
        
        /// <summary>
        /// Obtém uma análise pelo seu ID
        /// </summary>
        /// <param name="id">ID da análise</param>
        /// <returns>Análise encontrada ou null se não existir</returns>
        Task<CodeAnalysis> GetAnalysisByIdAsync(string id);
        
        /// <summary>
        /// Obtém análises por ID de commit
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <returns>Lista de análises para o commit</returns>
        Task<IEnumerable<CodeAnalysis>> GetAnalysesByCommitIdAsync(string commitId);
        
        /// <summary>
        /// Obtém uma análise específica para um commit e arquivo
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <param name="filePath">Caminho do arquivo</param>
        /// <returns>Análise encontrada ou null se não existir</returns>
        Task<CodeAnalysis> GetAnalysisByCommitAndFileAsync(string commitId, string filePath);
        
        /// <summary>
        /// Obtém análises realizadas em um período de tempo
        /// </summary>
        /// <param name="startDate">Data de início</param>
        /// <param name="endDate">Data de fim</param>
        /// <returns>Lista de análises no período</returns>
        Task<IEnumerable<CodeAnalysis>> GetAnalysesByDateRangeAsync(DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Atualiza uma análise existente
        /// </summary>
        /// <param name="analysis">Análise com as alterações</param>
        /// <returns>True se a atualização foi bem-sucedida</returns>
        Task<bool> UpdateAnalysisAsync(CodeAnalysis analysis);
        
        /// <summary>
        /// Remove uma análise pelo seu ID
        /// </summary>
        /// <param name="id">ID da análise a ser removida</param>
        /// <returns>True se a remoção foi bem-sucedida</returns>
        Task<bool> DeleteAnalysisAsync(string id);
    }
} 