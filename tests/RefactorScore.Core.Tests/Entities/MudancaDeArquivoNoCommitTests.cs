using System;
using RefactorScore.Domain.Entities;
using Xunit;

namespace RefactorScore.Core.Tests.Entities
{
    public class MudancaDeArquivoNoCommitTests
    {
        [Theory]
        [InlineData("arquivo.cs", true)]       // C#
        [InlineData("classe.java", true)]      // Java
        [InlineData("script.js", true)]        // JavaScript
        [InlineData("componente.ts", true)]    // TypeScript
        [InlineData("programa.py", true)]      // Python
        [InlineData("aplicacao.rb", true)]     // Ruby
        [InlineData("pagina.php", true)]       // PHP
        [InlineData("programa.go", true)]      // Go
        [InlineData("programa.c", true)]       // C
        [InlineData("classe.cpp", true)]       // C++
        [InlineData("cabecalho.h", true)]      // Header C/C++
        [InlineData("app.swift", true)]        // Swift
        [InlineData("classe.kt", true)]        // Kotlin
        [InlineData("programa.rs", true)]      // Rust
        [InlineData("script.sh", true)]        // Shell script
        [InlineData("script.pl", true)]        // Perl
        [InlineData("consulta.sql", true)]     // SQL
        public void EhCodigoFonte_ArquivosDeCodigo_RetornaTrue(string caminhoArquivo, bool resultadoEsperado)
        {
            // Arrange
            var mudanca = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = caminhoArquivo
            };
            
            // Act & Assert
            Assert.Equal(resultadoEsperado, mudanca.EhCodigoFonte);
        }
        
        [Theory]
        [InlineData("imagem.jpg", false)]     // Imagem
        [InlineData("documento.pdf", false)]  // PDF
        [InlineData("arquivo.zip", false)]    // Arquivo compactado
        [InlineData("dados.json", false)]     // JSON
        [InlineData("configuracao.xml", false)] // XML
        [InlineData("arquivo.txt", false)]    // Texto
        [InlineData("readme.md", false)]      // Markdown
        [InlineData("estilo.css", false)]     // CSS
        [InlineData("pagina.html", false)]    // HTML
        [InlineData("sem-extensao", false)]   // Sem extensão
        [InlineData("", false)]               // String vazia
        [InlineData(null, false)]             // Caminho nulo
        public void EhCodigoFonte_ArquivosNaoDeCodigo_RetornaFalse(string caminhoArquivo, bool resultadoEsperado)
        {
            // Arrange
            var mudanca = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = caminhoArquivo
            };
            
            // Act & Assert
            Assert.Equal(resultadoEsperado, mudanca.EhCodigoFonte);
        }
        
        [Fact]
        public void MudancaDeArquivoNoCommit_PropriedadesBasicas_SaoDefiniveisELegiveisCorretamente()
        {
            // Arrange
            var mudanca = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = "src/Program.cs",
                CaminhoAntigo = "src/App.cs",
                TipoMudanca = TipoMudanca.Renomeado,
                LinhasAdicionadas = 10,
                LinhasRemovidas = 5,
                ConteudoOriginal = "// Código original",
                ConteudoModificado = "// Código modificado",
                TextoDiff = "@@ -1,5 +1,10 @@"
            };
            
            // Act & Assert
            Assert.Equal("src/Program.cs", mudanca.CaminhoArquivo);
            Assert.Equal("src/App.cs", mudanca.CaminhoAntigo);
            Assert.Equal(TipoMudanca.Renomeado, mudanca.TipoMudanca);
            Assert.Equal(10, mudanca.LinhasAdicionadas);
            Assert.Equal(5, mudanca.LinhasRemovidas);
            Assert.Equal("// Código original", mudanca.ConteudoOriginal);
            Assert.Equal("// Código modificado", mudanca.ConteudoModificado);
            Assert.Equal("@@ -1,5 +1,10 @@", mudanca.TextoDiff);
        }
        
        [Fact]
        public void EhCodigoFonte_CaseSensitivity_TrataExtensaoIndependentementeDeCaixa()
        {
            // Arrange
            var mudancaLowerCase = new MudancaDeArquivoNoCommit { CaminhoArquivo = "arquivo.cs" };
            var mudancaUpperCase = new MudancaDeArquivoNoCommit { CaminhoArquivo = "ARQUIVO.CS" };
            var mudancaMixedCase = new MudancaDeArquivoNoCommit { CaminhoArquivo = "Arquivo.Cs" };
            
            // Act & Assert
            Assert.True(mudancaLowerCase.EhCodigoFonte);
            Assert.True(mudancaUpperCase.EhCodigoFonte);
            Assert.True(mudancaMixedCase.EhCodigoFonte);
        }
    }
} 