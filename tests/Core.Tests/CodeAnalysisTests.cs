using System;
using System.Collections.Generic;
using RefactorScore.Core.Entities;
using Xunit;

namespace RefactorScore.Core.Tests
{
    public class CodeAnalysisTests
    {
        [Fact]
        public void CodeAnalysis_ShouldInitializeCorrectly()
        {
            // Arrange
            var now = DateTime.UtcNow;
            
            // Act
            var analysis = new CodeAnalysis
            {
                Id = "analysis1",
                CommitId = "commit1",
                FilePath = "src/file.cs",
                Author = "Test User",
                CommitDate = now.AddHours(-1),
                AnalysisDate = now,
                CleanCodeAnalysis = new CleanCodeAnalysis
                {
                    VariableNaming = 8,
                    FunctionSize = 7,
                    CommentUsage = 6,
                    MethodCohesion = 9,
                    DeadCodeAvoidance = 10
                },
                OverallScore = 8.0,
                Justification = "Good code quality"
            };
            
            // Assert
            Assert.Equal("analysis1", analysis.Id);
            Assert.Equal("commit1", analysis.CommitId);
            Assert.Equal("src/file.cs", analysis.FilePath);
            Assert.Equal("Test User", analysis.Author);
            Assert.Equal(now.AddHours(-1), analysis.CommitDate);
            Assert.Equal(now, analysis.AnalysisDate);
            Assert.Equal(8.0, analysis.OverallScore);
            Assert.Equal("Good code quality", analysis.Justification);
            
            Assert.Equal(8, analysis.CleanCodeAnalysis.VariableNaming);
            Assert.Equal(7, analysis.CleanCodeAnalysis.FunctionSize);
            Assert.Equal(6, analysis.CleanCodeAnalysis.CommentUsage);
            Assert.Equal(9, analysis.CleanCodeAnalysis.MethodCohesion);
            Assert.Equal(10, analysis.CleanCodeAnalysis.DeadCodeAvoidance);
        }
        
        [Fact]
        public void CleanCodeAnalysis_AdditionalCriteria_ShouldWorkCorrectly()
        {
            // Arrange
            var cleanCodeAnalysis = new CleanCodeAnalysis
            {
                VariableNaming = 8,
                AdditionalCriteria = new Dictionary<string, string>
                {
                    { "TestCoverage", "9" },
                    { "DocumentationQuality", "7" }
                }
            };
            
            // Act & Assert
            Assert.Equal(8, cleanCodeAnalysis.VariableNaming);
            Assert.Equal("9", cleanCodeAnalysis.AdditionalCriteria["TestCoverage"]);
            Assert.Equal("7", cleanCodeAnalysis.AdditionalCriteria["DocumentationQuality"]);
        }
        
        [Fact]
        public void CodeAnalysis_DefaultAnalysisDate_ShouldBeUtcNow()
        {
            // Arrange & Act
            var analysis = new CodeAnalysis();
            
            // Assert
            // Allow 1 second tolerance for test execution time
            Assert.True((DateTime.UtcNow - analysis.AnalysisDate).TotalSeconds < 1);
        }
    }
} 