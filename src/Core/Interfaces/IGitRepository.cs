using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RefactorScore.Core.Entities;

namespace RefactorScore.Core.Interfaces
{
    /// <summary>
    /// Interface para acesso a funcionalidades do repositório Git
    /// </summary>
    public interface IGitRepository
    {
        /// <summary>
        /// Obtém os commits realizados nas últimas 24 horas
        /// </summary>
        /// <returns>Lista de informações básicas dos commits</returns>
        Task<IEnumerable<CommitInfo>> GetLastDayCommitsAsync();
        
        /// <summary>
        /// Obtém um commit específico pelo seu ID
        /// </summary>
        /// <param name="commitId">ID (SHA) do commit</param>
        /// <returns>Informações do commit ou null se não encontrado</returns>
        Task<CommitInfo> GetCommitByIdAsync(string commitId);
        
        /// <summary>
        /// Obtém as mudanças detalhadas de um commit específico
        /// </summary>
        /// <param name="commitId">ID (SHA) do commit</param>
        /// <returns>Lista de mudanças em arquivos</returns>
        Task<IEnumerable<CommitFileChange>> GetCommitChangesAsync(string commitId);
        
        /// <summary>
        /// Obtém o conteúdo de um arquivo em uma revisão específica
        /// </summary>
        /// <param name="commitId">ID (SHA) do commit</param>
        /// <param name="filePath">Caminho do arquivo</param>
        /// <returns>Conteúdo do arquivo</returns>
        Task<string> GetFileContentAtRevisionAsync(string commitId, string filePath);
        
        /// <summary>
        /// Obtém o diff de um arquivo específico em um commit
        /// </summary>
        /// <param name="commitId">ID (SHA) do commit</param>
        /// <param name="filePath">Caminho do arquivo</param>
        /// <returns>Texto do diff</returns>
        Task<string> GetFileDiffAsync(string commitId, string filePath);
    }
} 