using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RefactorScore.Core.Entities;

namespace RefactorScore.Core.Interfaces
{
    /// <summary>
    /// Interface para interação com repositório Git
    /// </summary>
    public interface IGitRepository
    {
        /// <summary>
        /// Obtém os commits realizados em um período específico
        /// </summary>
        /// <param name="dataInicio">Data de início (opcional)</param>
        /// <param name="dataFim">Data de fim (opcional)</param>
        /// <returns>Lista de commits</returns>
        Task<List<Commit>> ObterCommitsPorPeriodoAsync(DateTime? dataInicio = null, DateTime? dataFim = null);
        
        /// <summary>
        /// Obtém os commits realizados nos últimos N dias
        /// </summary>
        /// <param name="dias">Número de dias anteriores</param>
        /// <returns>Lista de commits</returns>
        Task<List<Commit>> ObterCommitsUltimosDiasAsync(int dias);
        
        /// <summary>
        /// Obtém os detalhes de um commit específico
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <returns>Detalhes do commit ou null se não encontrado</returns>
        Task<Commit?> ObterCommitPorIdAsync(string commitId);
        
        /// <summary>
        /// Obtém as mudanças de arquivos em um commit
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <returns>Lista de mudanças de arquivos</returns>
        Task<List<MudancaDeArquivoNoCommit>> ObterMudancasNoCommitAsync(string commitId);
        
        /// <summary>
        /// Obtém o conteúdo de um arquivo em um commit específico
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <param name="caminhoArquivo">Caminho do arquivo</param>
        /// <returns>Conteúdo do arquivo ou null se não encontrado</returns>
        Task<string?> ObterConteudoArquivoNoCommitAsync(string commitId, string caminhoArquivo);
        
        /// <summary>
        /// Obtém o diff de um arquivo entre dois commits
        /// </summary>
        /// <param name="commitIdAntigo">ID do commit antigo</param>
        /// <param name="commitIdNovo">ID do commit novo</param>
        /// <param name="caminhoArquivo">Caminho do arquivo</param>
        /// <returns>Texto do diff</returns>
        Task<string?> ObterDiffArquivoAsync(string commitIdAntigo, string commitIdNovo, string caminhoArquivo);
        
        /// <summary>
        /// Obtém o diff completo de um commit
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <returns>Texto do diff</returns>
        Task<string?> ObterDiffCommitAsync(string commitId);
        
        /// <summary>
        /// Verifica se um caminho especificado é um repositório Git válido
        /// </summary>
        /// <param name="caminho">Caminho a ser verificado</param>
        /// <returns>True se for um repositório Git válido, False caso contrário</returns>
        Task<bool> ValidarRepositorioAsync(string caminho);
    }
} 