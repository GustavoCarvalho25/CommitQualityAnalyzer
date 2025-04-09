using CommitQualityAnalyzer.Worker.Services.CommitAnalysis.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Serviço responsável por extrair diferenças entre commits
    /// </summary>
    public class GitDiffService
    {
        private readonly ILogger<GitDiffService> _logger;
        private readonly GitRepositoryWrapper _repoWrapper;
        private readonly string _repoPath;

        public GitDiffService(ILogger<GitDiffService> logger, GitRepositoryWrapper repoWrapper, string repoPath)
        {
            _logger = logger;
            _repoWrapper = repoWrapper;
            _repoPath = repoPath;
        }

        /// <summary>
        /// Obtém as mudanças de um commit com o texto de diferença de forma segura
        /// </summary>
        public List<CommitChangeInfo> GetCommitChangesWithDiff(string commitId)
        {
            var changes = new List<CommitChangeInfo>();
            
            try
            {
                if (string.IsNullOrEmpty(commitId))
                {
                    _logger.LogError("ID do commit não pode ser nulo ou vazio");
                    return changes;
                }
                
                _logger.LogDebug("Obtendo mudanças para o commit {CommitId}", commitId);
                
                // Usar o wrapper seguro para acessar o repositório
                return _repoWrapper.ExecuteSafely(repo => {
                    try {
                        // Obter o commit atual
                        var commit = repo.Lookup<Commit>(commitId);
                        if (commit == null)
                        {
                            _logger.LogError("Commit não encontrado: {CommitId}", commitId);
                            return changes;
                        }
                        
                        // Obter o commit pai para comparação de forma segura
                        var parent = commit.Parents.FirstOrDefault();
                        
                        if (parent == null)
                        {
                            _logger.LogWarning("Commit {CommitId} não tem pai, não é possível gerar diff", commitId);
                            return changes;
                        }

                        // Comparar as árvores de arquivos entre o commit e seu pai de forma segura
                        TreeChanges comparison;
                        try
                        {
                            comparison = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao comparar árvores de arquivos para o commit {CommitId}: {ErrorMessage}", 
                                commitId, ex.Message);
                            return changes;
                        }
                
                        if (comparison == null)
                        {
                            _logger.LogWarning("Não foi possível obter comparação para o commit {CommitId}", commitId);
                            return changes;
                        }
                
                foreach (var change in comparison)
                {
                    try
                    {
                        // Ignorar arquivos binários e arquivos excluídos
                        if (change.Status == ChangeKind.Deleted || IsBinaryPath(change.Path))
                            continue;

                        string originalContent = "";
                        string modifiedContent = "";
                        string diffText = "";

                        // Obter conteúdo original (do commit pai) de forma segura
                        if (change.Status != ChangeKind.Added)
                        {
                            try
                            {
                                var entry = parent[change.Path];
                                if (entry != null)
                                {
                                    var originalBlob = entry.Target as Blob;
                                    if (originalBlob != null)
                                    {
                                        try
                                        {
                                            using (var contentStream = originalBlob.GetContentStream())
                                            using (var reader = new StreamReader(contentStream))
                                            {
                                                originalContent = reader.ReadToEnd();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Erro ao ler conteúdo original do arquivo {FilePath}: {ErrorMessage}", 
                                                change.Path, ex.Message);
                                            originalContent = "[Conteúdo não disponível]";
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Erro ao acessar o arquivo {FilePath} no commit pai: {ErrorMessage}", 
                                    change.Path, ex.Message);
                                originalContent = "[Conteúdo não disponível]";
                            }
                        }

                        // Obter conteúdo modificado (do commit atual) de forma segura
                        if (change.Status != ChangeKind.Deleted)
                        {
                            try
                            {
                                var entry = commit[change.Path];
                                if (entry != null)
                                {
                                    var modifiedBlob = entry.Target as Blob;
                                    if (modifiedBlob != null)
                                    {
                                        try
                                        {
                                            using (var contentStream = modifiedBlob.GetContentStream())
                                            using (var reader = new StreamReader(contentStream))
                                            {
                                                modifiedContent = reader.ReadToEnd();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Erro ao ler conteúdo modificado do arquivo {FilePath}: {ErrorMessage}", 
                                                change.Path, ex.Message);
                                            modifiedContent = "[Conteúdo não disponível]";
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Erro ao acessar o arquivo {FilePath} no commit atual: {ErrorMessage}", 
                                    change.Path, ex.Message);
                                modifiedContent = "[Conteúdo não disponível]";
                            }
                        }

                        // Gerar texto de diferença de forma segura
                        try
                        {
                            if (!string.IsNullOrEmpty(originalContent) && !string.IsNullOrEmpty(modifiedContent))
                            {
                                diffText = GenerateDiffText(originalContent, modifiedContent, change.Path);
                            }
                            else if (!string.IsNullOrEmpty(modifiedContent))
                            {
                                // Se é um arquivo novo, todo o conteúdo é considerado como diferença
                                diffText = $"Arquivo novo:\n{TruncateIfTooLarge(modifiedContent)}";
                            }
                            else
                            {
                                diffText = "[Não foi possível gerar o diff]";
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Erro ao gerar texto de diff para {FilePath}: {ErrorMessage}", 
                                change.Path, ex.Message);
                            diffText = $"[Erro ao gerar diff: {ex.Message}]";
                        }

                        changes.Add(new CommitChangeInfo
                        {
                            FilePath = change.Path,
                            ChangeType = change.Status.ToString(),
                            OriginalContent = originalContent ?? "",
                            ModifiedContent = modifiedContent ?? "",
                            DiffText = diffText ?? ""
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar arquivo {FilePath} no commit {CommitId}: {ErrorMessage}",
                            change.Path, commit.Sha, ex.Message);
                    }
                }
            }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao obter mudanças do commit {CommitId}: {ErrorMessage}",
                            commitId, ex.Message);
                        return changes;
                    }
                    
                    return changes;
                }, changes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar commit: {ErrorMessage}", ex.Message);
                return changes;
            }
        }

        /// <summary>
        /// Gera um texto de diferença entre duas versões de conteúdo
        /// </summary>
        public string GenerateDiffText(string originalContent, string modifiedContent, string filePath = null)
        {
            try
            {
                // Validação de entrada
                if (string.IsNullOrEmpty(originalContent) && string.IsNullOrEmpty(modifiedContent))
                    return string.Empty;

                if (string.IsNullOrEmpty(originalContent))
                    return $"Arquivo novo:\n{TruncateIfTooLarge(modifiedContent)}";

                if (string.IsNullOrEmpty(modifiedContent))
                    return $"Arquivo removido:\n{TruncateIfTooLarge(originalContent)}";

                // Limitar o tamanho do conteúdo para evitar problemas de memória
                originalContent = TruncateIfTooLarge(originalContent);
                modifiedContent = TruncateIfTooLarge(modifiedContent);

                // Dividir em linhas de forma segura
                var originalLines = originalContent.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
                var modifiedLines = modifiedContent.Split(new[] { '\n', '\r' }, StringSplitOptions.None);

                // Limitar o número de linhas para evitar problemas de desempenho
                if (originalLines.Length > 1000 || modifiedLines.Length > 1000)
                {
                    _logger.LogWarning("Arquivo {FilePath} muito grande para gerar diff completo. Limitando a 1000 linhas.", filePath ?? "desconhecido");
                    originalLines = originalLines.Take(1000).ToArray();
                    modifiedLines = modifiedLines.Take(1000).ToArray();
                }

                var lcs = LongestCommonSubsequence(originalLines, modifiedLines);
                var diff = new StringBuilder();

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar texto de diff: {ErrorMessage}", ex.Message);
                return "[Erro ao gerar diff]";
            }
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
        private bool IsBinaryPath(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            string[] binaryExtensions = { ".exe", ".dll", ".pdb", ".zip", ".rar", ".7z", ".png", ".jpg", ".jpeg", ".gif", ".pdf" };
            return binaryExtensions.Contains(extension);
        }
    }
}
