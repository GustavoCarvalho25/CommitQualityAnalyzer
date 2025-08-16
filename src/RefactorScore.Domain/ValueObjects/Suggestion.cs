using RefactorScore.Domain.SeedWork;

namespace RefactorScore.Domain.ValueObjects;

public class Suggestion : ValueObject
{
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string Priority { get; private set; }
    public string Type { get; private set; }
    public string Difficult { get; private set; }
    public string FileReference { get; private set; } 
    public DateTime LastUpdate { get; private set; }
    public IReadOnlyList<string> StudyResources { get; private set; }
    
        
    public Suggestion(
        string title,
        string description,
        string priority,
        string type,
        string difficult,
        string fileReference,
        DateTime lastUpdate,
        List<string> studyResources = null
        )
    {
        Title = title;
        Description = description;
        Priority = priority;
        Type = type;
        Difficult = difficult;
        FileReference = fileReference;
        LastUpdate = lastUpdate;
        StudyResources = (studyResources ?? new List<string>()).AsReadOnly();
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Title;
        yield return Description;
        yield return Priority;
        yield return Type;
        yield return Difficult;
        yield return FileReference;
        yield return LastUpdate;
        yield return StudyResources;
    }
}