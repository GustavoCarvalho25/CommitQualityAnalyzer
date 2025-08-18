namespace RefactorScore.CrossCutting.IoC.Configuration;

public class OllamaSettings
{
    public string BaseUrl { get; set; } = String.Empty;
    public string Model { get; set; } = String.Empty;
    public int TimeoutMinutes { get; set; }
}