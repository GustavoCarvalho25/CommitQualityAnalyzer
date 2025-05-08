using CommitQualityAnalyzer.Worker.Services.CommitAnalysis.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// Modelo simplificado para armazenar informações de commit sem depender de objetos LibGit2Sharp
public class CommitInfo
{
    public string Sha { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public DateTimeOffset AuthorDate { get; set; }
    public string Message { get; set; } = "";
    public string ShortMessage { get; set; } = "";
}

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Wrapper seguro para operações do LibGit2Sharp para evitar erros de violação de acesso
    /// Implementa uma abordagem que evita completamente o uso de objetos Commit fora do escopo de um Repository
    /// </summary>
    public class GitRepositoryWrapper
    {
        private readonly ILogger<GitRepositoryWrapper> _logger;
        private readonly string _repoPath;
        private readonly HashSet<string> _binaryExtensions;

        public GitRepositoryWrapper(ILogger<GitRepositoryWrapper> logger, string repoPath)
        {
            _logger = logger;
            _repoPath = repoPath;
            
            // Inicializar o conjunto de extensões de arquivos binários
            _binaryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // Executáveis e bibliotecas
                ".exe", ".dll", ".so", ".dylib", ".bin", ".pdb", ".obj", ".o", ".a", ".lib",
                
                // Arquivos compactados
                ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".cab", ".jar", ".war", ".ear",
                
                // Imagens
                ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico", ".webp", ".psd", ".ai",
                
                // Áudio e vídeo
                ".mp3", ".mp4", ".wav", ".avi", ".mov", ".mkv", ".flac", ".ogg", ".webm", ".m4a", ".m4v",
                
                // Documentos binários
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp",
                
                // Outros formatos binários
                ".dat", ".db", ".sqlite", ".mdb", ".accdb", ".class", ".pyc", ".mo", ".deb", ".rpm"
            };
        }

