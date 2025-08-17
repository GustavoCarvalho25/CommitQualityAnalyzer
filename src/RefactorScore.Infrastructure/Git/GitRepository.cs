using System.Text;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using RefactorScore.Domain.Entities;
using RefactorScore.Domain.Interfaces;
using Commit = RefactorScore.Domain.Entities.Commit;

namespace RefactorScore.Infrastructure.Git
{
    public class GitRepository : IGitRepository
    {
        private readonly string _repositoryPath;
        private readonly ILogger<GitRepository> _logger;
        
        /// <summary>
        /// Construtor
        /// </summary>
        /// <param name="repositoryPath">Caminho para o repositório Git</param>
        /// <param name="logger">Logger</param>
        public GitRepository(string repositoryPath, ILogger<GitRepository> logger)
        {
            _repositoryPath = repositoryPath;
            _logger = logger;
            
            _logger.LogInformation("📂 Repositório Git inicializado em: {RepositoryPath}", _repositoryPath);
        }
        
        public async Task<List<Commit>> ObterCommitsPorPeriodoAsync(DateTime? dataInicio = null, DateTime? dataFim = null)
        {
            _logger.LogInformation("🔍 Buscando commits no período: {DataInicio} a {DataFim}", 
                dataInicio?.ToString("yyyy-MM-dd") ?? "início", 
                dataFim?.ToString("yyyy-MM-dd") ?? "atual");
            
            return await Task.Run(() =>
            {
                var commits = new List<Commit>();
                
                using (var repo = new Repository(_repositoryPath))
                {
                    var commitFilter = new CommitFilter
                    {
                        SortBy = CommitSortStrategies.Time,
                        IncludeReachableFrom = repo.Head.CanonicalName
                    };
                    
                    _logger.LogInformation("🔍 Executando query de commits na branch: {Branch}", repo.Head.FriendlyName);
                    
                    var libGitCommits = repo.Commits.QueryBy(commitFilter).ToList();
                    _logger.LogInformation("📊 Encontrados {Total} commits no repositório", libGitCommits.Count);
                    
                    int totalFiltrado = 0;
                    foreach (var libGitCommit in libGitCommits)
                    {
                        var commitDate = libGitCommit.Author.When.DateTime;
                        
                        if ((dataInicio == null || commitDate >= dataInicio) &&
                            (dataFim == null || commitDate <= dataFim))
                        {
                            totalFiltrado++;
                            commits.Add(ConverterParaCommit(libGitCommit));
                        }
                    }
                    
                    _logger.LogInformation("📋 Filtrados {Filtrados} commits no período especificado", totalFiltrado);
                }
                
                return commits;
            });
        }
        
        public async Task<List<Commit>> ObterCommitsUltimosDiasAsync(int dias)
        {
            _logger.LogInformation("🔍 Buscando commits dos últimos {Dias} dias", dias);
            
            var dataInicio = DateTime.UtcNow.AddDays(-dias);
            return await ObterCommitsPorPeriodoAsync(dataInicio);
        }
        
        public async Task<Commit?> ObterCommitPorIdAsync(string commitId)
        {
            _logger.LogInformation("🔍 Buscando commit por ID: {CommitId}", commitId);
            
            return await Task.Run(() =>
            {
                using (var repo = new Repository(_repositoryPath))
                {
                    var libGitCommit = repo.Lookup<LibGit2Sharp.Commit>(commitId);
                    
                    if (libGitCommit == null)
                    {
                        _logger.LogWarning("⚠️ Commit não encontrado: {CommitId}", commitId);
                        return null;
                    }
                    
                    _logger.LogInformation("✅ Commit encontrado: {CommitId} - {Mensagem}", 
                        commitId, libGitCommit.MessageShort);
                        
                    return ConverterParaCommit(libGitCommit);
                }
            });
        }
        
