using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RefactorScore.Core.Entities;
using RefactorScore.Infrastructure.GitIntegration;
using Xunit;

namespace RefactorScore.Integration.Tests
{
    public class GitRepositoryIntegrationTests
    {
        private readonly string _testRepoPath;
        private readonly GitRepository _gitRepository;
        private readonly Mock<ILogger<GitRepository>> _mockLogger;

        public GitRepositoryIntegrationTests()
        {
            // Caminho para um repositório de teste
            _testRepoPath = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..");
            
            _mockLogger = new Mock<ILogger<GitRepository>>();
            
            var options = new GitRepositoryOptions
            {
                RepositoryPath = _testRepoPath
            };
            
            var mockOptions = new Mock<IOptions<GitRepositoryOptions>>();
            mockOptions.Setup(o => o.Value).Returns(options);
            
            _gitRepository = new GitRepository(mockOptions.Object, _mockLogger.Object);
        }
        
        [Fact]
        public async Task GetLastDayCommitsAsync_ShouldReturnCommits()
        {
            // Act
            var commits = await _gitRepository.GetLastDayCommitsAsync();
            
            // Assert
            Assert.NotNull(commits);
            // Não garantimos que haja commits nas últimas 24h, então apenas verificamos se não é null
        }
        
        [Fact]
        public async Task GetCommitByIdAsync_WithValidId_ShouldReturnCommit()
        {
            // Arrange
            // Obter os últimos commits para encontrar um ID válido para teste
            var recentCommits = await _gitRepository.GetLastDayCommitsAsync();
            var commitId = recentCommits.FirstOrDefault()?.Id;
            
            // Skip test if no recent commits
            if (string.IsNullOrEmpty(commitId))
            {
                return;
            }
            
            // Act
            var commit = await _gitRepository.GetCommitByIdAsync(commitId);
            
            // Assert
            Assert.NotNull(commit);
            Assert.Equal(commitId, commit.Id);
        }
        
        [Fact]
        public async Task GetCommitByIdAsync_WithInvalidId_ShouldReturnNull()
        {
            // Act
            var commit = await _gitRepository.GetCommitByIdAsync("invalid_commit_id");
            
            // Assert
            Assert.Null(commit);
        }
        
        [Fact]
        public async Task GetCommitChangesAsync_WithValidId_ShouldReturnChanges()
        {
            // Arrange
            // Obter os últimos commits para encontrar um ID válido para teste
            var recentCommits = await _gitRepository.GetLastDayCommitsAsync();
            var commitId = recentCommits.FirstOrDefault()?.Id;
            
            // Skip test if no recent commits
            if (string.IsNullOrEmpty(commitId))
            {
                return;
            }
            
            // Act
            var changes = await _gitRepository.GetCommitChangesAsync(commitId);
            
            // Assert
            Assert.NotNull(changes);
        }
        
        [Fact]
        public async Task GetFileContentAtRevisionAsync_WithValidIdAndPath_ShouldReturnContent()
        {
            // Arrange
            // Obter os últimos commits para encontrar um ID válido para teste
            var recentCommits = await _gitRepository.GetLastDayCommitsAsync();
            var commitId = recentCommits.FirstOrDefault()?.Id;
            
            // Skip test if no recent commits
            if (string.IsNullOrEmpty(commitId))
            {
                return;
            }
            
            // Obter as alterações para encontrar um arquivo válido
            var changes = await _gitRepository.GetCommitChangesAsync(commitId);
            var filePath = changes.FirstOrDefault()?.FilePath ?? changes.FirstOrDefault()?.Path;
            
            // Skip test if no files changed
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }
            
            // Act
            var content = await _gitRepository.GetFileContentAtRevisionAsync(commitId, filePath);
            
            // Assert
            Assert.NotNull(content);
            Assert.NotEmpty(content);
        }
    }
} 