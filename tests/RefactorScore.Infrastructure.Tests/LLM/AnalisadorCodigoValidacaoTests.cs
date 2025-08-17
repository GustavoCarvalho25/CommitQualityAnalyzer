using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using RefactorScore.Domain.Common;
using RefactorScore.Domain.Entities;
using RefactorScore.Domain.Interfaces;
using RefactorScore.Infrastructure.LLM;
using Xunit;

namespace RefactorScore.Infrastructure.Tests.LLM
{
    public class AnalisadorCodigoValidacaoTests
    {
        private readonly Mock<ILLMService> _mockLlmService;
        private readonly Mock<IGitRepository> _mockGitRepository;
        private readonly Mock<ILogger<AnalisadorCodigo>> _mockLogger;
        private readonly Mock<IAnaliseRepository> _mockAnaliseRepository;
        private readonly AnalisadorCodigo _analisador;
        
        public AnalisadorCodigoValidacaoTests()
        {
            _mockLlmService = new Mock<ILLMService>();
            _mockGitRepository = new Mock<IGitRepository>();
            _mockLogger = new Mock<ILogger<AnalisadorCodigo>>();
            _mockAnaliseRepository = new Mock<IAnaliseRepository>();
            
            _analisador = new AnalisadorCodigo(
                _mockLlmService.Object,
                _mockGitRepository.Object,
                _mockLogger.Object,
                _mockAnaliseRepository.Object);
                
            // Configurar o mock do repositório para retornar null para análises existentes
            _mockAnaliseRepository.Setup(r => r.ObterAnaliseRecentePorCommitAsync(It.IsAny<string>()))
                .ReturnsAsync((AnaliseDeCommit)null);
            
            _mockAnaliseRepository.Setup(r => r.ObterAnalisesArquivoPorCommitAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<AnaliseDeArquivo>());
                
            // Configurar o mock para salvar análises
            _mockAnaliseRepository.Setup(r => r.AdicionarAsync(It.IsAny<AnaliseDeCommit>()))
                .ReturnsAsync((AnaliseDeCommit a) => a);
                
            _mockAnaliseRepository.Setup(r => r.SalvarAnaliseArquivoAsync(It.IsAny<AnaliseDeArquivo>()))
                .ReturnsAsync(true);
        }
        
        [Fact]
        public async Task AnalisarArquivoNoCommitAsync_ArquivoMuitoGrande_ProcessaCorretamente()
        {
            // Arrange
            string commitId = "123456";
            string caminhoArquivo = "Program.cs";
            
            // Criar conteúdo que exceda o tamanho máximo
            var conteudoGrande = new string('x', ProcessadorArquivoGrande.TAMANHO_MAXIMO_ANALISE * 2);
            
            var arquivo = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = caminhoArquivo,
                TipoMudanca = TipoMudanca.Modificado,
                ConteudoModificado = conteudoGrande,
                LinhasAdicionadas = 100,
                LinhasRemovidas = 50
            };
            
            var commit = new Commit
            {
                Id = commitId,
                Mensagem = "feat: implementa nova funcionalidade",
                Autor = "Teste",
                Data = DateTime.UtcNow,
                Mudancas = new List<MudancaDeArquivoNoCommit> { arquivo }
            };
            
            _mockGitRepository.Setup(g => g.ObterCommitPorIdAsync(commitId))
                .ReturnsAsync(commit);
                
            _mockGitRepository.Setup(g => g.ObterMudancasNoCommitAsync(commitId))
                .ReturnsAsync(commit.Mudancas);
                
            var codigoLimpo = new CodigoLimpo
            {
                NomenclaturaVariaveis = 8,
                TamanhoFuncoes = 7,
                UsoComentariosRelevantes = 6,
                CoesaoMetodos = 9,
                EvitacaoCodigoMorto = 8
            };
            
            // Verificar se o LLM foi chamado com o conteúdo processado, não com o original
            _mockLlmService.Setup(s => s.AnalisarCodigoAsync(
                    It.Is<string>(content => content.Length < conteudoGrande.Length), // Conteúdo processado deve ser menor
                    It.IsAny<string>(), 
                    It.IsAny<string>()))
                .ReturnsAsync(codigoLimpo)
                .Verifiable();
                
            // Act
            var resultado = await _analisador.AnalisarArquivoNoCommitAsync(commitId, caminhoArquivo);
            
            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(caminhoArquivo, resultado.CaminhoArquivo);
            Assert.NotNull(resultado.Analise);
            