        public async Task<List<MudancaDeArquivoNoCommit>> ObterMudancasNoCommitAsync(string commitId)
        {
            _logger.LogInformation("🔍 Buscando mudanças no commit: {CommitId}", commitId);
            
            return await Task.Run(() =>
            {
                var mudancas = new List<MudancaDeArquivoNoCommit>();
                
                using (var repo = new Repository(_repositoryPath))
                {
                    var commit = repo.Lookup<LibGit2Sharp.Commit>(commitId);
                    
                    if (commit == null)
                    {
                        _logger.LogWarning("⚠️ Commit não encontrado: {CommitId}", commitId);
                        return mudancas;
                    }
                    
                    _logger.LogInformation("✅ Analisando mudanças no commit: {CommitId} - {Mensagem} ({Data})", 
                        commitId.Substring(0, 7), commit.MessageShort, commit.Author.When.DateTime);
                        
                    // Se é o primeiro commit, não temos parent para comparar
                    if (commit.Parents.Count() == 0)
                    {
                        _logger.LogInformation("ℹ️ Commit inicial do repositório, analisando árvore completa");
                        
                        // Para o primeiro commit, todas as mudanças são adições
                        var tree = commit.Tree;
                        
                        foreach (var entry in tree)
                        {
                            if (entry.TargetType == TreeEntryTargetType.Blob)
                            {
                                var blob = (Blob)entry.Target;
                                var conteudo = blob.GetContentStream().ReadAsString();
                                
                                var mudanca = new MudancaDeArquivoNoCommit
                                {
                                    CaminhoArquivo = entry.Path,
                                    TipoMudanca = TipoMudanca.Adicionado,
                                    LinhasAdicionadas = conteudo.Split('\n').Length,
                                    LinhasRemovidas = 0,
                                    ConteudoOriginal = string.Empty,
                                    ConteudoModificado = conteudo,
                                    TextoDiff = string.Empty
                                };
                                
                                // Nota: EhCodigoFonte é calculado automaticamente pela propriedade
                                
                                mudancas.Add(mudanca);
                                
                                _logger.LogInformation("📄 Arquivo adicionado: {CaminhoArquivo} ({Linhas} linhas, {TipoArquivo})", 
                                    entry.Path, mudanca.LinhasAdicionadas, 
                                    mudanca.EhCodigoFonte ? "código fonte" : "não-código");
                            }
                        }
                    }
                    else
                    {
                        var parent = commit.Parents.First();
                        
                        _logger.LogInformation("ℹ️ Comparando commit com seu parent: {ParentId} - {ParentMensagem}", 
                            parent.Id.Sha.Substring(0, 7), parent.MessageShort);
                        
                        // Comparar com o parent para obter as mudanças
                        var comparison = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
                        
                        _logger.LogInformation("📊 Encontradas {Total} mudanças no commit", comparison.Count);
                        
                        foreach (var change in comparison)
                        {
                            var mudanca = new MudancaDeArquivoNoCommit
                            {
                                CaminhoArquivo = change.Path,
                                TipoMudanca = ConverterTipoMudanca(change.Status)
                            };
                            
                            // Para arquivos renomeados, adicionar o caminho antigo
                            if (change.Status == ChangeKind.Renamed)
                            {
                                mudanca.CaminhoAntigo = change.OldPath;
                            }
                            
                            // Obter o conteúdo e diff para mudanças que não sejam remoções
                            if (change.Status != ChangeKind.Deleted)
                            {
                                var blob = repo.Lookup<Blob>(change.Oid);
                                if (blob != null)
                                {
                                    mudanca.ConteudoModificado = blob.GetContentStream().ReadAsString();
                                }
                                else
                                {
                                    mudanca.ConteudoModificado = string.Empty;
                                }
                            }
                            else
                            {
                                mudanca.ConteudoModificado = string.Empty;
                            }
                            
                            // Obter o conteúdo original para mudanças que não sejam adições
                            if (change.Status != ChangeKind.Added)
                            {
                                var oldBlob = repo.Lookup<Blob>(change.OldOid);
                                if (oldBlob != null)
                                {
                                    mudanca.ConteudoOriginal = oldBlob.GetContentStream().ReadAsString();
                                }
                                else
                                {
                                    mudanca.ConteudoOriginal = string.Empty;
                                }
                            }
                            else
                            {
                                mudanca.ConteudoOriginal = string.Empty;
                            }
                            
                            // Obter detalhes do diff entre os arquivos
                            var options = new CompareOptions 
                            { 
                                Similarity = SimilarityOptions.Renames,
                                IncludeUnmodified = false,
                                ContextLines = 3,
                            };
                            
                            // Adicionar um filtro de caminho se possível
                            try
                            {
                                var patch = repo.Diff.Compare<Patch>(parent.Tree, commit.Tree, 
                                    new[] { change.Path }, options);
                                    
                                mudanca.TextoDiff = patch;
                            }
                            catch
                            {
                                // Se não conseguir obter o diff com filtro, tenta sem filtro
                                try
                                {
                                    var patch = repo.Diff.Compare<Patch>(parent.Tree, commit.Tree, options);
                                    mudanca.TextoDiff = patch;
                                }
                                catch
                                {
                                    // Fallback se não conseguir obter o diff de nenhuma forma
                                    mudanca.TextoDiff = string.Empty;
                                }
                            }
                            
                            // Contar linhas adicionadas e removidas
                            ContarLinhasMudanca(mudanca.TextoDiff, out int adicionadas, out int removidas);
                            mudanca.LinhasAdicionadas = adicionadas;
                            mudanca.LinhasRemovidas = removidas;
                            
                            // Nota: EhCodigoFonte é calculado automaticamente pela propriedade
                            
                            mudancas.Add(mudanca);
                            
                            _logger.LogInformation("📄 Arquivo {TipoMudanca}: {CaminhoArquivo} (+{LinhasAdd}/-{LinhasRem}, {TipoArquivo})",
                                mudanca.TipoMudanca, change.Path, adicionadas, removidas,
                                mudanca.EhCodigoFonte ? "código fonte" : "não-código");
                        }
                    }
                }
                
                _logger.LogInformation("📊 Total de mudanças encontradas: {Total}, sendo {CodigoFonte} arquivos de código fonte", 
                    mudancas.Count, mudancas.Count(m => m.EhCodigoFonte));
                
                return mudancas;
            });
        }
        
