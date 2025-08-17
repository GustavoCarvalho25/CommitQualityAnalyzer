using RefactorScore.Domain.Enum;

namespace RefactorScore.Domain.Models;

public class FileChange
{
    public string Path { get; set; }
    public string Language { get; set; }
    public int AddedLines { get; set; }
    public int RemovedLines { get; set; }
    public string Content { get; set; }
    public FileChangeType ChangeType { get; set; }
    public bool IsSourceCode { get; set; }
}