using RefactorScore.Domain.Models;
using RefactorScore.Domain.ValueObjects;

namespace RefactorScore.Domain.Services;

public interface ILLMService
{
    Task<LLMAnalysisResult> AnalyzeFileAsync(string filePath);
    Task<List<LLMSuggestion>> GenerateSuggestionsAsync(string fileContent, CleanCodeRating rating);
}