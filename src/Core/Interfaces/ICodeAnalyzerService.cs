using System.Collections.Generic;
using System.Threading.Tasks;
using RefactorScore.Core.Entities;

namespace RefactorScore.Core.Interfaces
{
    /// <summary>
    /// Interface para o serviço de análise de qualidade de código
    /// </summary>
    public interface ICodeAnalyzerService
    {
        /// <summary>
        /// Analisa um commit específico
        /// </summary>
        /// <param name="commitId">ID do commit a ser analisado</param>
        /// <returns>Lista de análises geradas para os arquivos do commit</returns>
        Task<IEnumerable<CodeAnalysis>> AnalyzeCommitAsync(string commitId);
        
        /// <summary>
        /// Analisa os commits das últimas 24 horas
        /// </summary>
        /// <returns>Lista de análises geradas para todos os commits</returns>
        Task<IEnumerable<CodeAnalysis>> AnalyzeLastDayCommitsAsync();
        
        /// <summary>
        /// Analisa um arquivo específico dentro de um commit
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <param name="filePath">Caminho do arquivo</param>
        /// <returns>Análise do arquivo ou null em caso de erro</returns>
        Task<CodeAnalysis> AnalyzeFileInCommitAsync(string commitId, string filePath);
        
        /// <summary>
        /// Processa e agrega múltiplas análises parciais em uma análise final
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <param name="partialAnalyses">Lista de análises parciais</param>
        /// <returns>Análise agregada</returns>
        Task<CodeAnalysis> AggregatePartialAnalysesAsync(string commitId, IEnumerable<CodeAnalysis> partialAnalyses);
    }
} 