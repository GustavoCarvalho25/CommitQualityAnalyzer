using RefactorScore.Domain.Models;

namespace RefactorScore.Domain.Services;

public interface IGitServiceFacade
{
    Task<CommitData?> GetCommitByIdAsync(string commitId);
    Task<List<CommitData>> GetCommitsByPeriodAsync(DateTime? startDate, DateTime? endDate);
    Task<List<FileChange>> GetCommitChangesAsync(string commitId);
    Task<bool> ValidateRepositoryAsync();
}