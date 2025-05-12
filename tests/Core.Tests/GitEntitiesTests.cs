using System;
using RefactorScore.Core.Entities;
using Xunit;

namespace RefactorScore.Core.Tests
{
    public class GitEntitiesTests
    {
        [Fact]
        public void CommitInfo_ShouldInitializeCorrectly()
        {
            // Arrange
            var date = DateTime.UtcNow;
            
            // Act
            var commit = new CommitInfo
            {
                Id = "abc123",
                ShortMessage = "Fix bug",
                Message = "Fix bug in the processor",
                Author = "Test User",
                AuthorEmail = "test@example.com",
                Date = date
            };
            
            // Assert
            Assert.Equal("abc123", commit.Id);
            Assert.Equal("Fix bug", commit.ShortMessage);
            Assert.Equal("Fix bug in the processor", commit.Message);
            Assert.Equal("Test User", commit.Author);
            Assert.Equal("test@example.com", commit.AuthorEmail);
            Assert.Equal(date, commit.Date);
        }
        
        [Fact]
        public void CommitFileChange_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var fileChange = new CommitFileChange
            {
                Path = "src/file.cs",
                OldPath = "src/oldfile.cs",
                Status = FileChangeType.Modified,
                LinesAdded = 10,
                LinesDeleted = 5
            };
            
            // Assert
            Assert.Equal("src/file.cs", fileChange.Path);
            Assert.Equal("src/oldfile.cs", fileChange.OldPath);
            Assert.Equal(FileChangeType.Modified, fileChange.Status);
            Assert.Equal(10, fileChange.LinesAdded);
            Assert.Equal(5, fileChange.LinesDeleted);
        }
        
        [Theory]
        [InlineData(FileChangeType.Added, true)]
        [InlineData(FileChangeType.Modified, true)]
        [InlineData(FileChangeType.Deleted, false)]
        [InlineData(FileChangeType.Renamed, true)]
        public void CommitFileChange_IsAnalyzable_ShouldReturnCorrectValue(FileChangeType status, bool expected)
        {
            // Arrange
            var fileChange = new CommitFileChange
            {
                Path = "src/file.cs",
                Status = status
            };
            
            // Act & Assert
            Assert.Equal(expected, fileChange.IsAnalyzable);
        }
    }
} 