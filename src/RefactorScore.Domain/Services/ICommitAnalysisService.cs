using RefactorScore.Domain.Entities;

namespace RefactorScore.Domain.Services;

public interface ICommitAnalysisService
{
    public Task<CommitAnalysis> AnalyzeCommitAsync(string commitId);
    public Task<bool> CheckLLMConnectionAsync();
    public Task<bool> CheckMongoConnectionAsync();
}