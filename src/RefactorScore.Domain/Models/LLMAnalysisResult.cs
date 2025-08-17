namespace RefactorScore.Domain.Models;

public class LLMAnalysisResult
{
    public int VariableScore { get; set; }
    public int FunctionScore { get; set; }
    public int CommentScore { get; set; }
    public int CohesionScore { get; set; }
    public int DeadCodeScore { get; set; }
    public Dictionary<string, string> Justifications { get; set; } = new();
    public List<LLMSuggestion> Suggestions { get; set; } = new();
}