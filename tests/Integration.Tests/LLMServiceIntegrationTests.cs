using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RefactorScore.Infrastructure.Ollama;
using Xunit;

namespace RefactorScore.Integration.Tests
{
    public class LLMServiceIntegrationTests
    {
        private readonly Mock<ILogger<OllamaService>> _mockLogger;
        private readonly OllamaOptions _options;
        private readonly OllamaService _ollamaService;

        public LLMServiceIntegrationTests()
        {
            _mockLogger = new Mock<ILogger<OllamaService>>();
            
            _options = new OllamaOptions
            {
                BaseUrl = "http://localhost:11434/",
                DefaultModel = "refactorscore",
                Temperature = 0.1f,
                MaxTokens = 2048,
                TopP = 0.9f,
                TopK = 40
            };

            var mockOptions = new Mock<IOptions<OllamaOptions>>();
            mockOptions.Setup(m => m.Value).Returns(_options);

            _ollamaService = new OllamaService(_mockLogger.Object, mockOptions.Object);
        }

        [Fact(Skip = "Requires Ollama service running")]
        public async Task IsAvailableAsync_ShouldReturnTrue_WhenOllamaServiceIsRunning()
        {
            // Act
            var result = await _ollamaService.IsAvailableAsync();

            // Assert
            Assert.True(result);
        }

        [Fact(Skip = "Requires Ollama service running")]
        public async Task IsModelAvailableAsync_ShouldReturnTrue_WhenModelIsAvailable()
        {
            // Act
            var result = await _ollamaService.IsModelAvailableAsync(_options.DefaultModel);

            // Assert
            Assert.True(result);
        }

        [Fact(Skip = "Requires Ollama service running")]
        public async Task GenerateResponseAsync_ShouldReturnValidResponse()
        {
            // Arrange
            string prompt = "Analise este código: function sum(a, b) { return a + b; }";

            // Act
            var response = await _ollamaService.GenerateResponseAsync(prompt);

            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response);
        }

        [Fact(Skip = "Requires Ollama service running")]
        public async Task GenerateResponseAsync_ShouldHandleJsonPrompts()
        {
            // Arrange
            string prompt = @"
Analise este código e responda em JSON:
```
function sum(a, b) { 
  return a + b; 
}
```";

            // Act
            var response = await _ollamaService.GenerateResponseAsync(prompt);

            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response);
            Assert.Contains("{", response); // Response should contain JSON
            Assert.Contains("}", response);
        }
    }
} 