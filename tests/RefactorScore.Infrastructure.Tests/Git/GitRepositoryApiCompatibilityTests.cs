using System;
using System.IO;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Moq;
using RefactorScore.Domain.Entities;
using RefactorScore.Domain.Interfaces;
using RefactorScore.Infrastructure.Git;
using Xunit;

namespace RefactorScore.Infrastructure.Tests.Git
{
    /// <summary>
    /// Testes para verificar a compatibilidade entre a interface IGitRepository e sua implementação.
    /// Estes testes são importantes para garantir que a implementação corresponde exatamente à interface.
    /// </summary>
    public class GitRepositoryApiCompatibilityTests : IDisposable
    {
        private readonly string _testRepoPath;
        private readonly Mock<ILogger<GitRepository>> _loggerMock;
        private readonly GitRepository _gitRepository;
        
        public GitRepositoryApiCompatibilityTests()
        {
            // Criar um repositório Git temporário para testes
            _testRepoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testRepoPath);
            
            Repository.Init(_testRepoPath);
            
            // Criar mock do logger
            _loggerMock = new Mock<ILogger<GitRepository>>();
            
            // Inicializar o repositório com commit inicial
            CreateInitialCommit();
            
            _gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
        }
        
        [Fact]
        public void ImplementacaoDeveSerCompativel_ComInterface()
        {
            // Arrange & Assert
            Assert.IsAssignableFrom<IGitRepository>(_gitRepository);
        }
        
        [Fact]
        public async Task ObterCommitsPorPeriodoAsync_DeveRetornarLista()
        {
            // Arrange
            IGitRepository repository = _gitRepository;
            
            // Act
            var commits = await repository.ObterCommitsPorPeriodoAsync();
            
            // Assert
            Assert.NotNull(commits);
            Assert.Single(commits);
        }
        
        [Fact]
        public async Task ObterCommitsUltimosDiasAsync_DeveRetornarLista()
        {
            // Arrange
            IGitRepository repository = _gitRepository;
            
            // Act
            var commits = await repository.ObterCommitsUltimosDiasAsync(1);
            
            // Assert
            Assert.NotNull(commits);
            Assert.Single(commits);
        }
        
        [Fact]
        public async Task ObterCommitPorIdAsync_DeveRetornarCommit()
        {
            // Arrange
            IGitRepository repository = _gitRepository;
            string commitId = GetFirstCommitId();
            
            // Act
            var commit = await repository.ObterCommitPorIdAsync(commitId);
            
            // Assert
            Assert.NotNull(commit);
            Assert.Equal(commitId, commit!.Id);
        }
        
        [Fact]
        public async Task ObterMudancasNoCommitAsync_DeveRetornarMudancas()
        {
            // Arrange
            IGitRepository repository = _gitRepository;
            string commitId = GetFirstCommitId();
            
            // Act
            var mudancas = await repository.ObterMudancasNoCommitAsync(commitId);
            
            // Assert
            Assert.NotNull(mudancas);
            Assert.Single(mudancas);
        }
        
        [Fact]
        public async Task ObterConteudoArquivoNoCommitAsync_DeveRetornarConteudo()
        {
            // Arrange
            IGitRepository repository = _gitRepository;
            string commitId = GetFirstCommitId();
            
            // Act
            var conteudo = await repository.ObterConteudoArquivoNoCommitAsync(commitId, "file.txt");
            
            // Assert
            Assert.NotNull(conteudo);
            Assert.Equal("Conteúdo inicial", conteudo);
        }
        
        [Fact]
        public async Task ObterDiffArquivoAsync_DeveRetornarDiff()
        {
            // Arrange
            IGitRepository repository = _gitRepository;
            
            // Criar dois commits modificando o mesmo arquivo
            string filePath = Path.Combine(_testRepoPath, "arquivo_diff.txt");
            
            // Versão 1
            File.WriteAllText(filePath, "Versão 1");
            string commitId1 = CommitFile(filePath, "Arquivo diff - versão 1");
            
            // Versão 2
            File.WriteAllText(filePath, "Versão 2");
            string commitId2 = CommitFile(filePath, "Arquivo diff - versão 2");
            
            // Act
            var diff = await repository.ObterDiffArquivoAsync(commitId1, commitId2, "arquivo_diff.txt");
            
            // Assert
            Assert.NotNull(diff);
            Assert.Contains("-Versão 1", diff);
            Assert.Contains("+Versão 2", diff);
        }
        
        [Fact]
        public async Task ObterDiffCommitAsync_DeveRetornarDiff()
        {
            // Arrange
            IGitRepository repository = _gitRepository;
            
            // Criar um arquivo novo e commitá-lo
            string filePath = Path.Combine(_testRepoPath, "novo_arquivo.txt");
            File.WriteAllText(filePath, "Conteúdo do novo arquivo");
            string commitId = CommitFile(filePath, "Adicionado novo arquivo");
            
            // Act
            var diff = await repository.ObterDiffCommitAsync(commitId);
            
            // Assert
            Assert.NotNull(diff);
            Assert.Contains("+Conteúdo do novo arquivo", diff);
        }
        
        [Fact]
        public async Task ValidarRepositorioAsync_DeveRetornarTrue()
        {
            // Arrange
            IGitRepository repository = _gitRepository;
            
            // Act
            var result = await repository.ValidarRepositorioAsync(_testRepoPath);
            
            // Assert
            Assert.True(result);
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
        
        private string GetFirstCommitId()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                return repo.Commits.First().Sha;
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