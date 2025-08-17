using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Moq;
using RefactorScore.Domain.Entities;
using RefactorScore.Infrastructure.Git;
using Xunit;

namespace RefactorScore.Infrastructure.Tests.Git
{
    public class GitRepositoryDiffOptionsTests : IDisposable
    {
        private readonly string _testRepoPath;
        private readonly Mock<ILogger<GitRepository>> _loggerMock;
        
        public GitRepositoryDiffOptionsTests()
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
        public async Task ObterDiffArquivoAsync_UsaPatchOptionsCorretamente()
        {
            // Arrange
            // Adicionar um arquivo e modificá-lo
            string filePath = Path.Combine(_testRepoPath, "arquivo_diff.txt");
            
            // Versão 1
            File.WriteAllText(filePath, "Linha 1\nLinha 2\nLinha 3");
            string commitId1 = CommitFile(filePath, "Versão 1");
            
            // Versão 2 - Adicionar várias linhas para testar o contexto
            File.WriteAllText(filePath, "Linha 1\nLinha 2 modificada\nLinha 3\nLinha 4\nLinha 5\nLinha 6\nLinha 7\nLinha 8");
            string commitId2 = CommitFile(filePath, "Versão 2");
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var diff = await gitRepository.ObterDiffArquivoAsync(commitId1, commitId2, "arquivo_diff.txt");
            
            // Assert
            Console.WriteLine("Diff obtido:");
            Console.WriteLine(diff);
            
            Assert.NotNull(diff);
            Assert.Contains("-Linha 2", diff);
            Assert.Contains("+Linha 2 modificada", diff);
            Assert.Contains("+Linha 4", diff);
            
            // Verificar se o diff contém as informações essenciais sem depender do formato exato do contexto
            Assert.Contains("Linha 1", diff); // Linha de contexto acima
            Assert.Contains("Linha 3", diff); // Linha de contexto abaixo
        }
        
        [Fact]
        public async Task ObterDiffCommitAsync_UsaPatchOptionsCorretamente()
        {
            // Arrange
            // Modificar múltiplos arquivos em um commit
            string filePath1 = Path.Combine(_testRepoPath, "arquivo1.txt");
            string filePath2 = Path.Combine(_testRepoPath, "arquivo2.txt");
            
            File.WriteAllText(filePath1, "Arquivo 1 - Conteúdo inicial");
            File.WriteAllText(filePath2, "Arquivo 2 - Conteúdo inicial");
            
            CommitAll("Commit inicial com dois arquivos");
            
            // Modificar ambos os arquivos
            File.WriteAllText(filePath1, "Arquivo 1 - Conteúdo modificado");
            File.WriteAllText(filePath2, "Arquivo 2 - Conteúdo modificado");
            
            string commitId = CommitAll("Modificados dois arquivos");
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var diff = await gitRepository.ObterDiffCommitAsync(commitId);
            
            // Assert
            Assert.NotNull(diff);
            Assert.Contains("Arquivo 1 - Conteúdo modificado", diff);
            Assert.Contains("Arquivo 2 - Conteúdo modificado", diff);
            Assert.Contains("-Arquivo 1 - Conteúdo inicial", diff);
            Assert.Contains("-Arquivo 2 - Conteúdo inicial", diff);
        }
        
        [Fact]
        public async Task ObterMudancasNoCommitAsync_ConfiguraPatchOptionsCorretamente()
        {
            // Arrange
            // Adicionar um arquivo com muitas linhas para testar configurações de contexto
            string filePath = Path.Combine(_testRepoPath, "arquivo_grande.txt");
            StringBuilder sb = new StringBuilder();
            for (int i = 1; i <= 100; i++)
            {
                sb.AppendLine($"Linha {i}");
            }
            File.WriteAllText(filePath, sb.ToString());
            
            string commitId1 = CommitFile(filePath, "Arquivo com muitas linhas");
            
            // Modificar apenas algumas linhas no meio
            sb.Clear();
            for (int i = 1; i <= 50; i++)
            {
                sb.AppendLine($"Linha {i}");
            }
            // Modificar a linha 51
            sb.AppendLine("Linha 51 - MODIFICADA");
            for (int i = 52; i <= 100; i++)
            {
                sb.AppendLine($"Linha {i}");
            }
            File.WriteAllText(filePath, sb.ToString());
            
            string commitId2 = CommitFile(filePath, "Uma linha modificada");
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var mudancas = await gitRepository.ObterMudancasNoCommitAsync(commitId2);
            
            // Assert
            Assert.Single(mudancas);
            Assert.Equal("arquivo_grande.txt", mudancas[0].CaminhoArquivo);
            Assert.Equal(TipoMudanca.Modificado, mudancas[0].TipoMudanca);
            Assert.Contains("-Linha 51", mudancas[0].TextoDiff);
            Assert.Contains("+Linha 51 - MODIFICADA", mudancas[0].TextoDiff);
            
            // Verificar se o contexto está limitado em torno da mudança
            // e não inclui todas as 100 linhas
            Assert.True(mudancas[0].TextoDiff.Length < sb.Length);
        }
        
        [Fact]
        public async Task ObterDiffArquivoAsync_OpcoesPersonalizadas_AplicaCorretamente()
        {
            // Arrange
            // Adicionar um arquivo e fazer várias modificações em diferentes partes
            string filePath = Path.Combine(_testRepoPath, "arquivo_opcoes.txt");
            
            StringBuilder sb = new StringBuilder();
            for (int i = 1; i <= 50; i++)
            {
                sb.AppendLine($"Linha original {i}");
            }
            File.WriteAllText(filePath, sb.ToString());
            
            string commitId1 = CommitFile(filePath, "Versão original");
            
            // Modificar linhas espalhadas pelo arquivo
            sb.Clear();
            sb.AppendLine("Linha modificada 1"); // Modificação no início
            for (int i = 2; i <= 20; i++)
            {
                sb.AppendLine($"Linha original {i}");
            }
            sb.AppendLine("Linha modificada 21"); // Modificação no meio
            for (int i = 22; i <= 49; i++)
            {
                sb.AppendLine($"Linha original {i}");
            }
            sb.AppendLine("Linha modificada 50"); // Modificação no fim
            
            File.WriteAllText(filePath, sb.ToString());
            string commitId2 = CommitFile(filePath, "Modificações espalhadas");
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var diff = await gitRepository.ObterDiffArquivoAsync(commitId1, commitId2, "arquivo_opcoes.txt");
            
            // Assert
            Assert.NotNull(diff);
            Assert.Contains("-Linha original 1", diff);
            Assert.Contains("+Linha modificada 1", diff);
            Assert.Contains("-Linha original 21", diff);
            Assert.Contains("+Linha modificada 21", diff);
            Assert.Contains("-Linha original 50", diff);
            Assert.Contains("+Linha modificada 50", diff);
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