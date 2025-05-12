using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RefactorScore.Core.Entities;
using RefactorScore.Core.Interfaces;
using RefactorScore.Core.Specifications;
using RefactorScore.WorkerService;
using RefactorScore.WorkerService.Workers;
using Xunit;

namespace RefactorScore.Integration.Tests
{
    public class WorkerIntegrationTests
    {
        private readonly Mock<ICodeAnalyzerService> _mockCodeAnalyzerService;
        private readonly Mock<ILLMService> _mockLlmService;
        private readonly Mock<IGitRepository> _mockGitRepository;
        private readonly Mock<IAnalysisRepository> _mockAnalysisRepository;
        private readonly Mock<ICacheService> _mockCacheService;
        private readonly Mock<ILogger<CommitAnalysisWorker>> _mockLogger;
        private readonly WorkerOptions _options;
        private readonly CommitAnalysisWorker _worker;

        public WorkerIntegrationTests()
        {
            _mockCodeAnalyzerService = new Mock<ICodeAnalyzerService>();
            _mockLlmService = new Mock<ILLMService>();
            _mockGitRepository = new Mock<IGitRepository>();
            _mockAnalysisRepository = new Mock<IAnalysisRepository>();
            _mockCacheService = new Mock<ICacheService>();
            _mockLogger = new Mock<ILogger<CommitAnalysisWorker>>();

            _options = new WorkerOptions
            {
                ScanIntervalMinutes = 1, // Short interval for tests
                MaxProcessingCommits = 2
            };

            var mockOptions = new Mock<IOptions<WorkerOptions>>();
            mockOptions.Setup(m => m.Value).Returns(_options);

            _worker = new CommitAnalysisWorker(
                _mockCodeAnalyzerService.Object,
                _mockLlmService.Object,
                _mockGitRepository.Object,
                _mockAnalysisRepository.Object,
                _mockCacheService.Object,
                mockOptions.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task ExecuteAsync_ShouldNotRunWhenLLMIsNotAvailable()
        {
            // Arrange
            _mockLlmService.Setup(m => m.IsAvailableAsync()).ReturnsAsync(false);

            // Act - Using PrivateObject to invoke protected method
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _worker.StartAsync(cts.Token);
            await Task.Delay(100); // Give a little time for execution
            await _worker.StopAsync(cts.Token);

            // Assert
            _mockCodeAnalyzerService.Verify(m => m.GetRecentCommitsAsync(), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessCommits_WhenLLMIsAvailable()
        {
            // Arrange
            _mockLlmService.Setup(m => m.IsAvailableAsync()).ReturnsAsync(true);
            
            var commits = new List<CommitInfo>
            {
                new CommitInfo { Id = "commit1", Author = "User1" },
                new CommitInfo { Id = "commit2", Author = "User2" }
            };
            
            _mockCodeAnalyzerService.Setup(m => m.GetRecentCommitsAsync())
                .ReturnsAsync(Result<IEnumerable<CommitInfo>>.Success(commits));
            
            var fileChanges = new List<CommitFileChange>
            {
                new CommitFileChange { Path = "file1.cs", Status = FileChangeType.Modified },
                new CommitFileChange { Path = "file2.cs", Status = FileChangeType.Added }
            };
            
            _mockCodeAnalyzerService.Setup(m => m.GetCommitChangesAsync(It.IsAny<string>()))
                .ReturnsAsync(Result<IEnumerable<CommitFileChange>>.Success(fileChanges));
            
            var analysis = new CodeAnalysis
            {
                Id = "analysis1",
                CommitId = "commit1",
                FilePath = "file1.cs",
                OverallScore = 8.5
            };
            
            _mockCodeAnalyzerService.Setup(m => m.AnalyzeCommitFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Result<CodeAnalysis>.Success(analysis));

            // Act - Start the worker briefly
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _worker.StartAsync(cts.Token);
            await Task.Delay(1000); // Give time for execution
            await _worker.StopAsync(cts.Token);

            // Assert
            _mockCodeAnalyzerService.Verify(m => m.GetRecentCommitsAsync(), Times.AtLeastOnce);
            _mockCodeAnalyzerService.Verify(m => m.GetCommitChangesAsync(It.IsAny<string>()), Times.AtLeastOnce);
            _mockCodeAnalyzerService.Verify(m => m.AnalyzeCommitFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
        }
    }
} 