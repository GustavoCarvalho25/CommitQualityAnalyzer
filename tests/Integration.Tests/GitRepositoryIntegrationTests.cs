using System;
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
    public class GitRepositoryIntegrationTests : IDisposable
    {
        private readonly string _testRepoPath;
        private readonly GitRepositoryOptions _options;
        private readonly Mock<ILogger<GitRepository>> _mockLogger;
        private GitRepository _gitRepository;

        public GitRepositoryIntegrationTests()
        {
            // Create a temporary directory for test repo
            _testRepoPath = Path.Combine(Path.GetTempPath(), "RefactorScoreTest_" + Guid.NewGuid());
            Directory.CreateDirectory(_testRepoPath);

            _options = new GitRepositoryOptions
            {
                RepositoryPath = _testRepoPath
            };

            var mockOptions = new Mock<IOptions<GitRepositoryOptions>>();
            mockOptions.Setup(m => m.Value).Returns(_options);

            _mockLogger = new Mock<ILogger<GitRepository>>();
            
            // Skip actual setup until test needs it to avoid unnecessary Git operations
        }

        [Fact(Skip = "Requires actual Git repository")]
        public async Task GetLastDayCommitsAsync_ShouldReturnRecentCommits()
        {
            // Arrange - Initialize repository with actual Git commands
            await InitializeTestRepository();
            _gitRepository = new GitRepository(_mockLogger.Object, Options.Create(_options));

            // Act
            var commits = await _gitRepository.GetLastDayCommitsAsync();

            // Assert
            Assert.NotNull(commits);
            Assert.True(commits.Any(), "Should have at least one commit");
            var commit = commits.First();
            Assert.NotNull(commit.Id);
            Assert.NotNull(commit.Author);
            Assert.True(commit.Date > DateTime.UtcNow.AddDays(-2), "Commit should be recent");
        }

        [Fact(Skip = "Requires actual Git repository")]
        public async Task GetCommitChangesAsync_ShouldReturnFileChanges()
        {
            // Arrange - Initialize repository with actual Git commands
            await InitializeTestRepository();
            _gitRepository = new GitRepository(_mockLogger.Object, Options.Create(_options));
            
            // Get most recent commit
            var commits = await _gitRepository.GetLastDayCommitsAsync();
            var commitId = commits.First().Id;

            // Act
            var changes = await _gitRepository.GetCommitChangesAsync(commitId);

            // Assert
            Assert.NotNull(changes);
            Assert.True(changes.Any(), "Should have at least one file change");
            var change = changes.First();
            Assert.NotNull(change.Path);
            Assert.NotEqual(FileChangeType.Unknown, change.Status);
        }

        [Fact(Skip = "Requires actual Git repository")]
        public async Task GetFileContentAtRevisionAsync_ShouldReturnFileContent()
        {
            // Arrange - Initialize repository with actual Git commands
            await InitializeTestRepository();
            _gitRepository = new GitRepository(_mockLogger.Object, Options.Create(_options));
            
            // Get most recent commit and its changes
            var commits = await _gitRepository.GetLastDayCommitsAsync();
            var commitId = commits.First().Id;
            var changes = await _gitRepository.GetCommitChangesAsync(commitId);
            var filePath = changes.First().Path;

            // Act
            var content = await _gitRepository.GetFileContentAtRevisionAsync(commitId, filePath);

            // Assert
            Assert.NotNull(content);
            Assert.NotEmpty(content);
        }

        private async Task InitializeTestRepository()
        {
            // Implement this if you want to run integration tests against an actual repo
            // Skip for test skeletal structure
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            // Clean up test repo directory
            try
            {
                if (Directory.Exists(_testRepoPath))
                {
                    Directory.Delete(_testRepoPath, true);
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }
    }
} 