using LibGit2Sharp;
using RefactorScore.Domain.Entities;
using RefactorScore.Infrastructure.Git;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;

namespace RefactorScore.Infrastructure.Tests.Git
{
    public class GitRepositoryTests : IDisposable
    {
        private readonly string _testRepoPath;
        private readonly GitRepository _gitRepository;
        private readonly Mock<ILogger<GitRepository>> _loggerMock;
        
        public GitRepositoryTests()
        {
            // Criar um repositório Git temporário para testes
            _testRepoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testRepoPath);
            
            Repository.Init(_testRepoPath);
            
            // Inicializar o repositório com um commit inicial
            CreateInitialCommit();
            
            // Criar mock do logger
            _loggerMock = new Mock<ILogger<GitRepository>>();
            
            _gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
        }
        
        [Fact]
        public async Task ValidarRepositorioAsync_RepositorioValido_RetornaTrue()
        {
            // Act
            var result = await _gitRepository.ValidarRepositorioAsync(_testRepoPath);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public async Task ValidarRepositorioAsync_RepositorioInvalido_RetornaFalse()
        {
            // Arrange
            var pathInvalido = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(pathInvalido);
            
            // Act
            var result = await _gitRepository.ValidarRepositorioAsync(pathInvalido);
            
            // Assert
            Assert.False(result);
            
            // Cleanup
            Directory.Delete(pathInvalido);
        }
        
        [Fact]
        public async Task ObterCommitPorIdAsync_CommitExistente_RetornaCommit()
        {
            // Arrange
            string commitId = GetFirstCommitId();
            
            // Act
            var commit = await _gitRepository.ObterCommitPorIdAsync(commitId);
            
            // Assert
            Assert.NotNull(commit);
            Assert.Equal(commitId, commit!.Id);
            Assert.Equal("Initial commit", commit.Mensagem.TrimEnd());
        }
        
        [Fact]
        public async Task ObterCommitPorIdAsync_CommitInexistente_RetornaNulo()
        {
            // Arrange
            string commitIdInexistente = "1234567890123456789012345678901234567890";
            
            // Act
            var commit = await _gitRepository.ObterCommitPorIdAsync(commitIdInexistente);
            
            // Assert
            Assert.Null(commit);
        }
        
        [Fact]
        public async Task ObterCommitsPorPeriodoAsync_PeriodoValido_RetornaCommits()
        {
            // Arrange
            var dataInicio = DateTime.UtcNow.AddDays(-1);
            var dataFim = DateTime.UtcNow.AddDays(1);
            
            // Act
            var commits = await _gitRepository.ObterCommitsPorPeriodoAsync(dataInicio, dataFim);
            
            // Assert
            Assert.NotEmpty(commits);
            Assert.Single(commits);
            Assert.Equal("Initial commit", commits[0].Mensagem.TrimEnd());
        }
        
        [Fact]
        public async Task ObterCommitsUltimosDiasAsync_UltimoDia_RetornaCommits()
        {
            // Act
            var commits = await _gitRepository.ObterCommitsUltimosDiasAsync(1);
            
            // Assert
            Assert.NotEmpty(commits);
            Assert.Single(commits);
            Assert.Equal("Initial commit", commits[0].Mensagem.TrimEnd());
        }
        
        [Fact]
        public async Task ObterMudancasNoCommitAsync_CommitInicial_RetornaArquivosAdicionados()
        {
            // Arrange
            string commitId = GetFirstCommitId();
            
            // Act
            var mudancas = await _gitRepository.ObterMudancasNoCommitAsync(commitId);
            
            // Assert
            Assert.Single(mudancas);
            Assert.Equal("file.txt", mudancas[0].CaminhoArquivo);
            Assert.Equal(TipoMudanca.Adicionado, mudancas[0].TipoMudanca);
            Assert.Equal("Conteúdo inicial", mudancas[0].ConteudoModificado);
        }
        
        [Fact]
        public async Task ObterConteudoArquivoNoCommitAsync_ArquivoExistente_RetornaConteudo()
        {
            // Arrange
            string commitId = GetFirstCommitId();
            
            // Act
            var conteudo = await _gitRepository.ObterConteudoArquivoNoCommitAsync(commitId, "file.txt");
            
            // Assert
            Assert.Equal("Conteúdo inicial", conteudo);
        }
        
        [Fact]
        public async Task ObterDiffCommitAsync_CommitInicial_RetornaDiff()
        {
            // Arrange
            string commitId = GetFirstCommitId();
            
            // Act
            var diff = await _gitRepository.ObterDiffCommitAsync(commitId);
            
            // Assert
            Assert.NotNull(diff);
            Assert.Contains("+Conteúdo inicial", diff!);
        }
        
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