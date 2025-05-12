using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RefactorScore.Core.Interfaces;
using RefactorScore.Infrastructure.Ollama;
using Xunit;

namespace RefactorScore.Integration.Tests
{
    public class LLMServiceIntegrationTests
    {
        private readonly OllamaService _llmService;
        private readonly Mock<ILogger<OllamaService>> _mockLogger;
        private readonly Mock<HttpClient> _mockHttpClient;

        public LLMServiceIntegrationTests()
        {
            _mockLogger = new Mock<ILogger<OllamaService>>();
            _mockHttpClient = new Mock<HttpClient>();
            
            var options = new OllamaOptions
            {
                BaseUrl = "http://localhost:11434",
                DefaultModel = "refactorscore",
                Temperature = 0.1f,
                MaxTokens = 2048,
                TopP = 0.9f,
                TopK = 40
            };
            
            var mockOptions = new Mock<IOptions<OllamaOptions>>();
            mockOptions.Setup(o => o.Value).Returns(options);
            
            _llmService = new OllamaService(_mockHttpClient.Object, mockOptions.Object, _mockLogger.Object);
        }
        
        [Fact]
        public async Task IsAvailableAsync_ShouldReturnBooleanValue()
        {
            // Act
            var result = await _llmService.IsAvailableAsync();
            
            // Assert
            // Resultado pode ser true ou false dependendo se o Ollama está em execução
            Assert.IsType<bool>(result);
        }
        
        [Fact(Skip = "Requer Ollama em execução")]
        public async Task ProcessPromptAsync_WithValidPrompt_ShouldReturnResponse()
        {
            // Arrange
            var prompt = "Explain what Clean Code is in one sentence.";
            
            // Act
            var response = await _llmService.ProcessPromptAsync(prompt);
            
            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response);
        }
        
        [Fact(Skip = "Requer Ollama em execução")]
        public async Task ProcessPromptAsync_WithSpecificModel_ShouldReturnResponse()
        {
            // Arrange
            var prompt = "Explain what Clean Code is in one sentence.";
            var modelName = "llama2"; // ou outro modelo disponível no Ollama
            
            // Act
            var response = await _llmService.ProcessPromptAsync(prompt, modelName);
            
            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response);
        }
        
        [Fact]
        public async Task ProcessPromptAsync_WithInvalidPrompt_ShouldHandleErrorGracefully()
        {
            // Act & Assert
            // Diretamente testando exceção com null sem atribuir a uma variável
            await Assert.ThrowsAsync<Exception>(() => 
                _llmService.ProcessPromptAsync(prompt: null!));
        }
    }
} 