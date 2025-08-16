namespace RefactorScore.Application.Options;

public class GitOptions
{
    public string RepositoryPath { get; set; } = "./repositorio";
    public string DefaultBranch { get; set; } = "main";
    public string UserName { get; set; } = "RefactorScore";
    public string UserEmail { get; set; } = "refactorscore@example.com";
}