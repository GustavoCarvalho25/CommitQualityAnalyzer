namespace RefactorScore.Domain.Models;

public class LLMSuggestion
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string Priority { get; set; }
    public string Type { get; set; }
    public string Difficulty { get; set; }
    public List<string> StudyResources { get; set; } = new();
}