using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Moq;
using RefactorScore.Core.Entities;
using RefactorScore.Infrastructure.Git;
using Xunit;

namespace RefactorScore.Infrastructure.Tests.Git
{
    public class GitRepositoryEdgeCasesTests : IDisposable
    {
        private readonly string _testRepoPath;
        private readonly Mock<ILogger<GitRepository>> _loggerMock;
        
        public GitRepositoryEdgeCasesTests()
        {
            // Criar um repositório Git temporário para testes
            _testRepoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testRepoPath);
            
            Repository.Init(_testRepoPath);
            
            // Criar mock do logger
            _loggerMock = new Mock<ILogger<GitRepository>>();
        }
        
        [Fact]
        public async Task ObterMudancasNoCommitAsync_ArquivoGrande_ProcessaCorretamente()
        {
            // Arrange
            // Criar um arquivo grande (5MB)
            string filePath = Path.Combine(_testRepoPath, "arquivo_grande.txt");
            CreateLargeFile(filePath, 5 * 1024 * 1024);
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            string commitId = CommitFile(filePath, "Adicionado arquivo grande");
            
            // Act
            var mudancas = await gitRepository.ObterMudancasNoCommitAsync(commitId);
            
            // Assert
            Assert.Single(mudancas);
            Assert.Equal("arquivo_grande.txt", mudancas[0].CaminhoArquivo);
            Assert.Equal(TipoMudanca.Adicionado, mudancas[0].TipoMudanca);
            
            // Verificar se o tamanho do conteúdo corresponde ao arquivo criado
            Assert.NotNull(mudancas[0].ConteudoModificado);
            Assert.True(mudancas[0].ConteudoModificado!.Length > 0);
        }
        
        [Fact]
        public async Task ValidarRepositorioAsync_RepositorioCorrupto_TrataErroGraciosamente()
        {
            // Arrange
            // Criar um repositório Git e depois corromper o diretório .git
            string repoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(repoPath);
            Repository.Init(repoPath);
            
            // Corromper o repositório apagando arquivos críticos
            string headFilePath = Path.Combine(repoPath, ".git", "HEAD");
            if (File.Exists(headFilePath))
            {
                File.Delete(headFilePath);
            }
            
            var gitRepository = new GitRepository(repoPath, _loggerMock.Object);
            
            // Act
            var result = await gitRepository.ValidarRepositorioAsync(repoPath);
            
            // Assert
            Assert.False(result);
            
            // Cleanup
            Directory.Delete(repoPath, true);
        }
        
        [Fact]
        public async Task ObterConteudoArquivoNoCommitAsync_CommitInexistente_RetornaNulo()
        {
            // Arrange
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Commit ID inválido/inexistente
            string commitIdInexistente = "1234567890123456789012345678901234567890";
            
            // Act
            var conteudo = await gitRepository.ObterConteudoArquivoNoCommitAsync(
                commitIdInexistente, "qualquer_arquivo.txt");
            
            // Assert
            Assert.Null(conteudo);
        }
        
        [Fact]
        public async Task ObterDiffArquivoAsync_ArquivoExcluido_RetornaDiffIndicandoRemocao()
        {
            // Arrange
            // Criar arquivo
            string filePath = Path.Combine(_testRepoPath, "arquivo_para_remover.txt");
            File.WriteAllText(filePath, "Este arquivo será removido");
            
            // Commit inicial com o arquivo
            string commitIdInicial = CommitFile(filePath, "Adicionado arquivo");
            
            // Remover o arquivo
            File.Delete(filePath);
            string commitIdRemocao = CommitAll("Removido arquivo");
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var diff = await gitRepository.ObterDiffArquivoAsync(
                commitIdInicial, commitIdRemocao, "arquivo_para_remover.txt");
            
            // Assert
            Assert.NotNull(diff);
            Assert.Contains("-Este arquivo será removido", diff);
        }
        
