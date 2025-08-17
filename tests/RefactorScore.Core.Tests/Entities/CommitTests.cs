using System;
using System.Collections.Generic;
using RefactorScore.Domain.Entities;
using Xunit;

namespace RefactorScore.Core.Tests.Entities
{
    public class CommitTests
    {
        [Fact]
        public void DeterminarTipoCommit_MensagemVazia_RetornaDesconhecido()
        {
            // Arrange
            var commit = new Commit { Mensagem = "" };
            
            // Act & Assert
            Assert.Equal("desconhecido", commit.Tipo);
        }

        [Fact]
        public void DeterminarTipoCommit_MensagemNula_RetornaDesconhecido()
        {
            // Arrange
            var commit = new Commit { Mensagem = null };
            
            // Act & Assert
            Assert.Equal("desconhecido", commit.Tipo);
        }

        [Theory]
        [InlineData("feat: adiciona nova funcionalidade", "feat")]
        [InlineData("fix: corrige bug na autenticação", "fix")]
        [InlineData("docs: atualiza documentação", "docs")]
        [InlineData("style: formata código", "style")]
        [InlineData("refactor: refatora lógica de negócio", "refactor")]
        [InlineData("test: adiciona testes", "test")]
        [InlineData("chore: atualiza dependências", "chore")]
        [InlineData("ci: configura pipeline", "ci")]
        [InlineData("build: ajusta build", "build")]
        [InlineData("perf: melhora performance", "perf")]
        [InlineData("Feat: adiciona com letra maiúscula", "feat")] // Teste com maiúscula
        [InlineData("FEAT: adiciona com todas maiúsculas", "feat")] // Teste com maiúsculas
        public void DeterminarTipoCommit_MensagemComPrefixo_RetornaTipoCorreto(string mensagem, string tipoEsperado)
        {
            // Arrange
            var commit = new Commit { Mensagem = mensagem };
            
            // Act & Assert
            Assert.Equal(tipoEsperado, commit.Tipo);
        }

        [Fact]
        public void DeterminarTipoCommit_MensagemSemPrefixoConhecido_RetornaOutro()
        {
            // Arrange
            var commit = new Commit { Mensagem = "implementa nova funcionalidade" };
            
            // Act & Assert
            Assert.Equal("outro", commit.Tipo);
        }

        [Fact]
        public void Commit_PropriedadesMudancas_InicializaListaVazia()
        {
            // Arrange & Act
            var commit = new Commit();
            
            // Assert
            Assert.NotNull(commit.Mudancas);
            Assert.Empty(commit.Mudancas);
        }
        
        [Fact]
        public void Commit_AdicionarMudanca_AdicionaNaLista()
        {
            // Arrange
            var commit = new Commit();
            var mudanca = new MudancaDeArquivoNoCommit
            {
                CaminhoArquivo = "arquivo.cs",
                TipoMudanca = TipoMudanca.Adicionado
            };
            
            // Act
            commit.Mudancas.Add(mudanca);
            
            // Assert
            Assert.Single(commit.Mudancas);
            Assert.Equal("arquivo.cs", commit.Mudancas[0].CaminhoArquivo);
            Assert.Equal(TipoMudanca.Adicionado, commit.Mudancas[0].TipoMudanca);
        }
    }
} 