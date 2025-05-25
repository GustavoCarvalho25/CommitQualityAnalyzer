using System.Collections.Generic;
using System.Threading.Tasks;
using RefactorScore.Core.Entities;

namespace RefactorScore.Core.Interfaces
{
    /// <summary>
    /// Interface para o serviço de análise de código
    /// </summary>
    public interface IAnalisadorCodigo
    {
        /// <summary>
        /// Analisa um commit completo
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <returns>Análise do commit</returns>
        Task<AnaliseDeCommit> AnalisarCommitAsync(string commitId);
        
        /// <summary>
        /// Analisa um arquivo específico em um commit
        /// </summary>
        /// <param name="commitId">ID do commit</param>
        /// <param name="caminhoArquivo">Caminho do arquivo</param>
        /// <returns>Análise do arquivo</returns>
        Task<AnaliseDeArquivo> AnalisarArquivoNoCommitAsync(string commitId, string caminhoArquivo);
        
        /// <summary>
        /// Analisa os commits recentes
        /// </summary>
        /// <param name="quantidadeDias">Quantidade de dias para analisar (padrão: 1)</param>
        /// <param name="limitarQuantidade">Limitar quantidade de commits (padrão: 10)</param>
        /// <returns>Lista de análises de commits</returns>
        Task<List<AnaliseDeCommit>> AnalisarCommitsRecentesAsync(int quantidadeDias = 1, int limitarQuantidade = 10);
        
        /// <summary>
        /// Gera uma análise temporal baseada em commits
        /// </summary>
        /// <param name="autor">Autor dos commits (opcional)</param>
        /// <param name="dias">Período em dias (padrão: 30)</param>
        /// <returns>Análise temporal</returns>
        Task<AnaliseTemporal> GerarAnaliseTemporalAsync(string? autor = null, int dias = 30);
        
        /// <summary>
        /// Gera recomendações baseadas em uma análise de commit
        /// </summary>
        /// <param name="analiseCommit">Análise do commit</param>
        /// <returns>Lista de recomendações</returns>
        Task<List<Recomendacao>> GerarRecomendacoesAsync(AnaliseDeCommit analiseCommit);
        
        /// <summary>
        /// Avalia a evolução do código ao longo do tempo
        /// </summary>
        /// <param name="autor">Autor dos commits</param>
        /// <param name="dias">Período em dias</param>
        /// <returns>Dicionário com métricas de evolução</returns>
        Task<Dictionary<string, double>> AvaliarEvolucaoAsync(string autor, int dias = 90);
    }
} 