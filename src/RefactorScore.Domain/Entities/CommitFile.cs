using RefactorScore.Domain.SeedWork;
using RefactorScore.Domain.ValueObjects;

namespace RefactorScore.Domain.Entities;

public class CommitFile : Entity
{
    public string Path { get; private set; }
    public string Language { get; private set; }
    public int AddedLines { get; private set; }
    public int RemovedLines { get; private set; }
    public string Content { get; private set; }
    public bool HasAnalysis => Rating != null;
    public CleanCodeRating? Rating { get; private set; }

    private readonly List<Suggestion> _suggestions = new();
    public IReadOnlyList<Suggestion> Suggestions => _suggestions.AsReadOnly();
    
    public CommitFile(string path, int addedLines, int removedLines, string language, string content)
    {
        Path = path;
        Language = language;
        Content = content;
        AddedLines = addedLines;
        RemovedLines = removedLines;
    }

    public void SetAnalysis(CleanCodeRating rating, List<Suggestion> suggestions) 
    {
        Rating = rating;
        _suggestions.Clear();
        _suggestions.AddRange(suggestions);
    }
}