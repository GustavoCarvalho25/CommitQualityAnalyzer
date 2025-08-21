using RefactorScore.Domain.Exceptions;
using RefactorScore.Domain.SeedWork;
using RefactorScore.Domain.ValueObjects;

namespace RefactorScore.Domain.Entities;

public class CommitAnalysis : Entity, IAggregateRoot
{
    public string CommitId { get; private set; }
    public string Author { get; private set; }
    public string Email { get; private set; }
    public DateTime CommitDate { get; private set; }
    public DateTime AnalysisDate { get; private set; }
    public string Language { get; private set; }
    public int AddedLines { get; private set; }
    public int RemovedLines { get; private set; }

    private readonly List<CommitFile> _files = new();
    private readonly List<Suggestion> _suggestions = new();
    
    public IReadOnlyList<CommitFile> Files => _files.AsReadOnly();
    public IReadOnlyList<Suggestion> Suggestions => _suggestions.AsReadOnly();
    
    public CleanCodeRating? Rating => CalculateOverallRating();
    
    public double OverallNote => Rating?.Note ?? 0.0;

    private CleanCodeRating CalculateOverallRating()
    {
        if (!_files.Any(f => f.HasAnalysis)) return null;
        
        var analyzedFiles = _files.Where(f => f.HasAnalysis).ToList();
        
        return new CleanCodeRating(
            (int)analyzedFiles.Average(f => f.Rating.VariableNaming),
            (int)analyzedFiles.Average(f => f.Rating.FunctionSizes),
            (int)analyzedFiles.Average(f => f.Rating.NoNeedsComments),
            (int)analyzedFiles.Average(f => f.Rating.MethodCohesion),
            (int)analyzedFiles.Average(f => f.Rating.DeadCode),
            analyzedFiles
                .Where(f => f.HasAnalysis)
                .SelectMany(f => f.Rating.Justifies)
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(g => g.Key, g => g.First().Value)
        );
    }
    
    public CommitAnalysis(string commitId, string author, string email, DateTime commitDate, DateTime analysisDate, string language, int addedLines, int removedLines)
    {
        CommitId = commitId;
        Author = author;
        Email = email;
        CommitDate = commitDate;
        AnalysisDate = analysisDate;
        Language = language;
        AddedLines = addedLines;
        RemovedLines = removedLines;
    }
    
    public void AddFile(CommitFile file)
    {
        if (_files.Any(f => f.Path == file.Path))
            throw new DomainException($"File {file.Path} already exists in this analysis");
            
        _files.Add(file);
    }
    
    public void AddSuggestion(Suggestion suggestion) => _suggestions.Add(suggestion);
    
    public void CompleteFileAnalysis(string filePath, CleanCodeRating rating, List<Suggestion> suggestions)
    {
        var file = _files.FirstOrDefault(f => f.Path == filePath);
        if (file == null)
            throw new DomainException($"File {filePath} not found in this analysis");
            
        file.SetAnalysis(rating, suggestions);
        _suggestions.AddRange(suggestions);
    }
}