using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RefactorScore.Core.Entities;
using RefactorScore.Infrastructure.MongoDB;
using RefactorScore.Infrastructure.RedisCache;
using Xunit;

namespace RefactorScore.Integration.Tests
{
    public class RepositoryIntegrationTests
    {
        [Collection("MongoDB Integration Tests")]
        public class MongoDbAnalysisRepositoryTests
        {
            private readonly Mock<ILogger<MongoDbAnalysisRepository>> _mockLogger;
            private readonly MongoDbOptions _options;
            private readonly MongoDbAnalysisRepository _repository;

            public MongoDbAnalysisRepositoryTests()
            {
                _mockLogger = new Mock<ILogger<MongoDbAnalysisRepository>>();
                
                _options = new MongoDbOptions
                {
                    ConnectionString = "mongodb://admin:admin123@localhost:27017",
                    DatabaseName = "RefactorScore_Test",
                    CollectionName = "CodeAnalyses_Test"
                };

                var mockOptions = new Mock<IOptions<MongoDbOptions>>();
                mockOptions.Setup(m => m.Value).Returns(_options);

                _repository = new MongoDbAnalysisRepository(_mockLogger.Object, mockOptions.Object);
            }

            [Fact(Skip = "Requires MongoDB running")]
            public async Task SaveAndRetrieveAnalysisAsync()
            {
                // Arrange
                var analysis = new CodeAnalysis
                {
                    Id = Guid.NewGuid().ToString(),
                    CommitId = "test-commit-id",
                    FilePath = "src/test.cs",
                    Author = "Test User",
                    CommitDate = DateTime.UtcNow.AddHours(-1),
                    AnalysisDate = DateTime.UtcNow,
                    OverallScore = 8.5,
                    Justification = "Test justification",
                    CleanCodeAnalysis = new CleanCodeAnalysis
                    {
                        VariableNaming = 8,
                        FunctionSize = 9,
                        CommentUsage = 7,
                        MethodCohesion = 8,
                        DeadCodeAvoidance = 10
                    }
                };

                try
                {
                    // Act - Save
                    await _repository.SaveAnalysisAsync(analysis);
                    
                    // Act - Retrieve
                    var retrievedAnalysis = await _repository.GetAnalysisByIdAsync(analysis.Id);
                    
                    // Assert
                    Assert.NotNull(retrievedAnalysis);
                    Assert.Equal(analysis.Id, retrievedAnalysis.Id);
                    Assert.Equal(analysis.CommitId, retrievedAnalysis.CommitId);
                    Assert.Equal(analysis.FilePath, retrievedAnalysis.FilePath);
                    Assert.Equal(analysis.Author, retrievedAnalysis.Author);
                    Assert.Equal(analysis.OverallScore, retrievedAnalysis.OverallScore);
                    
                    // Act - Retrieve by commit and file
                    var fileAnalysis = await _repository.GetAnalysisByCommitAndFileAsync(analysis.CommitId, analysis.FilePath);
                    
                    // Assert
                    Assert.NotNull(fileAnalysis);
                    Assert.Equal(analysis.Id, fileAnalysis.Id);
                    
                    // Act - Get all analyses for commit
                    var analyses = await _repository.GetAnalysesByCommitIdAsync(analysis.CommitId);
                    
                    // Assert
                    Assert.NotNull(analyses);
                    Assert.Contains(analyses, a => a.Id == analysis.Id);
                }
                finally
                {
                    // Cleanup - Remove test data
                    await _repository.DeleteAnalysisAsync(analysis.Id);
                }
            }
        }

        [Collection("Redis Integration Tests")]
        public class RedisCacheServiceTests
        {
            private readonly Mock<ILogger<RedisCacheService>> _mockLogger;
            private readonly RedisCacheOptions _options;
            private readonly RedisCacheService _cacheService;

            public RedisCacheServiceTests()
            {
                _mockLogger = new Mock<ILogger<RedisCacheService>>();
                
                _options = new RedisCacheOptions
                {
                    ConnectionString = "localhost:6379",
                    KeyPrefix = "test_refactorscore",
                    DatabaseId = 0,
                    DefaultExpiryHours = 1
                };

                var mockOptions = new Mock<IOptions<RedisCacheOptions>>();
                mockOptions.Setup(m => m.Value).Returns(_options);

                _cacheService = new RedisCacheService(_mockLogger.Object, mockOptions.Object);
            }

            [Fact(Skip = "Requires Redis running")]
            public async Task SetAndGetAsync_ShouldWorkCorrectly()
            {
                // Arrange
                string key = "test_key";
                string value = "test_value";
                
                try
                {
                    // Act - Set
                    await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
                    
                    // Act - Get
                    var retrievedValue = await _cacheService.GetAsync<string>(key);
                    
                    // Assert
                    Assert.NotNull(retrievedValue);
                    Assert.Equal(value, retrievedValue);
                }
                finally
                {
                    // Cleanup
                    await _cacheService.RemoveAsync(key);
                }
            }

            [Fact(Skip = "Requires Redis running")]
            public async Task SetAndGetComplexObjectAsync_ShouldWorkCorrectly()
            {
                // Arrange
                string key = "test_complex_object";
                var value = new CodeAnalysis
                {
                    Id = "test-id",
                    CommitId = "test-commit",
                    FilePath = "test.cs",
                    OverallScore = 8.0
                };
                
                try
                {
                    // Act - Set
                    await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
                    
                    // Act - Get
                    var retrievedValue = await _cacheService.GetAsync<CodeAnalysis>(key);
                    
                    // Assert
                    Assert.NotNull(retrievedValue);
                    Assert.Equal(value.Id, retrievedValue.Id);
                    Assert.Equal(value.CommitId, retrievedValue.CommitId);
                    Assert.Equal(value.FilePath, retrievedValue.FilePath);
                    Assert.Equal(value.OverallScore, retrievedValue.OverallScore);
                }
                finally
                {
                    // Cleanup
                    await _cacheService.RemoveAsync(key);
                }
            }
        }
    }
} 