using RefactorScore.Domain.Entities;

namespace RefactorScore.Domain.Interfaces;

public interface IAnaliseRepository : IRepository<AnaliseDeCommit>
{
    Task<AnaliseDeCommit> ObterAnaliseRecentePorCommitAsync(string commitId);
    Task<bool> SalvarAnaliseArquivoAsync(AnaliseDeArquivo analiseArquivo);
    Task<List<AnaliseDeArquivo>> ObterAnalisesArquivoPorCommitAsync(string commitId);
}