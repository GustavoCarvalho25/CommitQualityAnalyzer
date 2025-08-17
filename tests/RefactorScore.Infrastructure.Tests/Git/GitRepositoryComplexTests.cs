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
    public class GitRepositoryComplexTests : IDisposable
    {
        private readonly string _testRepoPath;
        private readonly GitRepository _gitRepository;
        private readonly Mock<ILogger<GitRepository>> _loggerMock;
        
        public GitRepositoryComplexTests()
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
        public async Task ObterCommitsPorPeriodoAsync_MudarDataLimite_RetornaApenasCommitsNoIntervalo()
        {
            // Arrange
            // Criar commits com datas variadas
            CreateCommitWithDate("file2.txt", "Conteúdo do arquivo 2", "Commit de 10 dias atrás", DateTime.UtcNow.AddDays(-10));
            CreateCommitWithDate("file3.txt", "Conteúdo do arquivo 3", "Commit de 5 dias atrás", DateTime.UtcNow.AddDays(-5));
            CreateCommitWithDate("file4.txt", "Conteúdo do arquivo 4", "Commit de hoje", DateTime.UtcNow);
            
            // Act - Buscar commits dos últimos 7 dias
            var commits = await _gitRepository.ObterCommitsPorPeriodoAsync(DateTime.UtcNow.AddDays(-7));
            
            // Assert
            // Adicione um log para debug temporário para verificar os commits encontrados
            foreach (var commit in commits)
            {
                Console.WriteLine($"Commit encontrado: {commit.Mensagem} - {commit.Data}");
            }
            
            // Ajustar a expectativa para 3 commits: commit inicial + commit de 5 dias atrás + commit de hoje
            Assert.Equal(3, commits.Count);
            Assert.Contains(commits, c => c.Mensagem.Contains("Commit de 5 dias atrás"));
            Assert.Contains(commits, c => c.Mensagem.Contains("Commit de hoje"));
            Assert.DoesNotContain(commits, c => c.Mensagem.Contains("Commit de 10 dias atrás"));
        }
        
        [Fact]
        public async Task ObterMudancasNoCommitAsync_ArquivoModificado_RetornaDetalhesCorretos()
        {
            // Arrange
            // Criar um arquivo e modificá-lo em dois commits
            string filePathInicial = Path.Combine(_testRepoPath, "arquivo_modificado.txt");
            File.WriteAllText(filePathInicial, "Versão inicial do arquivo");
            
            string commitIdInicial = CommitFile(filePathInicial, "Versão inicial");
            
            // Modificar o arquivo
            File.WriteAllText(filePathInicial, "Versão inicial do arquivo\nLinha adicionada");
            string commitIdModificado = CommitFile(filePathInicial, "Arquivo modificado");
            
            // Act
            var mudancas = await _gitRepository.ObterMudancasNoCommitAsync(commitIdModificado);
            
            // Assert
            Assert.Single(mudancas);
            Assert.Equal("arquivo_modificado.txt", mudancas[0].CaminhoArquivo);
            Assert.Equal(TipoMudanca.Modificado, mudancas[0].TipoMudanca);
            Assert.Equal("Versão inicial do arquivo\nLinha adicionada", mudancas[0].ConteudoModificado);
            Assert.Equal("Versão inicial do arquivo", mudancas[0].ConteudoOriginal);
            Assert.True(mudancas[0].LinhasAdicionadas > 0);
            Assert.Contains("+Linha adicionada", mudancas[0].TextoDiff);
        }
        
        [Fact]
        public async Task ObterMudancasNoCommitAsync_ArquivoRenomeado_IdentificaRenomeacao()
        {
            // Arrange
            // Criar um arquivo
            string filePathOriginal = Path.Combine(_testRepoPath, "arquivo_original.txt");
            File.WriteAllText(filePathOriginal, "Conteúdo do arquivo");
            
            CommitFile(filePathOriginal, "Arquivo original");
            
            // Renomear o arquivo
            string filePathNovo = Path.Combine(_testRepoPath, "arquivo_renomeado.txt");
            File.Move(filePathOriginal, filePathNovo);
            
            string commitIdRenomeado = CommitAll("Arquivo renomeado");
            
            // Act
            var mudancas = await _gitRepository.ObterMudancasNoCommitAsync(commitIdRenomeado);
            
            // Assert
            Assert.Single(mudancas);
            Assert.Equal("arquivo_renomeado.txt", mudancas[0].CaminhoArquivo);
            Assert.Equal(TipoMudanca.Renomeado, mudancas[0].TipoMudanca);
            Assert.Equal("arquivo_original.txt", mudancas[0].CaminhoAntigo);
        }
        
        [Fact]
        public async Task ObterCommitsPorPeriodoAsync_RepositorioVazio_RetornaListaVazia()
        {
            // Arrange
            // Criar um repositório vazio
            var repoVazioPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(repoVazioPath);
            Repository.Init(repoVazioPath);
            
            var gitRepoVazio = new GitRepository(repoVazioPath, _loggerMock.Object);
            
            // Act
            var commits = await gitRepoVazio.ObterCommitsPorPeriodoAsync();
            
            // Assert
            Assert.Empty(commits);
            
            // Cleanup
            Directory.Delete(repoVazioPath, true);
        }
        
        [Fact]
        public async Task ObterDiffArquivoAsync_EntreCommits_RetornaDiferencasCorretas()
        {
            // Arrange
            // Criar arquivo e modificá-lo em commits sucessivos
            string filePath = Path.Combine(_testRepoPath, "arquivo_diff.txt");
            
            // Versão 1
            File.WriteAllText(filePath, "Linha 1\nLinha 2\nLinha 3");
            string commitId1 = CommitFile(filePath, "Versão 1");
            
            // Versão 2
            File.WriteAllText(filePath, "Linha 1\nLinha 2 modificada\nLinha 3\nLinha 4");
            string commitId2 = CommitFile(filePath, "Versão 2");
            
            // Act
            var diff = await _gitRepository.ObterDiffArquivoAsync(commitId1, commitId2, "arquivo_diff.txt");
            
            // Assert
            Assert.NotNull(diff);
            Assert.Contains("-Linha 2", diff);
            Assert.Contains("+Linha 2 modificada", diff);
            Assert.Contains("+Linha 4", diff);
        }
        
        [Fact]
        public async Task ObterConteudoArquivoNoCommitAsync_ArquivoInexistente_RetornaNulo()
        {
            // Arrange
            string commitId = GetFirstCommitId();
            
            // Act
            var conteudo = await _gitRepository.ObterConteudoArquivoNoCommitAsync(commitId, "arquivo_inexistente.txt");
            
            // Assert
            Assert.Null(conteudo);
        }
        
        [Fact]
        public async Task ObterMudancasNoCommitAsync_ArquivoBinario_ProcessaCorretamente()
        {
            // Arrange
            // Criar um arquivo binário simulado
            string binaryFilePath = Path.Combine(_testRepoPath, "arquivo.bin");
            using (var stream = File.Create(binaryFilePath))
            {
                byte[] data = new byte[1024];
                new Random().NextBytes(data);
                stream.Write(data, 0, data.Length);
            }
            
            string commitId = CommitFile(binaryFilePath, "Adicionado arquivo binário");
            
            // Act
            var mudancas = await _gitRepository.ObterMudancasNoCommitAsync(commitId);
            
            // Assert
            Assert.Single(mudancas);
            Assert.Equal("arquivo.bin", mudancas[0].CaminhoArquivo);
            Assert.Equal(TipoMudanca.Adicionado, mudancas[0].TipoMudanca);
            Assert.False(mudancas[0].EhCodigoFonte);
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
        
        private void CreateCommitWithDate(string fileName, string content, string message, DateTime date)
        {
            string filePath = Path.Combine(_testRepoPath, fileName);
            File.WriteAllText(filePath, content);
            
            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, filePath);
                
                var when = new DateTimeOffset(date);
                var author = new Signature("Test User", "test@example.com", when);
                repo.Commit(message, author, author);
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
        
        private string CommitAll(string message)
        {
            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, "*");
                
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