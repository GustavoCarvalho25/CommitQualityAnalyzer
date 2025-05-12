using System;
using System.Linq;
using RefactorScore.Core.Specifications;
using Xunit;

namespace RefactorScore.Core.Tests
{
    public class ResultTests
    {
        [Fact]
        public void Success_ShouldCreateSuccessResult()
        {
            // Arrange & Act
            var result = Result<string>.Success("test");
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("test", result.Data);
            Assert.Empty(result.Errors);
        }
        
        [Fact]
        public void Fail_WithMessage_ShouldCreateFailResult()
        {
            // Arrange & Act
            var result = Result<string>.Fail("error message");
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Null(result.Data);
            Assert.Single(result.Errors);
            Assert.Equal("error message", result.Errors.First().Message);
        }
        
        [Fact]
        public void Fail_WithException_ShouldCreateFailResult()
        {
            // Arrange
            var exception = new InvalidOperationException("test exception");
            
            // Act
            var result = Result<string>.Fail(exception);
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Null(result.Data);
            Assert.Single(result.Errors);
            Assert.Equal("test exception", result.Errors.First().Message);
        }
        
        [Fact]
        public void Fail_WithErrorList_ShouldCreateFailResult()
        {
            // Arrange
            var errors = new[] 
            {
                new Error("error1"),
                new Error("error2")
            };
            
            // Act
            var result = Result<string>.Fail(errors);
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Null(result.Data);
            Assert.Equal(2, result.Errors.Count());
            Assert.Contains(result.Errors, e => e.Message == "error1");
            Assert.Contains(result.Errors, e => e.Message == "error2");
        }
    }
} 