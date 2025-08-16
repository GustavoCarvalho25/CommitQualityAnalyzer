using RefactorScore.Core.Entities;

namespace RefactorScore.Core.Interfaces;

public interface IAnalisadorCodigo
{
    Task<AnaliseDeCommit> AnalisarCommitAsync(string commitId);
}