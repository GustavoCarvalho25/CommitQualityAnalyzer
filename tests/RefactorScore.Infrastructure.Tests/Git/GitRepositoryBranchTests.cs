using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Moq;
using RefactorScore.Domain.Entities;
using RefactorScore.Infrastructure.Git;
using Xunit;

namespace RefactorScore.Infrastructure.Tests.Git
{
    public class GitRepositoryBranchTests : IDisposable
    {
        private readonly string _testRepoPath;
        private readonly Mock<ILogger<GitRepository>> _loggerMock;
        
        public GitRepositoryBranchTests()
        {
            // Criar um repositório Git temporário para testes
            _testRepoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testRepoPath);
            
            Repository.Init(_testRepoPath);
            
            // Criar mock do logger
            _loggerMock = new Mock<ILogger<GitRepository>>();
            
            // Inicializar o repositório com commit inicial
            CreateInitialCommit();
        }
        
        [Fact]
        public async Task ObterCommitsPorPeriodoAsync_MultiploBranches_RetornaApenasCommitsDoBranchAtual()
        {
            // Arrange
            // Criar um repositório com múltiplos branches
            string mainBranchFilePath = Path.Combine(_testRepoPath, "main_branch_file.txt");
            File.WriteAllText(mainBranchFilePath, "Arquivo na branch principal");
            CommitFile(mainBranchFilePath, "Commit na branch principal");
            
            // Criar uma nova branch
            using (var repo = new Repository(_testRepoPath))
            {
                // Criar e mudar para uma nova branch
                var newBranch = repo.CreateBranch("feature-branch");
                Commands.Checkout(repo, newBranch);
                
                // Adicionar um arquivo na nova branch
                string featureBranchFilePath = Path.Combine(_testRepoPath, "feature_branch_file.txt");
                File.WriteAllText(featureBranchFilePath, "Arquivo na feature branch");
                
                // Commitar na nova branch
                Commands.Stage(repo, featureBranchFilePath);
                var author = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit("Commit na feature branch", author, author);
                
                // Voltar para a branch principal
                Commands.Checkout(repo, "master");
            }
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var commits = await gitRepository.ObterCommitsPorPeriodoAsync();
            
            // Assert
            Assert.Equal(2, commits.Count); // Commit inicial + commit na branch principal
            Assert.Contains(commits, c => c.Mensagem.Contains("Commit na branch principal"));
            Assert.DoesNotContain(commits, c => c.Mensagem.Contains("Commit na feature branch"));
        }
        
        [Fact]
        public async Task ObterMudancasNoCommitAsync_CommitMerge_RetornaMudancasCorretamente()
        {
            // Arrange
            // Criar um cenário de merge entre branches
            
            using (var repo = new Repository(_testRepoPath))
            {
                // Criar e mudar para uma nova branch
                var featureBranch = repo.CreateBranch("feature-merge");
                Commands.Checkout(repo, featureBranch);
                
                // Modificar o arquivo na feature branch
                string filePath = Path.Combine(_testRepoPath, "file.txt");
                File.AppendAllText(filePath, "\nModificação na feature branch");
                
                // Commitar na feature branch
                Commands.Stage(repo, filePath);
                var author = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit("Modificação na feature branch", author, author);
                
                // Voltar para a branch principal
                Commands.Checkout(repo, "master");
                
                // Modificar o mesmo arquivo na branch principal (para criar conflito)
                File.AppendAllText(filePath, "\nModificação na branch principal");
                
                // Commitar na branch principal
                Commands.Stage(repo, filePath);
                repo.Commit("Modificação na branch principal", author, author);
                
                // Fazer o merge (vai gerar um commit de merge)
                var mergeResult = repo.Merge(featureBranch, author);
                
                // Se houve conflito, resolvê-lo manualmente
                if (mergeResult.Status == MergeStatus.Conflicts)
                {
                    // Resolver conflito de forma simples (mantendo ambas alterações)
                    File.WriteAllText(filePath, "Conteúdo inicial\nModificação na feature branch\nModificação na branch principal");
                    
                    Commands.Stage(repo, filePath);
                    repo.Commit("Merge resolvido", author, author);
                }
            }
            
            // Obter o commit de merge
            string mergeCommitId;
            using (var repo = new Repository(_testRepoPath))
            {
                mergeCommitId = repo.Commits.First().Sha;
            }
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var mudancas = await gitRepository.ObterMudancasNoCommitAsync(mergeCommitId);
            
            // Assert
            Assert.Single(mudancas); // Apenas um arquivo foi modificado
            Assert.Equal("file.txt", mudancas[0].CaminhoArquivo);
            Assert.Contains("Modificação na feature branch", mudancas[0].ConteudoModificado);
            Assert.Contains("Modificação na branch principal", mudancas[0].ConteudoModificado);
        }
        