        public async Task<string?> ObterConteudoArquivoNoCommitAsync(string commitId, string caminhoArquivo)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var repo = new Repository(_repositoryPath))
                    {
                        var commit = repo.Lookup<LibGit2Sharp.Commit>(commitId);
                        
                        if (commit == null)
                            return null;
                            
                        var treeEntry = commit[caminhoArquivo];
                        
                        if (treeEntry == null || treeEntry.TargetType != TreeEntryTargetType.Blob)
                            return null;
                            
                        var blob = (Blob)treeEntry.Target;
                        return blob.GetContentStream().ReadAsString();
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }
        
        public async Task<string?> ObterDiffArquivoAsync(string commitIdAntigo, string commitIdNovo, string caminhoArquivo)
        {
            return await Task.Run(() =>
            {
                using (var repo = new Repository(_repositoryPath))
                {
                    var commitAntigo = repo.Lookup<LibGit2Sharp.Commit>(commitIdAntigo);
                    var commitNovo = repo.Lookup<LibGit2Sharp.Commit>(commitIdNovo);
                    
                    if (commitAntigo == null || commitNovo == null)
                        return null;
                    
                    var options = new CompareOptions 
                    { 
                        Similarity = SimilarityOptions.Renames,
                        IncludeUnmodified = false,
                        ContextLines = 3
                    };
                    
                    try
                    {
                        var patch = repo.Diff.Compare<Patch>(commitAntigo.Tree, commitNovo.Tree,
                            new[] { caminhoArquivo }, options);
                            
                        return patch;
                    }
                    catch
                    {
                        return null;
                    }
                }
            });
        }
        
        public async Task<string?> ObterDiffCommitAsync(string commitId)
        {
            return await Task.Run(() =>
            {
                using (var repo = new Repository(_repositoryPath))
                {
                    var commit = repo.Lookup<LibGit2Sharp.Commit>(commitId);
                    
                    if (commit == null)
                        return null;
                    
                    var options = new CompareOptions 
                    { 
                        Similarity = SimilarityOptions.Renames,
                        IncludeUnmodified = false,
                        ContextLines = 3
                    };
                        
                    try
                    {
                        if (commit.Parents.Count() == 0)
                        {
                            var patch = repo.Diff.Compare<Patch>(null, commit.Tree, options);
                            return patch;
                        }
                        else
                        {
                            var parent = commit.Parents.First();
                            var patch = repo.Diff.Compare<Patch>(parent.Tree, commit.Tree, options);
                            return patch;
                        }
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
            });
        }
        
        public async Task<bool> ValidarRepositorioAsync(string caminho)
        {
            _logger.LogInformation("🔍 Validando repositório Git em: {Caminho}", caminho);
            
            return await Task.Run(() =>
            {
                try
                {
                    var repoValido = Repository.IsValid(caminho);
                    
                    if (repoValido)
                    {
                        _logger.LogInformation("Repositório Git válido encontrado em: {Caminho}", caminho);
                    }
                    else
                    {
                        _logger.LogWarning("Diretório não é um repositório Git válido: {Caminho}", caminho);
                    }
                    
                    return repoValido;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao validar repositório Git: {Mensagem}", ex.Message);
                    return false;
                }
            });
        }
        
        #region Métodos auxiliares
        
        private Commit ConverterParaCommit(LibGit2Sharp.Commit libGitCommit)
        {
            return new Commit
            {
                Id = libGitCommit.Sha,
                Autor = libGitCommit.Author.Name,
                Email = libGitCommit.Author.Email,
                Data = libGitCommit.Author.When.DateTime,
                Mensagem = libGitCommit.Message
            };
        }
        
        private TipoMudanca ConverterTipoMudanca(ChangeKind changeKind)
        {
            return changeKind switch
            {
                ChangeKind.Added => TipoMudanca.Adicionado,
                ChangeKind.Deleted => TipoMudanca.Removido,
                ChangeKind.Modified => TipoMudanca.Modificado,
                ChangeKind.Renamed => TipoMudanca.Renomeado,
                _ => TipoMudanca.Modificado
            };
        }
        
        private void ContarLinhasMudanca(string patch, out int linhasAdicionadas, out int linhasRemovidas)
        {
            linhasAdicionadas = 0;
            linhasRemovidas = 0;
            
            if (string.IsNullOrEmpty(patch))
                return;
                
            var linhas = patch.Split('\n');
            
            foreach (var linha in linhas)
            {
                if (linha.StartsWith("+") && !linha.StartsWith("+++"))
                    linhasAdicionadas++;
                else if (linha.StartsWith("-") && !linha.StartsWith("---"))
                    linhasRemovidas++;
            }
        }
        
        #endregion
    }
    
    internal static class StreamExtensions
    {
        public static string ReadAsString(this Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
} 