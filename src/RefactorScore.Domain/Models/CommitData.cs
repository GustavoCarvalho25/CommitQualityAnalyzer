namespace RefactorScore.Domain.Models;

public class CommitData
{
    public string Id { get; set; }
    public string Author { get; set; }
    public string Email { get; set; }
    public DateTime Date { get; set; }
    public string Message { get; set; }
    public string MessageShort { get; set; }
    public List<FileChange> Changes { get; set; } = new();
    public int TotalAddedLines => Changes.Sum(f => f.AddedLines);
    public int TotalRemovedLines => Changes.Sum(f => f.RemovedLines);
}