        [Fact]
        public async Task ObterCommitsUltimosDiasAsync_ComMergeCommit_ContabilizaCorretamente()
        {
            // Arrange
            // Criar um cenário com commits e merge
            using (var repo = new Repository(_testRepoPath))
            {
                var author = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                
                // Criar branch de feature
                var featureBranch = repo.CreateBranch("feature");
                Commands.Checkout(repo, featureBranch);
                
                // Commit na branch de feature
                string featureFilePath = Path.Combine(_testRepoPath, "feature_file.txt");
                File.WriteAllText(featureFilePath, "Arquivo da feature");
                Commands.Stage(repo, featureFilePath);
                repo.Commit("Commit na feature", author, author);
                
                // Voltar para master e criar commit lá também
                Commands.Checkout(repo, "master");
                string mainFilePath = Path.Combine(_testRepoPath, "main_file.txt");
                File.WriteAllText(mainFilePath, "Arquivo da main");
                Commands.Stage(repo, mainFilePath);
                repo.Commit("Commit na main", author, author);
                
                // Fazer merge (fast-forward se possível)
                repo.Merge(featureBranch, author);
            }
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var commits = await gitRepository.ObterCommitsUltimosDiasAsync(1);
            
            // Assert - verificar se contabiliza corretamente os commits
            Assert.True(commits.Count >= 3); // Commit inicial + commit na feature + commit na main (+ possível merge commit)
        }
        
        [Fact]
        public async Task ObterDiffArquivoAsync_ArquivoRenomeadoEntreCommits_ProcessaCorretamente()
        {
            // Arrange
            string originalFilePath = Path.Combine(_testRepoPath, "arquivo_original.txt");
            string newFilePath = Path.Combine(_testRepoPath, "arquivo_renomeado.txt");
            
            // Criar arquivo inicial
            File.WriteAllText(originalFilePath, "Conteúdo do arquivo");
            string commitId1 = CommitFile(originalFilePath, "Adicionado arquivo original");
            
            // Renomear arquivo e modificar conteúdo
            File.Delete(originalFilePath);
            File.WriteAllText(newFilePath, "Conteúdo do arquivo modificado");
            
            string commitId2;
            using (var repo = new Repository(_testRepoPath))
            {
                // Stage todas as mudanças (inclusive renomeação)
                Commands.Stage(repo, originalFilePath); // Stage a remoção
                Commands.Stage(repo, newFilePath);     // Stage a adição
                
                var author = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                var commit = repo.Commit("Arquivo renomeado e modificado", author, author);
                
                commitId2 = commit.Sha;
            }
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act - Tentar obter o diff entre os commits usando o nome do arquivo original
            var diff1 = await gitRepository.ObterDiffArquivoAsync(commitId1, commitId2, "arquivo_original.txt");
            
            // Act - Tentar obter o diff entre os commits usando o nome do arquivo novo
            var diff2 = await gitRepository.ObterDiffArquivoAsync(commitId1, commitId2, "arquivo_renomeado.txt");
            
            // Assert
            Assert.NotNull(diff1);
            Assert.NotNull(diff2);
            
            // Pelo menos um dos diffs deve mostrar a mudança de conteúdo
            bool foundContentChange = (diff1?.Contains("modificado") ?? false) || 
                                     (diff2?.Contains("modificado") ?? false);
                                     
            Assert.True(foundContentChange, "O diff deveria mostrar a mudança de conteúdo");
        }
        
        #region Helpers
        
        private void CreateInitialCommit()
        {
            string filePath = Path.Combine(_testRepoPath, "file.txt");
            File.WriteAllText(filePath, "Conteúdo inicial");
            
            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, filePath);
                
                var author = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit("Initial commit", author, author);
            }
        }
        
        private string CommitFile(string filePath, string message)
        {
            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, filePath);
                
                var author = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                var commit = repo.Commit(message, author, author);
                
                return commit.Sha;
            }
        }
        
        #endregion
        
        public void Dispose()
        {
            // Limpar o repositório de teste
            try
            {
                Directory.Delete(_testRepoPath, true);
            }
            catch
            {
                // Ignorar erros na limpeza
            }
        }
    }
} 