        /// <summary>
        /// Verifica se um caminho de arquivo é provavelmente um arquivo binário
        /// </summary>
        private bool IsBinaryPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string extension = Path.GetExtension(path);
            return _binaryExtensions.Contains(extension);
        }

        /// <summary>
        /// Obtém o conteúdo de um arquivo em um commit específico
        /// </summary>
        public string GetFileContent(string commitSha, string filePath)
        {
            return ExecuteSafely(repo =>
            {
                try
                {
                    // Verificar se o arquivo é binário antes de tentar obter o conteúdo
                    if (IsBinaryPath(filePath))
                    {
                        _logger.LogInformation("Arquivo {FilePath} é binário e será ignorado", filePath);
                        return string.Empty;
                    }
                    
                    var commit = repo.Lookup<Commit>(commitSha);
                    if (commit == null)
                    {
                        _logger.LogWarning("Commit {CommitSha} não encontrado", commitSha);
                        return string.Empty;
                    }

                    var blob = commit[filePath]?.Target as Blob;
                    if (blob == null)
                    {
                        _logger.LogWarning("Arquivo {FilePath} não encontrado no commit {CommitSha}", filePath, commitSha);
                        return string.Empty;
                    }
                    
                    // Verificação adicional se o arquivo é binário usando a API do LibGit2Sharp
                    if (blob.IsBinary)
                    {
                        _logger.LogInformation("Arquivo {FilePath} detectado como binário pelo LibGit2Sharp e será ignorado", filePath);
                        return string.Empty;
                    }

                    return blob.GetContentText();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao obter conteúdo do arquivo {FilePath} no commit {CommitSha}", filePath, commitSha);
                    return string.Empty;
                }
            }, string.Empty);
        }

        /// <summary>
        /// Obtém as mudanças feitas em um commit com o texto de diff para cada arquivo modificado
        /// </summary>
        public List<CommitChangeInfo> GetCommitChangesWithDiff(string commitSha)
        {
            return ExecuteSafely(repo =>
            {
                var changes = new List<CommitChangeInfo>();
                
                try
                {
                    // Obter o commit pelo SHA
                    var commit = repo.Lookup<Commit>(commitSha);
                    if (commit == null)
                    {
                        _logger.LogWarning("Commit {CommitSha} não encontrado", commitSha);
                        return changes;
                    }

                    // Obter o commit pai para comparar as mudanças
                    var parent = commit.Parents.FirstOrDefault();
                    if (parent == null)
                    {
                        _logger.LogWarning("Commit {CommitSha} não tem pai (commit inicial)", commitSha);
                        return changes;
                    }

                    // Comparar as árvores dos dois commits
                    var comparison = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
                    
                    // Processar cada arquivo modificado
                    foreach (var change in comparison)
                    {
                        try
                        {
                            // Ignorar arquivos binários
                            if (IsBinaryPath(change.Path))
                            {
                                _logger.LogInformation("Ignorando arquivo binário: {FilePath}", change.Path);
                                continue;
                            }

                            // Obter o conteúdo original e modificado
                            string originalContent = string.Empty;
                            string modifiedContent = string.Empty;

                            // Obter conteúdo original (do commit pai)
                            if (change.Status != ChangeKind.Added)
                            {
                                var oldBlob = parent[change.Path]?.Target as Blob;
                                if (oldBlob != null && !oldBlob.IsBinary)
                                {
                                    originalContent = oldBlob.GetContentText();
                                }
                            }

                            // Obter conteúdo modificado (do commit atual)
                            if (change.Status != ChangeKind.Deleted)
                            {
                                var newBlob = commit[change.Path]?.Target as Blob;
                                if (newBlob != null && !newBlob.IsBinary)
                                {
                                    modifiedContent = newBlob.GetContentText();
                                }
                            }

                            // Gerar o texto de diff
                            string diffText = GenerateDiffText(originalContent, modifiedContent);

                            // Obter o tamanho do arquivo modificado
                            long fileSize = 0;
                            if (change.Status != ChangeKind.Deleted)
                            {
                                var newBlob = commit[change.Path]?.Target as Blob;
                                if (newBlob != null)
                                {
                                    fileSize = newBlob.Size;
                                }
                            }
                            
                            // Criar o objeto de informações de mudança
                            var changeInfo = new CommitChangeInfo
                            {
                                FilePath = change.Path,
                                ChangeType = change.Status.ToString(),
                                DiffText = diffText,
                                OriginalContent = originalContent,
                                ModifiedContent = modifiedContent,
                                FileSize = fileSize
                            };

                            changes.Add(changeInfo);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao processar mudanças do arquivo {FilePath}: {ErrorMessage}", 
                                change.Path, ex.Message);
                        }
                    }

                    _logger.LogInformation("Encontradas {ChangeCount} mudanças no commit {CommitSha}", 
                        changes.Count, commitSha);
                    return changes;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao obter mudanças do commit {CommitSha}: {ErrorMessage}", 
                        commitSha, ex.Message);
                    return changes;
                }
            }, new List<CommitChangeInfo>());
        }

        /// <summary>
        /// Executa uma operação de forma segura em um repositório Git
        /// </summary>
        public T ExecuteSafely<T>(Func<Repository, T> operation, T defaultValue = default)
        {
            try
            {
                using var repo = new Repository(_repoPath);
                return operation(repo);
            }
            catch (AccessViolationException ex)
            {
                _logger.LogError(ex, "Erro de violação de acesso ao acessar o repositório: {ErrorMessage}", ex.Message);
                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao acessar o repositório: {ErrorMessage}", ex.Message);
                return defaultValue;
            }
        }
        
        /// <summary>
        /// Executa uma ação de forma segura em um repositório Git
        /// </summary>
        public void ExecuteSafely(Action<Repository> action)
        {
            try
            {
                using var repo = new Repository(_repoPath);
                action(repo);
            }
            catch (AccessViolationException ex)
            {
                _logger.LogError(ex, "Erro de violação de acesso ao acessar o repositório: {ErrorMessage}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao acessar o repositório: {ErrorMessage}", ex.Message);
            }
        }

        /// <summary>
        /// Obtém informações dos commits do último dia de forma segura
        /// </summary>
        public List<CommitInfo> GetLastDayCommits()
        {
            return ExecuteSafely(repo =>
            {
                try
                {
                    var yesterday = DateTime.Now.AddDays(-3);
                    _logger.LogInformation("Obtendo commits do último dia no repositório: {RepoPath}", _repoPath);
                    
                    // Usar uma abordagem mais segura para obter commits
                    var commitInfos = new List<CommitInfo>();
                    
                    // Limitar o número de commits para evitar problemas de memória
                    var recentCommits = repo.Commits.Take(100).ToList();
                    
                    foreach (var commit in recentCommits)
                    {
                        try
                        {
                            if (commit.Author.When >= yesterday)
                            {
                                // Extrair informações do commit em um objeto seguro
                                commitInfos.Add(new CommitInfo
                                {
                                    Sha = commit.Sha,
                                    AuthorName = commit.Author.Name,
                                    AuthorDate = commit.Author.When,
                                    Message = commit.Message,
                                    ShortMessage = commit.MessageShort
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Erro ao processar commit durante a filtragem: {ErrorMessage}", ex.Message);
                        }
                    }
                    
                    _logger.LogInformation("Encontrados {CommitCount} commits nas últimas 24 horas", commitInfos.Count);
                    return commitInfos;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao obter commits do último dia: {ErrorMessage}", ex.Message);
                    return new List<CommitInfo>();
                }
            }, new List<CommitInfo>());
        }
        
        /// <summary>
        /// Processa cada commit do último dia com uma ação específica
        /// </summary>
        public void ProcessLastDayCommits(Action<string> processAction)
        {
            ExecuteSafely(repo =>
            {
                try
                {
                    var yesterday = DateTime.Now.AddDays(-1);
                    _logger.LogInformation("Processando commits do último dia no repositório: {RepoPath}", _repoPath);
                    
                    // Limitar o número de commits para evitar problemas de memória
                    var recentCommits = repo.Commits.Take(100).ToList();
                    
                    foreach (var commit in recentCommits)
                    {
                        try
                        {
                            if (commit.Author.When >= yesterday)
                            {
                                // Processar o commit usando apenas o SHA
                                processAction(commit.Sha);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Erro ao processar commit: {ErrorMessage}", ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar commits do último dia: {ErrorMessage}", ex.Message);
                }
            });
        }

        /// <summary>
        /// Obtém informações seguras sobre um commit por ID
        /// </summary>
        public CommitInfo GetCommitInfoById(string commitId)
        {
            return ExecuteSafely(repo =>
            {
                try
                {
                    var commit = repo.Lookup<Commit>(commitId);
                    if (commit == null)
                    {
                        _logger.LogWarning("Commit não encontrado: {CommitId}", commitId);
                        return new CommitInfo { Sha = commitId };
                    }

                    return new CommitInfo
                    {
                        Sha = commit.Sha,
                        AuthorName = commit.Author?.Name ?? "Desconhecido",
                        AuthorDate = commit.Author?.When ?? DateTimeOffset.MinValue,
                        Message = commit.Message ?? string.Empty,
                        ShortMessage = commit.MessageShort ?? string.Empty
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao obter informações do commit {CommitId}: {ErrorMessage}", 
                        commitId, ex.Message);
                    return new CommitInfo { Sha = commitId };
                }
            }, new CommitInfo { Sha = commitId });
        }


        
        /// <summary>
        /// Gera um texto de diferença entre duas versões de conteúdo
        /// </summary>
        private string GenerateDiffText(string originalContent, string modifiedContent)
        {
            if (string.IsNullOrEmpty(originalContent) && string.IsNullOrEmpty(modifiedContent))
                return string.Empty;

            if (string.IsNullOrEmpty(originalContent))
                return $"Arquivo novo:\n{TruncateIfTooLarge(modifiedContent)}";

            if (string.IsNullOrEmpty(modifiedContent))
                return $"Arquivo removido:\n{TruncateIfTooLarge(originalContent)}";

            // Limitar o tamanho do conteúdo para evitar problemas de memória
            originalContent = TruncateIfTooLarge(originalContent);
            modifiedContent = TruncateIfTooLarge(modifiedContent);

            var originalLines = originalContent.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
            var modifiedLines = modifiedContent.Split(new[] { '\n', '\r' }, StringSplitOptions.None);

            // Limitar o número de linhas para evitar problemas de desempenho
            if (originalLines.Length > 1000 || modifiedLines.Length > 1000)
            {
                originalLines = originalLines.Take(1000).ToArray();
                modifiedLines = modifiedLines.Take(1000).ToArray();
            }

            var diff = new System.Text.StringBuilder();
            var lcs = LongestCommonSubsequence(originalLines, modifiedLines);

            int originalIndex = 0;
            int modifiedIndex = 0;

            foreach (var pair in lcs)
            {
                // Adicionar linhas removidas
                while (originalIndex < pair.Item1)
                {
                    diff.AppendLine($"- {originalLines[originalIndex]}");
                    originalIndex++;
                }

                // Adicionar linhas adicionadas
                while (modifiedIndex < pair.Item2)
                {
                    diff.AppendLine($"+ {modifiedLines[modifiedIndex]}");
                    modifiedIndex++;
                }

                // Adicionar linhas inalteradas
                diff.AppendLine($"  {originalLines[originalIndex]}");
                originalIndex++;
                modifiedIndex++;
            }

            // Processar linhas restantes
            while (originalIndex < originalLines.Length)
            {
                diff.AppendLine($"- {originalLines[originalIndex]}");
                originalIndex++;
            }

            while (modifiedIndex < modifiedLines.Length)
            {
                diff.AppendLine($"+ {modifiedLines[modifiedIndex]}");
                modifiedIndex++;
            }

            return diff.ToString();
        }
        
        /// <summary>
        /// Trunca uma string se ela for muito grande
        /// </summary>
        private string TruncateIfTooLarge(string content, int maxLength = 100000)
        {
            if (string.IsNullOrEmpty(content))
                return content;
                
            if (content.Length > maxLength)
            {
                return content.Substring(0, maxLength) + "\n[Conteúdo truncado devido ao tamanho...]";
            }
            
            return content;
        }
        
        /// <summary>
        /// Calcula a subsequência comum mais longa entre duas sequências
        /// </summary>
        private List<Tuple<int, int>> LongestCommonSubsequence(string[] original, string[] modified)
        {
            try
            {
                // Limitar o tamanho das arrays para evitar problemas de memória
                if (original.Length > 1000 || modified.Length > 1000)
                {
                    original = original.Take(1000).ToArray();
                    modified = modified.Take(1000).ToArray();
                }
                
                int[,] lengths = new int[original.Length + 1, modified.Length + 1];

                // Preencher a matriz de comprimentos
                for (int i = 0; i < original.Length; i++)
                {
                    for (int j = 0; j < modified.Length; j++)
                    {
                        if (string.Equals(original[i], modified[j], StringComparison.Ordinal))
                            lengths[i + 1, j + 1] = lengths[i, j] + 1;
                        else
                            lengths[i + 1, j + 1] = Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
                    }
                }

                // Reconstruir a subsequência
                var result = new List<Tuple<int, int>>();
                int originalIndex = original.Length;
                int modifiedIndex = modified.Length;

                while (originalIndex > 0 && modifiedIndex > 0)
                {
                    if (string.Equals(original[originalIndex - 1], modified[modifiedIndex - 1], StringComparison.Ordinal))
                    {
                        result.Add(Tuple.Create(originalIndex - 1, modifiedIndex - 1));
                        originalIndex--;
                        modifiedIndex--;
                    }
                    else if (lengths[originalIndex - 1, modifiedIndex] >= lengths[originalIndex, modifiedIndex - 1])
                    {
                        originalIndex--;
                    }
                    else
                    {
                        modifiedIndex--;
                    }
                }

                result.Reverse();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular a subsequência comum mais longa: {ErrorMessage}", ex.Message);
                return new List<Tuple<int, int>>();
            }
        }
        
        /// <summary>
        /// Verifica se um caminho de arquivo é provavelmente um arquivo binário
        /// </summary>
    }
}
