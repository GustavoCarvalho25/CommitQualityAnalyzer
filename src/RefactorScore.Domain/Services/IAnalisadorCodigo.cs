using RefactorScore.Domain.Entities;

namespace RefactorScore.Domain.Interfaces;

public interface IAnalisadorCodigo
{
    Task<AnaliseDeCommit> AnalisarCommitAsync(string commitId);
}