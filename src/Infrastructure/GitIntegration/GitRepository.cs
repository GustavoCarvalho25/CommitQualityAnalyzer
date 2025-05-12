using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RefactorScore.Core.Entities;
using RefactorScore.Core.Interfaces;
using FileChangeType = RefactorScore.Core.Entities.FileChangeType;

namespace RefactorScore.Infrastructure.GitIntegration
{
    public class GitRepository : IGitRepository
    {
        private readonly string _repositoryPath;
        private readonly ILogger<GitRepository> _logger;
        private readonly GitRepositoryOptions _options;

        public GitRepository(
            IOptions<GitRepositoryOptions> options,
            ILogger<GitRepository> logger)
        {
            _options = options.Value;
            _repositoryPath = _options.RepositoryPath;
            _logger = logger;
            
            if (!Directory.Exists(_repositoryPath))
            {
                throw new DirectoryNotFoundException($"O diretório do repositório Git não foi encontrado: {_repositoryPath}");
            }

            if (!Repository.IsValid(_repositoryPath))
            {
                throw new InvalidOperationException($"O diretório não é um repositório Git válido: {_repositoryPath}");
            }
        }

        /// <inheritdoc />
        public Task<IEnumerable<CommitInfo>> GetLastDayCommitsAsync()
        {
            try
            {
                _logger.LogInformation("Obtendo commits das últimas 24 horas do repositório: {RepositoryPath}", _repositoryPath);
                
                using var repo = new Repository(_repositoryPath);
                var yesterday = DateTimeOffset.Now.AddDays(-1);
                
                var commits = repo.Commits
                    .Where(c => c.Author.When >= yesterday)
                    .Select(MapToCommitInfo)
                    .ToList();
                
                _logger.LogInformation("Encontrados {CommitCount} commits nas últimas 24 horas", commits.Count);
                
                return Task.FromResult<IEnumerable<CommitInfo>>(commits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter commits das últimas 24 horas");
                throw;
            }
        }

        /// <inheritdoc />
        public Task<CommitInfo> GetCommitByIdAsync(string commitId)
        {
            try
            {
                _logger.LogInformation("Obtendo commit com ID: {CommitId}", commitId);
                
                using var repo = new Repository(_repositoryPath);
                var commit = repo.Lookup<Commit>(commitId);
                
                if (commit == null)
                {
                    _logger.LogWarning("Commit não encontrado: {CommitId}", commitId);
                    return Task.FromResult<CommitInfo>(null);
                }
                
                var result = MapToCommitInfo(commit);
                
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter commit por ID: {CommitId}", commitId);
                throw;
            }
        }

        /// <inheritdoc />
        public Task<IEnumerable<CommitFileChange>> GetCommitChangesAsync(string commitId)
        {
            try
            {
                _logger.LogInformation("Obtendo alterações do commit: {CommitId}", commitId);
                
                using var repo = new Repository(_repositoryPath);
                var commit = repo.Lookup<Commit>(commitId);
                
                if (commit == null)
                {
                    _logger.LogWarning("Commit não encontrado: {CommitId}", commitId);
                    return Task.FromResult<IEnumerable<CommitFileChange>>(new List<CommitFileChange>());
                }
                
                var parentCommit = commit.Parents.FirstOrDefault();
                var changes = new List<CommitFileChange>();
                
                if (parentCommit == null)
                {
                    // Para o primeiro commit do repositório
                    var treeChanges = repo.Diff.Compare<TreeChanges>(null, commit.Tree);
                    changes = MapTreeChangesToFileChanges(repo, treeChanges, commit);
                }
                else
                {
                    var treeChanges = repo.Diff.Compare<TreeChanges>(parentCommit.Tree, commit.Tree);
                    changes = MapTreeChangesToFileChanges(repo, treeChanges, commit, parentCommit);
                }
                
                _logger.LogInformation("Encontradas {ChangeCount} alterações no commit {CommitId}", 
                    changes.Count, commitId.Substring(0, Math.Min(8, commitId.Length)));
                
                return Task.FromResult<IEnumerable<CommitFileChange>>(changes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter alterações do commit: {CommitId}", commitId);
                throw;
            }
        }

        /// <inheritdoc />
        public Task<string> GetFileContentAtRevisionAsync(string commitId, string filePath)
        {
            try
            {
                _logger.LogInformation("Obtendo conteúdo do arquivo {FilePath} na revisão {CommitId}", filePath, commitId);
                
                using var repo = new Repository(_repositoryPath);
                var commit = repo.Lookup<Commit>(commitId);
                
                if (commit == null)
                {
                    _logger.LogWarning("Commit não encontrado: {CommitId}", commitId);
                    return Task.FromResult(string.Empty);
                }
                
                var blob = commit[filePath]?.Target as Blob;
                
                if (blob == null)
                {
                    _logger.LogWarning("Arquivo não encontrado na revisão: {FilePath} em {CommitId}", filePath, commitId);
                    return Task.FromResult(string.Empty);
                }
                
                // Detecta se o arquivo é binário
                if (blob.IsBinary)
                {
                    _logger.LogInformation("Arquivo binário ignorado: {FilePath}", filePath);
                    return Task.FromResult("[ARQUIVO BINÁRIO]");
                }
                
                using var contentStream = blob.GetContentStream();
                using var reader = new StreamReader(contentStream, Encoding.UTF8);
                return Task.FromResult(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter conteúdo do arquivo {FilePath} na revisão {CommitId}", filePath, commitId);
                throw;
            }
        }

        /// <inheritdoc />
        public Task<string> GetFileDiffAsync(string commitId, string filePath)
        {
            try
            {
                _logger.LogInformation("Obtendo diff do arquivo {FilePath} no commit {CommitId}", filePath, commitId);
                
                using var repo = new Repository(_repositoryPath);
                var commit = repo.Lookup<Commit>(commitId);
                
                if (commit == null)
                {
                    _logger.LogWarning("Commit não encontrado: {CommitId}", commitId);
                    return Task.FromResult(string.Empty);
                }
                
                var parentCommit = commit.Parents.FirstOrDefault();
                
                // Para o primeiro commit do repositório
                if (parentCommit == null)
                {
                    var patch = repo.Diff.Compare<Patch>(null, commit.Tree, new List<string> { filePath });
                    return Task.FromResult(patch);
                }
                
                var options = new CompareOptions
                {
                    Similarity = SimilarityOptions.None,
                    IncludeUnmodified = false,
                    ContextLines = 3,
                    InterhunkLines = 0,
                    PathFilter = filePath
                };
                
                var diff = repo.Diff.Compare<Patch>(parentCommit.Tree, commit.Tree, options);
                return Task.FromResult(diff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter diff do arquivo {FilePath} no commit {CommitId}", filePath, commitId);
                throw;
            }
        }

        #region Helper Methods

        private CommitInfo MapToCommitInfo(Commit commit)
        {
            return new CommitInfo
            {
                Id = commit.Sha,
                Author = commit.Author.Name,
                AuthorEmail = commit.Author.Email,
                CommitDate = commit.Author.When.DateTime,
                Message = commit.Message?.Trim()
            };
        }

        private List<CommitFileChange> MapTreeChangesToFileChanges(
            Repository repo, 
            TreeChanges treeChanges, 
            Commit commit, 
            Commit parentCommit = null)
        {
            var changes = new List<CommitFileChange>();
            
            foreach (var change in treeChanges)
            {
                // Filtrar apenas arquivos relevantes (ignorar arquivos binários, etc.)
                if (ShouldIgnoreFile(change.Path))
                {
                    _logger.LogDebug("Ignorando arquivo: {FilePath}", change.Path);
                    continue;
                }
                
                try
                {
                    var fileChange = new CommitFileChange
                    {
                        FilePath = change.Path,
                        ChangeType = MapChangeType(change.Status)
                    };
                    
                    switch (change.Status)
                    {
                        case LibGit2Sharp.ChangeKind.Added:
                            fileChange.OriginalContent = string.Empty;
                            fileChange.ModifiedContent = GetBlobContent(commit[change.Path]?.Target as Blob);
                            fileChange.LinesAdded = CountLines(fileChange.ModifiedContent);
                            fileChange.LinesRemoved = 0;
                            break;
                            
                        case LibGit2Sharp.ChangeKind.Deleted:
                            fileChange.OriginalContent = GetBlobContent(parentCommit?[change.Path]?.Target as Blob);
                            fileChange.ModifiedContent = string.Empty;
                            fileChange.LinesAdded = 0;
                            fileChange.LinesRemoved = CountLines(fileChange.OriginalContent);
                            break;
                            
                        case LibGit2Sharp.ChangeKind.Modified:
                            fileChange.OriginalContent = GetBlobContent(parentCommit?[change.Path]?.Target as Blob);
                            fileChange.ModifiedContent = GetBlobContent(commit[change.Path]?.Target as Blob);
                            CalculateChangedLines(fileChange);
                            break;
                            
                        case LibGit2Sharp.ChangeKind.Renamed:
                            fileChange.OriginalContent = GetBlobContent(parentCommit?[change.OldPath]?.Target as Blob);
                            fileChange.ModifiedContent = GetBlobContent(commit[change.Path]?.Target as Blob);
                            CalculateChangedLines(fileChange);
                            break;
                    }
                    
                    // Calcular diff
                    var patchOptions = new CompareOptions
                    {
                        Similarity = SimilarityOptions.None,
                        ContextLines = 3,
                        InterhunkLines = 0,
                        PathFilter = change.Path
                    };

                    var patch = string.Empty;
                    
                    if (change.Status == LibGit2Sharp.ChangeKind.Added)
                    {
                        patch = repo.Diff.Compare<Patch>(null, commit.Tree, new List<string> { change.Path });
                    }
                    else if (change.Status == LibGit2Sharp.ChangeKind.Deleted)
                    {
                        patch = repo.Diff.Compare<Patch>(parentCommit.Tree, null, new List<string> { change.Path });
                    }
                    else
                    {
                        patch = repo.Diff.Compare<Patch>(parentCommit?.Tree, commit.Tree, patchOptions);
                    }
                    
                    fileChange.DiffText = patch;
                    changes.Add(fileChange);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar alteração do arquivo {FilePath}", change.Path);
                }
            }
            
            return changes;
        }

        private static FileChangeType MapChangeType(LibGit2Sharp.ChangeKind changeKind)
        {
            return changeKind switch
            {
                LibGit2Sharp.ChangeKind.Added => FileChangeType.Added,
                LibGit2Sharp.ChangeKind.Deleted => FileChangeType.Deleted,
                LibGit2Sharp.ChangeKind.Modified => FileChangeType.Modified,
                LibGit2Sharp.ChangeKind.Renamed => FileChangeType.Renamed,
                _ => FileChangeType.Modified
            };
        }

        private static string GetBlobContent(Blob blob)
        {
            if (blob == null) return string.Empty;
            if (blob.IsBinary) return "[ARQUIVO BINÁRIO]";
            
            using var contentStream = blob.GetContentStream();
            using var reader = new StreamReader(contentStream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static int CountLines(string content)
        {
            if (string.IsNullOrEmpty(content)) return 0;
            return content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length;
        }

        private static void CalculateChangedLines(CommitFileChange fileChange)
        {
            var originalLines = fileChange.OriginalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var modifiedLines = fileChange.ModifiedContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            // Esta é uma implementação simples. Uma implementação mais sofisticada usaria
            // um algoritmo de diff para calcular as diferenças exatas.
            fileChange.LinesAdded = Math.Max(0, modifiedLines.Length - originalLines.Length);
            fileChange.LinesRemoved = Math.Max(0, originalLines.Length - modifiedLines.Length);
        }

        private bool ShouldIgnoreFile(string filePath)
        {
            // Ignorar arquivos binários comuns
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var ignoredExtensions = new[]
            {
                ".exe", ".dll", ".pdb", ".obj", ".bin", ".dat", ".zip", ".tar",
                ".gz", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".pdf"
            };
            
            if (ignoredExtensions.Contains(extension))
            {
                return true;
            }
            
            // Ignorar pastas comuns
            var normalizedPath = filePath.Replace('\\', '/');
            var ignoredPaths = new[]
            {
                "bin/", "obj/", "node_modules/", "dist/", "build/", ".git/"
            };
            
            return ignoredPaths.Any(p => normalizedPath.Contains(p));
        }

        #endregion
    }

    public class GitRepositoryOptions
    {
        public string RepositoryPath { get; set; }
    }
} 