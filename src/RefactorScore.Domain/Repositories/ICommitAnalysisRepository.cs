using RefactorScore.Domain.Entities;

namespace RefactorScore.Domain.Repositories;

public interface ICommitAnalysisRepository
{
    Task<CommitAnalysis?> GetByIdAsync(string id);
    Task<CommitAnalysis?> GetByCommitIdAsync(string commitId);
    Task<List<CommitAnalysis>> GetAllAsync();
    Task AddAsync(CommitAnalysis commitAnalysis);
    Task UpdateAsync(CommitAnalysis commitAnalysis);
    Task DeleteAsync(string id);
}