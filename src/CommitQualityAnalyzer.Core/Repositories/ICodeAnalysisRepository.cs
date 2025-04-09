using CommitQualityAnalyzer.Core.Models;

namespace CommitQualityAnalyzer.Core.Repositories
{
    public interface ICodeAnalysisRepository
    {
        Task SaveAnalysisAsync(CodeAnalysis analysis);
        Task<IEnumerable<CodeAnalysis>> GetAnalysesByCommitIdAsync(string commitId);
        Task<IEnumerable<CodeAnalysis>> GetAnalysesByDateRangeAsync(DateTime start, DateTime end);
        Task<CodeAnalysis> GetAnalysisByCommitAndFileAsync(string commitId, string filePath);
    }
}
