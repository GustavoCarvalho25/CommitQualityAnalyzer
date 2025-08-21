using Microsoft.Extensions.Logging;
using RefactorScore.Domain.Entities;
using RefactorScore.Domain.Exceptions;
using RefactorScore.Domain.Models;
using RefactorScore.Domain.Repositories;
using RefactorScore.Domain.Services;
using RefactorScore.Domain.ValueObjects;

namespace RefactorScore.Application.Services;

public class CommitAnalysisService : ICommitAnalysisService
{
    private ICommitAnalysisRepository _repository;
    private ILogger<CommitAnalysisService> _logger;
    private IGitServiceFacade _gitRepository;
    private ILLMService _illmService;

    public CommitAnalysisService(ICommitAnalysisRepository repository, ILogger<CommitAnalysisService> logger, IGitServiceFacade gitRepository, ILLMService illmService)
    {
        _repository = repository;
        _logger = logger;
        _gitRepository = gitRepository;
        _illmService = illmService;
    }

    public async Task<CommitAnalysis> AnalyzeCommitAsync(string commitId)
    {
        var existing = await _repository.GetByCommitIdAsync(commitId);
        
        if (existing != null)
        {
            _logger.LogInformation("Commit analysis already exists for commit {CommitId}", commitId);
            return existing;
        }
        
        var commitData = await _gitRepository.GetCommitByIdAsync(commitId);

        if (commitData == null)
        {
            throw new DomainException($"Commit with id {commitId} not found");
        }
        
        var filesChanges = await _gitRepository.GetCommitChangesAsync(commitId);
        commitData.Changes = filesChanges;
        
        var analysis = new CommitAnalysis(
            commitData.Id,
            commitData.Author,
            commitData.Email,
            commitData.Date,
            DateTime.UtcNow,
            DetermineOverallLanguage(filesChanges),
            filesChanges.Sum(f => f.AddedLines),
            filesChanges.Sum(f => f.RemovedLines)
        );

        foreach (var file in filesChanges.Where(c => c.IsSourceCode))
        {
            var commitFile = new CommitFile(
                file.Path,
                file.AddedLines,
                file.RemovedLines,
                file.Language,
                file.Content
            );
            
            analysis.AddFile(commitFile);
            
            var llmResult = await _illmService.AnalyzeFileAsync(file.Content);
            
            var rating = new CleanCodeRating(
                llmResult.VariableScore,
                llmResult.FunctionScore,
                llmResult.CommentScore,
                llmResult.CohesionScore,
                llmResult.DeadCodeScore,
                llmResult.Justifications
            );
            
            var llmSuggestions = await _illmService.GenerateSuggestionsAsync(file.Content, rating);
            
            var suggestions = llmSuggestions
                .Select(s => new Suggestion(
                    s.Title,
                    s.Description,
                    s.Priority,
                    s.Type,
                    s.Difficulty,
                    file.Path,
                    DateTime.UtcNow,
                    s.StudyResources
                )).ToList();
            
            analysis.CompleteFileAnalysis(file.Path, rating, suggestions);
        }
        
        await _repository.AddAsync(analysis);
        return analysis;
    }

    private string DetermineOverallLanguage(List<FileChange> filesChanges)
    {
        if (!filesChanges.Any()) return "Unknown";
        
        var languageCounts = filesChanges
            .Where(f => f.IsSourceCode)
            .GroupBy(f => f.Language)
            .ToDictionary(g => g.Key, g => g.Count());
        
        return languageCounts.OrderByDescending(kv => kv.Value).First().Key;
    }
}