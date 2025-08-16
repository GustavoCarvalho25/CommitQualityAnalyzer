using RefactorScore.Core.Entities;

namespace RefactorScore.Core.Interfaces
{
    public interface IGitRepository
    {
        Task<List<Commit>> ObterCommitsPorPeriodoAsync(DateTime? dataInicio = null, DateTime? dataFim = null);
        Task<List<Commit>> ObterCommitsUltimosDiasAsync(int dias);
        Task<Commit?> ObterCommitPorIdAsync(string commitId);
        Task<List<MudancaDeArquivoNoCommit>> ObterMudancasNoCommitAsync(string commitId);
        Task<string?> ObterConteudoArquivoNoCommitAsync(string commitId, string caminhoArquivo);
        Task<string?> ObterDiffArquivoAsync(string commitIdAntigo, string commitIdNovo, string caminhoArquivo);
        Task<string?> ObterDiffCommitAsync(string commitId);
        Task<bool> ValidarRepositorioAsync(string caminho);
    }
} 