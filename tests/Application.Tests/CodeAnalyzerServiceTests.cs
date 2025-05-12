using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RefactorScore.Application.Services;
using RefactorScore.Core.Entities;
using RefactorScore.Core.Interfaces;
using Xunit;

namespace RefactorScore.Application.Tests
{
    public class CodeAnalyzerServiceTests
    {
        private readonly Mock<IGitRepository> _mockGitRepository;
        private readonly Mock<ILLMService> _mockLlmService;
        private readonly Mock<IAnalysisRepository> _mockAnalysisRepository;
        private readonly Mock<ICacheService> _mockCacheService;
        private readonly Mock<ILogger<CodeAnalyzerService>> _mockLogger;
        private readonly CodeAnalyzerOptions _options;
        private readonly CodeAnalyzerService _service;

        public CodeAnalyzerServiceTests()
        {
            _mockGitRepository = new Mock<IGitRepository>();
            _mockLlmService = new Mock<ILLMService>();
            _mockAnalysisRepository = new Mock<IAnalysisRepository>();
            _mockCacheService = new Mock<ICacheService>();
            _mockLogger = new Mock<ILogger<CodeAnalyzerService>>();
            _options = new CodeAnalyzerOptions
            {
                ModelName = "testmodel",
                MaxCodeLength = 1000,
                MaxDiffLength = 500
            };

            var mockOptions = new Mock<IOptions<CodeAnalyzerOptions>>();
            mockOptions.Setup(m => m.Value).Returns(_options);

            _service = new CodeAnalyzerService(
                _mockGitRepository.Object,
                _mockLlmService.Object,
                _mockAnalysisRepository.Object,
                _mockCacheService.Object,
                mockOptions.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task GetRecentCommitsAsync_ShouldReturnCommits_WhenGitRepoHasCommits()
        {
            // Arrange
            var expectedCommits = new List<CommitInfo>
            {
                new CommitInfo { Id = "commit1", Author = "User1", Message = "Fix bug" },
                new CommitInfo { Id = "commit2", Author = "User2", Message = "Add feature" }
            };

            _mockGitRepository
                .Setup(x => x.GetLastDayCommitsAsync())
                .ReturnsAsync(expectedCommits);

            // Act
            var result = await _service.GetRecentCommitsAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(expectedCommits, result.Data);
        }

        [Fact]
        public async Task GetRecentCommitsAsync_ShouldReturnFailResult_WhenExceptionOccurs()
        {
            // Arrange
            _mockGitRepository
                .Setup(x => x.GetLastDayCommitsAsync())
                .ThrowsAsync(new Exception("Git error"));

            // Act
            var result = await _service.GetRecentCommitsAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Single(result.Errors);
            Assert.Equal("Git error", result.Errors[0].Message);
        }

        [Fact]
        public async Task GetCommitChangesAsync_ShouldReturnFromCache_WhenCacheContainsChanges()
        {
            // Arrange
            string commitId = "commit123";
            var expectedChanges = new List<CommitFileChange>
            {
                new CommitFileChange { Path = "file1.cs", Status = FileChangeType.Modified },
                new CommitFileChange { Path = "file2.cs", Status = FileChangeType.Added }
            };

            _mockCacheService
                .Setup(x => x.GetAsync<IEnumerable<CommitFileChange>>(It.IsAny<string>()))
                .ReturnsAsync(expectedChanges);

            // Act
            var result = await _service.GetCommitChangesAsync(commitId);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(expectedChanges, result.Data);
            _mockGitRepository.Verify(x => x.GetCommitChangesAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetCommitChangesAsync_ShouldFetchFromGitAndCacheResults_WhenCacheEmpty()
        {
            // Arrange
            string commitId = "commit123";
            var expectedChanges = new List<CommitFileChange>
            {
                new CommitFileChange { Path = "file1.cs", Status = FileChangeType.Modified },
                new CommitFileChange { Path = "file2.cs", Status = FileChangeType.Added }
            };

            _mockCacheService
                .Setup(x => x.GetAsync<IEnumerable<CommitFileChange>>(It.IsAny<string>()))
                .ReturnsAsync((IEnumerable<CommitFileChange>)null);

            _mockGitRepository
                .Setup(x => x.GetCommitChangesAsync(commitId))
                .ReturnsAsync(expectedChanges);

            // Act
            var result = await _service.GetCommitChangesAsync(commitId);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(expectedChanges, result.Data);
            _mockGitRepository.Verify(x => x.GetCommitChangesAsync(commitId), Times.Once);
            _mockCacheService.Verify(
                x => x.SetAsync(
                    It.IsAny<string>(), 
                    It.Is<IEnumerable<CommitFileChange>>(c => c == expectedChanges), 
                    It.IsAny<TimeSpan>()
                ), 
                Times.Once
            );
        }
    }
} 