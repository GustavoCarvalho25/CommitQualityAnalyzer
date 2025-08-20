using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using RefactorScore.Domain.Models;
using RefactorScore.Domain.Services;
using RefactorScore.Infrastructure.Mappers;

namespace RefactorScore.Infrastructure.Services;

public class GitServiceFacade : IGitServiceFacade
{
    private readonly string _repositoryPath;
    private readonly GitMapper _mapper;
    private readonly ILogger<GitServiceFacade> _logger;

    public GitServiceFacade(string repositoryPath, GitMapper mapper, ILogger<GitServiceFacade> logger)
    {
        _repositoryPath = repositoryPath;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<CommitData?> GetCommitByIdAsync(string commitId)
    {
        if (string.IsNullOrEmpty(commitId))
        {
            _logger.LogInformation("Commit id is null or empty");
            return null;
        }
        
        try
        {
            _logger.LogInformation("Getting commit data for: {CommitId}", commitId);

            return await Task.Run(() =>
            {
                if (!Repository.IsValid(_repositoryPath))
                {
                    _logger.LogInformation("Repository path: {RepositoryPath} is not valid", _repositoryPath);
                    throw new InvalidOperationException($"Invalid repository path: {_repositoryPath}");
                }
                
                using var repo = new Repository(_repositoryPath);
                var libGitCommit = repo.Lookup<Commit>(commitId);
                
                if (libGitCommit == null)
                {
                    _logger.LogWarning("Commit not found: {CommitId}", commitId);
                    return null;
                }

                return _mapper.MapCommitToCommitData(libGitCommit);
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting commit by id: {CommitId}", commitId);
            throw;
        }
    }

    public async Task<List<CommitData>> GetCommitsByPeriodAsync(DateTime? startDate, DateTime? endDate)
    {
        _logger.LogInformation("Getting commits by period: {StartDate} to {EndDate}", startDate, endDate);
        
        if (!Repository.IsValid(_repositoryPath))
        {
            _logger.LogError("Invalid repository path: {RepositoryPath}", _repositoryPath);
            throw new InvalidOperationException($"Invalid repository path: {_repositoryPath}");
        }

        using var repo = new Repository(_repositoryPath);
            
        var commitFilter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Time,
            IncludeReachableFrom = repo.Head.CanonicalName
        };

        var commits = repo.Commits.QueryBy(commitFilter);
            
        var filteredCommits = commits
            .Where(commit =>
            {
                var commitDate = commit.Author.When.DateTime;
                    
                if (startDate.HasValue && commitDate < startDate.Value)
                    return false;
                        
                if (endDate.HasValue && commitDate > endDate.Value)
                    return false;
                        
                return true;
            })
            .ToList();

        _logger.LogInformation("Found {Count} commits in the specified period", filteredCommits.Count);
            
        return filteredCommits.Select(c => _mapper.MapCommitToCommitData(c)).ToList();
    }

    public async Task<List<FileChange>> GetCommitChangesAsync(string commitId)
    {
        if (string.IsNullOrWhiteSpace(commitId))
        {
            _logger.LogWarning("CommitId is null or empty");
            return new List<FileChange>();
        }

        try
        {
            _logger.LogInformation("Getting commit changes for: {CommitId}", commitId);
            
            return await Task.Run(() =>
            {
                if (!Repository.IsValid(_repositoryPath))
                {
                    _logger.LogError("Invalid repository path: {RepositoryPath}", _repositoryPath);
                    throw new InvalidOperationException($"Invalid repository path: {_repositoryPath}");
                }

                using var repo = new Repository(_repositoryPath);
                var libGitCommit = repo.Lookup<Commit>(commitId);
                
                if (libGitCommit == null)
                {
                    _logger.LogWarning("Commit not found: {CommitId}", commitId);
                    return new List<FileChange>();
                }

                return _mapper.MapCommitChangesToFileChanges(libGitCommit, repo);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting commit changes: {CommitId}", commitId);
            throw;
        }
    }
    
    public Task<bool> ValidateRepositoryAsync()
    {
        try
        {
            bool isValid = Repository.IsValid(_repositoryPath);
            _logger.LogInformation("Repository validation for {RepositoryPath}: {IsValid}", _repositoryPath, isValid);
            return Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating repository: {RepositoryPath}", _repositoryPath);
            return Task.FromResult(false);
        }
    }
}