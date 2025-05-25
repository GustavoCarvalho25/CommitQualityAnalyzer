using System.Text;
using RefactorScore.Core.Common;
using RefactorScore.Core.Entities;

namespace RefactorScore.Core.Tests.Common
{
    public class ValidacaoArquivoTests
    {
        [Fact]
        public void ValidarArquivo_ArquivoNulo_RetornaFalha()
        {
            // Arrange
            MudancaDeArquivoNoCommit arquivo = null;
            
            // Act
            var resultado = ValidacaoArquivo.ValidarArquivo(arquivo);
            
            // Assert
            Assert.False(resultado.Sucesso);
            Assert.Contains("Arquivo não fornecido", resultado.Erros);
        }
        
        [Fact]
        public void ValidarArquivo_CaminhoArquivoVazio_RetornaFalha()
        {
            // Arrange
            var arquivo = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = "",
                ConteudoModificado = "conteúdo válido"
            };
            
            // Act
            var resultado = ValidacaoArquivo.ValidarArquivo(arquivo);
            
            // Assert
            Assert.False(resultado.Sucesso);
            Assert.Contains("Caminho do arquivo não fornecido", resultado.Erros);
        }
        
        [Theory]
        [InlineData(".exe")]
        [InlineData(".dll")]
        [InlineData(".jpg")]
        [InlineData(".zip")]
        [InlineData(".pdf")]
        public void ValidarArquivo_ExtensaoProibida_RetornaFalha(string extensao)
        {
            // Arrange
            var arquivo = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = $"arquivo{extensao}",
                ConteudoModificado = "conteúdo válido"
            };
            
            // Act
            var resultado = ValidacaoArquivo.ValidarArquivo(arquivo);
            
            // Assert
            Assert.False(resultado.Sucesso);
            Assert.Contains($"Tipo de arquivo não suportado: {extensao}", resultado.Erros);
        }
        
        [Fact]
        public void ValidarArquivo_ArquivoNaoCodigoFonte_RetornaFalha()
        {
            // Arrange
            var arquivo = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = "arquivo.txt", // Não é reconhecido como código fonte
                ConteudoModificado = "conteúdo válido"
            };
            
            // Act
            var resultado = ValidacaoArquivo.ValidarArquivo(arquivo);
            
            // Assert
            Assert.False(resultado.Sucesso);
            Assert.Contains("Arquivo não é código fonte", resultado.Erros[0]);
        }
        
        [Fact]
        public void ValidarArquivo_ConteudoVazio_RetornaFalha()
        {
            // Arrange
            var arquivo = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = "arquivo.cs",
                ConteudoModificado = ""
            };
            
            // Act
            var resultado = ValidacaoArquivo.ValidarArquivo(arquivo);
            
            // Assert
            Assert.False(resultado.Sucesso);
            Assert.Contains("Conteúdo do arquivo está vazio", resultado.Erros);
        }
        
        [Fact]
        public void ValidarArquivo_ConteudoMuitoGrande_RetornaFalha()
        {
            // Arrange
            var conteudoGrande = new string('x', ValidacaoArquivo.TAMANHO_MAXIMO_ARQUIVO + 1);
            var arquivo = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = "arquivo.cs",
                ConteudoModificado = conteudoGrande
            };
            
            // Act
            var resultado = ValidacaoArquivo.ValidarArquivo(arquivo);
            
            // Assert
            Assert.False(resultado.Sucesso);
            Assert.Contains("Arquivo muito grande", resultado.Erros[0]);
        }
        
        [Fact]
        public void ValidarArquivo_ConteudoComCaracteresBinarios_RetornaFalha()
        {
            // Arrange
            // Cria um conteúdo com mais de 5% de caracteres não imprimíveis
            var sb = new StringBuilder();
            // Adiciona muito mais caracteres não imprimíveis para garantir que ultrapasse o limite de 5%
            for (int i = 0; i < 200; i++)
            {
                sb.Append((char)(i % 30)); // Adiciona caracteres não imprimíveis em maior quantidade
                sb.Append("ab"); // Menos caracteres normais para aumentar a proporção
            }
            
            var arquivo = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = "arquivo.cs",
                ConteudoModificado = sb.ToString()
            };
            
            // Act
            var resultado = ValidacaoArquivo.ValidarArquivo(arquivo);
            
            // Assert
            Assert.False(resultado.Sucesso);
            Assert.Contains("Arquivo contém caracteres binários", resultado.Erros);
        }
        
        [Fact]
        public void ValidarArquivo_ArquivoValido_RetornaSucesso()
        {
            // Arrange
            var arquivo = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = "arquivo.cs",
                ConteudoModificado = "public class Teste { }"
            };
            
            // Act
            var resultado = ValidacaoArquivo.ValidarArquivo(arquivo);
            
            // Assert
            Assert.True(resultado.Sucesso);
            Assert.Empty(resultado.Erros);
            Assert.True(resultado.Dados);
        }
    }
} 