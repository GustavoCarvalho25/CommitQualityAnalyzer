using System.Text;
using RefactorScore.Core.Common;

namespace RefactorScore.Core.Tests.Common
{
    public class ProcessadorArquivoGrandeTests
    {
        [Fact]
        public void PrepararConteudoParaAnalise_ConteudoNulo_RetornaNull()
        {
            // Arrange
            string conteudo = null;
            
            // Act
            var resultado = ProcessadorArquivoGrande.PrepararConteudoParaAnalise(conteudo);
            
            // Assert
            Assert.Null(resultado);
        }
        
        [Fact]
        public void PrepararConteudoParaAnalise_ConteudoVazio_RetornaVazio()
        {
            // Arrange
            string conteudo = "";
            
            // Act
            var resultado = ProcessadorArquivoGrande.PrepararConteudoParaAnalise(conteudo);
            
            // Assert
            Assert.Equal("", resultado);
        }
        
        [Fact]
        public void PrepararConteudoParaAnalise_ConteudoPequeno_RetornaConteudoOriginal()
        {
            // Arrange
            string conteudo = "Este é um conteúdo pequeno que não precisa ser processado.";
            
            // Act
            var resultado = ProcessadorArquivoGrande.PrepararConteudoParaAnalise(conteudo);
            
            // Assert
            Assert.Equal(conteudo, resultado);
        }
        
        [Fact]
        public void PrepararConteudoParaAnalise_ConteudoGrande_RetornaConteudoProcessado()
        {
            // Arrange
            // Criar conteúdo que exceda o tamanho máximo
            var sb = new StringBuilder();
            for (int i = 0; i < ProcessadorArquivoGrande.TAMANHO_MAXIMO_ANALISE * 2; i++)
            {
                sb.Append((char)('a' + (i % 26)));
            }
            string conteudo = sb.ToString();
            
            // Act
            var resultado = ProcessadorArquivoGrande.PrepararConteudoParaAnalise(conteudo);
            
            // Assert
            Assert.NotEqual(conteudo, resultado); // Deve ser diferente do original
            Assert.True(resultado.Length < conteudo.Length); // Deve ser menor que o original
            Assert.Contains("[...Conteúdo muito grande", resultado); // Deve conter mensagem de processamento
        }
        
        [Fact]
        public void PrepararConteudoParaAnalise_LinhasModificadasInformadas_PriorizaConteudoRelevante()
        {
            // Arrange
            // Criar conteúdo grande com linhas numeradas
            var sb = new StringBuilder();
            // Criar linhas muito grandes para garantir que exceda o tamanho máximo
            for (int i = 1; i <= 1000; i++)
            {
                // Adicionar uma linha muito mais longa para garantir que exceda o limite
                sb.AppendLine($"Linha {i}: Este é o conteúdo da linha {i}. " + new string('X', 500));
            }
            string conteudo = sb.ToString();
            int linhaModificada = 500; // Linha do meio
            
            // Act
            var resultado = ProcessadorArquivoGrande.PrepararConteudoParaAnalise(conteudo, linhaModificada);
            
            // Assert
            Assert.NotEqual(conteudo, resultado); // Deve ser diferente do original
            Assert.True(resultado.Length < conteudo.Length, 
                $"O resultado deveria ser menor que o original, mas resultado={resultado.Length}, original={conteudo.Length}");
            
            // Deve conter a linha modificada
            Assert.Contains($"Linha {linhaModificada}", resultado);
            
            // O conteúdo está sendo processado
            Assert.True(resultado.Length <= ProcessadorArquivoGrande.TAMANHO_MAXIMO_ANALISE * 1.1, 
                "O resultado deve ter tamanho próximo ou menor que o limite máximo");
        }
        
        [Fact]
        public void PrepararConteudoParaAnalise_ConteudoGrande_ContemTresParte()
        {
            // Arrange
            // Criar conteúdo com início, meio e fim distintos
            var inicio = new string('a', 1000);
            var meio = new string('b', 1000);
            var fim = new string('c', 1000);
            
            // Adicionar conteúdo entre as partes para garantir que o arquivo seja grande
            var preenchimento = new string('x', ProcessadorArquivoGrande.TAMANHO_MAXIMO_ANALISE);
            
            string conteudo = inicio + preenchimento + meio + preenchimento + fim;
            
            // Act
            var resultado = ProcessadorArquivoGrande.PrepararConteudoParaAnalise(conteudo);
            
            // Assert
            Assert.Contains('a', resultado); // Deve conter parte do início
            Assert.Contains('b', resultado); // Deve conter parte do meio
            Assert.Contains('c', resultado); // Deve conter parte do fim
            Assert.Contains("[...Conteúdo omitido...]", resultado); // Deve indicar conteúdo omitido
        }
    }
} 