        [Fact]
        public async Task ObterMudancasNoCommitAsync_MuitosArquivosModificados_ProcessaTodos()
        {
            // Arrange
            // Criar vários arquivos (50) em um único commit
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            const int numeroArquivos = 50;
            List<string> arquivos = new List<string>();
            
            for (int i = 1; i <= numeroArquivos; i++)
            {
                string filePath = Path.Combine(_testRepoPath, $"arquivo_{i}.txt");
                File.WriteAllText(filePath, $"Conteúdo do arquivo {i}");
                arquivos.Add(filePath);
            }
            
            string commitId = CommitAll("Adicionados múltiplos arquivos");
            
            // Act
            var mudancas = await gitRepository.ObterMudancasNoCommitAsync(commitId);
            
            // Assert
            Assert.Equal(numeroArquivos, mudancas.Count);
        }
        
        [Fact]
        public async Task ObterCommitsPorPeriodoAsync_PeriodoInvalido_RetornaListaVazia()
        {
            // Arrange
            // Data de início posterior à data de fim
            var dataInicio = DateTime.UtcNow.AddDays(10);
            var dataFim = DateTime.UtcNow.AddDays(-10);
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var commits = await gitRepository.ObterCommitsPorPeriodoAsync(dataInicio, dataFim);
            
            // Assert
            Assert.Empty(commits);
        }
        
        [Fact]
        public async Task ValidarRepositorioAsync_CaminhoInexistente_RetornaFalse()
        {
            // Arrange
            string caminhoInexistente = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var result = await gitRepository.ValidarRepositorioAsync(caminhoInexistente);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public async Task ObterDiffCommitAsync_CommitComArquivosBinarios_ProcessaCorretamente()
        {
            // Arrange
            // Criar um arquivo binário
            string binaryFilePath = Path.Combine(_testRepoPath, "arquivo.bin");
            using (var fs = File.Create(binaryFilePath))
            {
                byte[] data = new byte[1024];
                new Random().NextBytes(data);
                fs.Write(data, 0, data.Length);
            }
            
            string commitId = CommitFile(binaryFilePath, "Adicionado arquivo binário");
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var diff = await gitRepository.ObterDiffCommitAsync(commitId);
            
            // Assert
            Assert.NotNull(diff);
            // Verificar se o diff contém indicação de arquivo binário
            Assert.Contains("Binary", diff, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public async Task ObterMudancasNoCommitAsync_ArquivoComCaracteresEspeciais_ProcessaCorretamente()
        {
            // Arrange
            // Criar arquivo com caracteres especiais no nome e conteúdo
            string fileNameWithSpecialChars = "arquivo_€$ç@ão_especial.txt";
            string filePath = Path.Combine(_testRepoPath, fileNameWithSpecialChars);
            
            string contentWithSpecialChars = "Conteúdo com caracteres especiais: áéíóú ÁéÍÓÚ çÇ ãÃ õÕ";
            File.WriteAllText(filePath, contentWithSpecialChars, Encoding.UTF8);
            
            string commitId = CommitFile(filePath, "Adicionado arquivo com caracteres especiais");
            
            var gitRepository = new GitRepository(_testRepoPath, _loggerMock.Object);
            
            // Act
            var mudancas = await gitRepository.ObterMudancasNoCommitAsync(commitId);
            
            // Assert
            Assert.Single(mudancas);
            Assert.Equal(fileNameWithSpecialChars, mudancas[0].CaminhoArquivo);
            Assert.Equal(contentWithSpecialChars, mudancas[0].ConteudoModificado);
        }
        
        #region Helpers
        
        private void CreateLargeFile(string filePath, int sizeInBytes)
        {
            const int bufferSize = 4096;
            byte[] buffer = new byte[bufferSize];
            Random random = new Random();
            
            using (var fs = File.Create(filePath))
            {
                int remainingBytes = sizeInBytes;
                
                while (remainingBytes > 0)
                {
                    int bytesToWrite = Math.Min(bufferSize, remainingBytes);
                    random.NextBytes(buffer);
                    fs.Write(buffer, 0, bytesToWrite);
                    remainingBytes -= bytesToWrite;
                }
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