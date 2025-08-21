using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using RefactorScore.Domain.Enum;
using RefactorScore.Domain.Models;

namespace RefactorScore.Infrastructure.Mappers;

public partial class GitMapper
{
    private readonly ILogger<GitMapper> _logger;

    public GitMapper(ILogger<GitMapper> logger)
    {
        _logger = logger;
    }
    
    public CommitData MapCommitToCommitData(Commit libGitCommit)
    {
        return new CommitData
        {
            Id = libGitCommit.Sha,
            Author = libGitCommit.Author.Name,
            Email = libGitCommit.Author.Email,
            Date = libGitCommit.Author.When.DateTime,
            Message = libGitCommit.Message,
            MessageShort = libGitCommit.MessageShort
            
        };
    }

    public List<FileChange> MapCommitChangesToFileChanges(Commit libGitCommit, Repository repo)
    {
        var changes = new List<FileChange>();

        if (libGitCommit.Parents.Count() == 0)
        {
            changes.AddRange(MapTreeToFileChanges(libGitCommit.Tree, FileChangeType.Added, repo));
        }
        else
        {
            var parent = libGitCommit.Parents.First();
            var patch = repo.Diff.Compare<Patch>(parent.Tree, libGitCommit.Tree);

            changes.AddRange(MapPatchToFileChanges(patch));
        }

        return changes;
    }

    private List<FileChange> MapTreeToFileChanges(Tree tree, FileChangeType type, Repository repo)
    {
        var changes = new List<FileChange>();

        try
        {
            foreach (var entry in tree)
            {
                if (entry.TargetType == TreeEntryTargetType.Blob)
                {
                    var blob = (Blob)entry.Target;
                    var content = blob.GetContentText();
                    
                    var lines = content?.Split('\n') ?? Array.Empty<string>();
                    var addedLines = lines.Length;
                    
                    var change = new FileChange
                    {
                        Path = entry.Path,
                        Language = DetermineLanguage(entry.Path),
                        AddedLines = addedLines,
                        RemovedLines = 0,
                        ChangeType = type,
                        IsSourceCode = IsSourceCode(entry.Path),
                        Content = content
                    };
                    
                    changes.Add(change);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error mapping tree to file changes");
        }
        
        return changes;
    }

    private List<FileChange> MapPatchToFileChanges(Patch patch)
    {
        var changes = new List<FileChange>();

        foreach (var patchEntryChange in patch)
        {
            var change = new FileChange
            {
                Path = patchEntryChange.Path,
                Language = DetermineLanguage(patchEntryChange.Path),
                AddedLines = patchEntryChange.LinesAdded,
                RemovedLines = patchEntryChange.LinesDeleted,
                ChangeType = MapChangeType(patchEntryChange.Status),
                IsSourceCode = IsSourceCode(patchEntryChange.Path)
            };
            
            if (patchEntryChange.Status != ChangeKind.Deleted)
            {
                change.Content = GetFileContentDiff(patchEntryChange);
            }
            
            changes.Add(change);
        }
        
        return changes;
    }

    private string DetermineLanguage(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return "Unknown";
        
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
    
        return extension switch
        {
            ".cs" => "C#",
            ".java" => "Java",
            ".js" => "JavaScript",
            ".ts" => "TypeScript",
            ".py" => "Python",
            ".rb" => "Ruby",
            ".php" => "PHP",
            ".go" => "Go",
            ".c" => "C",
            ".cpp" => "C++",
            ".h" => "C/C++",
            ".swift" => "Swift",
            ".kt" => "Kotlin",
            ".rs" => "Rust",
            ".sh" => "Shell",
            ".pl" => "Perl",
            ".sql" => "SQL",
            ".html" => "HTML",
            ".css" => "CSS",
            ".scss" => "SCSS",
            ".less" => "LESS",
            ".xml" => "XML",
            ".json" => "JSON",
            ".yaml" => "YAML",
            ".yml" => "YAML",
            _ => extension.TrimStart('.').ToUpperInvariant()
        };
    }
    
    private FileChangeType MapChangeType(ChangeKind changeKind)
    {
        return changeKind switch
        {
            ChangeKind.Added => FileChangeType.Added,
            ChangeKind.Modified => FileChangeType.Modified,
            ChangeKind.Deleted => FileChangeType.Deleted,
            ChangeKind.Renamed => FileChangeType.Renamed,
            _ => FileChangeType.Modified
        };
    }

    private bool IsSourceCode(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var sourceCodeExtensions = new HashSet<string>
        {
            ".cs", ".java", ".js", ".ts", ".py", ".rb", ".php", ".go",
            ".c", ".cpp", ".cc", ".cxx", ".h", ".hpp", ".hxx",
            ".swift", ".kt", ".rs", ".scala", ".clj", ".hs", ".ml",
            ".pl", ".pm", ".r", ".m", ".mm", ".f", ".f90", ".f95",

            ".html", ".htm", ".css", ".scss", ".sass", ".less",
            ".jsx", ".tsx", ".vue", ".svelte",

            ".sh", ".bash", ".zsh", ".fish", ".ps1", ".bat", ".cmd",
            ".sql", ".xml", ".json", ".yaml", ".yml", ".toml",

            ".dart",

            ".lua", ".vim", ".el", ".lisp", ".scm", ".rkt"
        };
        
        return sourceCodeExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());
    }

    private string GetFileContentDiff(PatchEntryChanges patchEntry)
    {
        try
        {
            if (patchEntry.Status == ChangeKind.Deleted)
                return string.Empty;
            
            var patch = patchEntry.Patch;
            if (string.IsNullOrEmpty(patch))
                return string.Empty;
            
            var lines = patch.Split("\n");
            var diffContent = new List<string>();
            
            bool inHunk = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("@@"))
                {
                    inHunk = true;
                    diffContent.Add(line);
                    continue;
                }
                
                if (!inHunk) continue;
                
                if (line.StartsWith("-") || line.StartsWith("+") || line.StartsWith(" "))
                {
                    diffContent.Add(line);
                }
                else if (line.StartsWith("\\"))
                {
                    diffContent.Add(line);
                }
            }
            
            return string.Join("\n", diffContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file content: {Message}", ex.Message);
            return string.Empty;
        }
    }
}