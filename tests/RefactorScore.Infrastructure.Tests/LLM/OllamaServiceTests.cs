using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using RefactorScore.Core.Entities;
using RefactorScore.Infrastructure.LLM;
using Xunit;

namespace RefactorScore.Infrastructure.Tests.LLM
{
    public class OllamaServiceTests
    {
        private readonly Mock<IOptions<OllamaOptions>> _mockOptions;
        private readonly Mock<ILogger<OllamaService>> _mockLogger;
        private readonly PromptTemplates _promptTemplates;
        
        public OllamaServiceTests()
        {
            _mockOptions = new Mock<IOptions<OllamaOptions>>();
            _mockOptions.Setup(o => o.Value).Returns(new OllamaOptions
            {
                BaseUrl = "http://localhost:11434",
                ModeloPadrao = "deepseek-coder:6.7b-instruct-q4_0",
                ModeloAnalise = "deepseek-coder:6.7b-instruct-q4_0",
                ModeloRecomendacoes = "deepseek-coder:6.7b-instruct-q4_0"
            });
            
            _mockLogger = new Mock<ILogger<OllamaService>>();
            
            _promptTemplates = new PromptTemplates();
        }
        
        [Fact]
        public async Task IsAvailableAsync_ModelosDisponiveis_RetornaTrue()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(
                HttpStatusCode.OK, 
                JsonSerializer.Serialize(new 
                {
                    models = new[] 
                    { 
                        new { name = "deepseek-coder:6.7b-instruct-q4_0", modified_at = "2024-05-01", size = 123456 } 
                    }
                })
            );
            
            var service = new OllamaService(httpClient, _mockOptions.Object, _mockLogger.Object, _promptTemplates);
            
            // Act
            var result = await service.IsAvailableAsync();
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public async Task IsAvailableAsync_ErroNoServidor_RetornaFalse()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "");
            
            var service = new OllamaService(httpClient, _mockOptions.Object, _mockLogger.Object, _promptTemplates);
            