            // Verificar que o serviço LLM foi chamado com o conteúdo processado
            _mockLlmService.Verify();
        }
        
        [Fact]
        public async Task AnalisarArquivoNoCommitAsync_ArquivoInvalido_RetornaNull()
        {
            // Arrange
            string commitId = "123456";
            string caminhoArquivo = "imagem.jpg"; // Arquivo não é código fonte
            
            var arquivo = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = caminhoArquivo,
                TipoMudanca = TipoMudanca.Adicionado,
                ConteudoModificado = "conteúdo binário",
                LinhasAdicionadas = 0,
                LinhasRemovidas = 0
            };
            
            var commit = new Commit
            {
                Id = commitId,
                Mensagem = "feat: adiciona imagem",
                Autor = "Teste",
                Data = DateTime.UtcNow,
                Mudancas = new List<MudancaDeArquivoNoCommit> { arquivo }
            };
            
            _mockGitRepository.Setup(g => g.ObterCommitPorIdAsync(commitId))
                .ReturnsAsync(commit);
                
            _mockGitRepository.Setup(g => g.ObterMudancasNoCommitAsync(commitId))
                .ReturnsAsync(commit.Mudancas);
                
            // Act
            var resultado = await _analisador.AnalisarArquivoNoCommitAsync(commitId, caminhoArquivo);
            
            // Assert
            Assert.Null(resultado);
            
            // Verificar que o serviço LLM nunca foi chamado
            _mockLlmService.Verify(
                s => s.AnalisarCodigoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
                Times.Never);
        }
        
        [Fact]
        public async Task EstimarLinhaModificada_DiffValido_RetornaLinhaCorreta()
        {
            // Arrange
            string commitId = "123456";
            
            // Diff contém linha de contexto "@@ -10,5 +15,8 @@"
            string textoDiff = "diff --git a/Program.cs b/Program.cs\r\n" +
                              "index 1234567..abcdef 100644\r\n" +
                              "--- a/Program.cs\r\n" +
                              "+++ b/Program.cs\r\n" +
                              "@@ -10,5 +15,8 @@ namespace Test\r\n" +
                              " public class Program\r\n" +
                              " {\r\n" +
                              "-    public static void Main()\r\n" +
                              "+    public static void Main(string[] args)\r\n" +
                              "     {\r\n" +
                              "+        Console.WriteLine(\"Hello World\");\r\n" +
                              "     }\r\n" +
                              " }";
            
            var arquivo = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = "Program.cs",
                TipoMudanca = TipoMudanca.Modificado,
                ConteudoModificado = "public class Program\r\n{\r\n    public static void Main(string[] args)\r\n    {\r\n        Console.WriteLine(\"Hello World\");\r\n    }\r\n}",
                TextoDiff = textoDiff,
                LinhasAdicionadas = 2,
                LinhasRemovidas = 1
            };
            
            var commit = new Commit
            {
                Id = commitId,
                Mensagem = "feat: atualiza programa",
                Autor = "Teste",
                Data = DateTime.UtcNow,
                Mudancas = new List<MudancaDeArquivoNoCommit> { arquivo }
            };
            
            _mockGitRepository.Setup(g => g.ObterCommitPorIdAsync(commitId))
                .ReturnsAsync(commit);
                
            _mockGitRepository.Setup(g => g.ObterMudancasNoCommitAsync(commitId))
                .ReturnsAsync(commit.Mudancas);
                
            var codigoLimpo = new CodigoLimpo
            {
                NomenclaturaVariaveis = 8,
                TamanhoFuncoes = 7,
                UsoComentariosRelevantes = 6,
                CoesaoMetodos = 9,
                EvitacaoCodigoMorto = 8
            };
            
            // Verificar se o LLM foi chamado com o centro de modificação na linha 15
            _mockLlmService.Setup(s => s.AnalisarCodigoAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(), 
                    It.IsAny<string>()))
                .ReturnsAsync(codigoLimpo)
                .Callback<string, string, string>((conteudo, linguagem, contexto) => {
                    // O conteúdo processado deve incluir linhas ao redor da linha 15
                    Assert.Contains("Main(string[] args)", conteudo);
                });
                
            // Act
            var resultado = await _analisador.AnalisarArquivoNoCommitAsync(commitId, "Program.cs");
            
            // Assert
            Assert.NotNull(resultado);
            
            // Verificar que o serviço LLM foi chamado
            _mockLlmService.Verify(
                s => s.AnalisarCodigoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), 
                Times.Once);
        }
    }
} 