            // Act
            var result = await service.IsAvailableAsync();
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public async Task ProcessarPromptAsync_RespostaValida_RetornaTexto()
        {
            // Arrange
            var respostaEsperada = "Esta é a resposta do modelo";
            var httpClient = CreateMockHttpClient(
                HttpStatusCode.OK, 
                JsonSerializer.Serialize(new 
                {
                    model = "deepseek-coder:6.7b-instruct-q4_0",
                    response = respostaEsperada,
                    total_duration = 1234,
                    prompt_eval_count = 10,
                    eval_count = 20
                })
            );
            
            // Configurar o mock para primeiro retornar a lista de modelos (para a verificação do modelo)
            // e depois retornar a resposta do prompt
            var modelosResponse = JsonSerializer.Serialize(new 
            {
                models = new[] 
                { 
                    new { name = "deepseek-coder:6.7b-instruct-q4_0", modified_at = "2024-05-01", size = 123456 } 
                }
            });
            
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(modelosResponse, Encoding.UTF8, "application/json")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(
                        JsonSerializer.Serialize(new 
                        {
                            model = "deepseek-coder:6.7b-instruct-q4_0",
                            response = respostaEsperada,
                            total_duration = 1234,
                            prompt_eval_count = 10,
                            eval_count = 20
                        }), 
                        Encoding.UTF8, 
                        "application/json")
                });
                
            var httpClientSequence = new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri("http://localhost:11434")
            };
            
            var service = new OllamaService(
                httpClientSequence, 
                _mockOptions.Object, 
                _mockLogger.Object, 
                _promptTemplates);
            
            // Act
            var result = await service.ProcessarPromptAsync("Olá, como vai?");
            
            // Assert
            Assert.Equal(respostaEsperada, result);
        }
        
        [Fact]
        public async Task AnalisarCodigoAsync_RespostaValida_RetornaCodigoLimpo()
        {
            // Arrange
            string respostaJson = @"{
                ""nomenclaturaVariaveis"": 7,
                ""tamanhoFuncoes"": 8,
                ""usoComentariosRelevantes"": 6,
                ""coesaoMetodos"": 9,
                ""evitacaoCodigoMorto"": 5,
                ""justificativas"": {
                    ""nomenclaturaVariaveis"": ""Nomes descritivos e claros"",
                    ""tamanhoFuncoes"": ""Funções curtas e com responsabilidade única"",
                    ""usoComentariosRelevantes"": ""Poucos comentários explicando o propósito do código"",
                    ""coesaoMetodos"": ""Métodos bem coesos, cada um realiza uma única tarefa"",
                    ""evitacaoCodigoMorto"": ""Há código comentado que poderia ser removido""
                }
            }";
            
            // Simular a resposta do LLM com o JSON
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(
                        JsonSerializer.Serialize(new 
                        {
                            models = new[] 
                            { 
                                new { name = "deepseek-coder:6.7b-instruct-q4_0", modified_at = "2024-05-01", size = 123456 } 
                            }
                        }), 
                        Encoding.UTF8, 
                        "application/json")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(
                        JsonSerializer.Serialize(new 
                        {
                            model = "deepseek-coder:6.7b-instruct-q4_0",
                            response = respostaJson,
                            total_duration = 1234,
                            prompt_eval_count = 10,
                            eval_count = 20
                        }), 
                        Encoding.UTF8, 
                        "application/json")
                });
                
            var httpClient = new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri("http://localhost:11434")
            };
            
            var service = new OllamaService(
                httpClient, 
                _mockOptions.Object, 
                _mockLogger.Object, 
                _promptTemplates);
            
            // Act
            var result = await service.AnalisarCodigoAsync(
                "public void Test() { int x = 1; }",
                "C#",
                "Função de teste");
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(7, result.NomenclaturaVariaveis);
            Assert.Equal(8, result.TamanhoFuncoes);
            Assert.Equal(6, result.UsoComentariosRelevantes);
            Assert.Equal(9, result.CoesaoMetodos);
            Assert.Equal(5, result.EvitacaoCodigoMorto);
            Assert.Equal("Nomes descritivos e claros", result.Justificativas["nomenclaturaVariaveis"]);
        }
        
        [Fact]
        public async Task GerarRecomendacoesAsync_RespostaValida_RetornaListaRecomendacoes()
        {
            // Arrange
            string respostaJson = @"[
                {
                    ""titulo"": ""Melhore a nomenclatura de variáveis"",
                    ""descricao"": ""Variáveis como 'x' não comunicam seu propósito. Nomes descritivos melhoram a legibilidade."",
                    ""exemplo"": ""Em vez de 'int x = 1;', use 'int contador = 1;'"",
                    ""prioridade"": ""Alta"",
                    ""tipo"": ""Nomenclatura"",
                    ""dificuldade"": ""Fácil"",
                    ""referenciaArquivo"": ""linha 1"",
                    ""recursosEstudo"": [""https://cleancoders.com/resources/naming-variables""]
                }
            ]";
            
            // Simular a resposta do LLM com o JSON
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(
                        JsonSerializer.Serialize(new 
                        {
                            models = new[] 
                            { 
                                new { name = "deepseek-coder:6.7b-instruct-q4_0", modified_at = "2024-05-01", size = 123456 } 
                            }
                        }), 
                        Encoding.UTF8, 
                        "application/json")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(
                        JsonSerializer.Serialize(new 
                        {
                            model = "deepseek-coder:6.7b-instruct-q4_0",
                            response = respostaJson,
                            total_duration = 1234,
                            prompt_eval_count = 10,
                            eval_count = 20
                        }), 
                        Encoding.UTF8, 
                        "application/json")
                });
                
            var httpClient = new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri("http://localhost:11434")
            };
            
            var service = new OllamaService(
                httpClient, 
                _mockOptions.Object, 
                _mockLogger.Object, 
                _promptTemplates);
            
            // Criar uma análise de código para passar para o método
            var analise = new CodigoLimpo
            {
                NomenclaturaVariaveis = 5,
                TamanhoFuncoes = 8,
                UsoComentariosRelevantes = 7,
                CoesaoMetodos = 9,
                EvitacaoCodigoMorto = 6,
                Justificativas = new Dictionary<string, string>
                {
                    { "nomenclaturaVariaveis", "Algumas variáveis têm nomes pouco descritivos" }
                }
            };
            
            // Act
            var result = await service.GerarRecomendacoesAsync(
                analise,
                "public void Test() { int x = 1; }",
                "C#");
            
            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Melhore a nomenclatura de variáveis", result[0].Titulo);
            Assert.Equal("Alta", result[0].Prioridade);
            Assert.Equal("Nomenclatura", result[0].Tipo);
            Assert.Equal("Fácil", result[0].Dificuldade);
            Assert.Contains("https://cleancoders.com/resources/naming-variables", result[0].RecursosEstudo);
        }
        
        private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
                
            return new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri("http://localhost:11434")
            };
        }
    }
} 