using System;
using System.Collections.Generic;
using RefactorScore.Domain.Entities;
using Xunit;

namespace RefactorScore.Core.Tests.Entities
{
    public class AnaliseTemporalTests
    {
        [Fact]
        public void AnaliseTemporal_Construtor_InicializaPropriedadesCorretamente()
        {
            // Arrange & Act
            var analise = new AnaliseTemporal();
            
            // Assert
            Assert.NotNull(analise.Id);
            Assert.NotEqual(Guid.Empty.ToString(), analise.Id);
            Assert.NotNull(analise.CommitsAnalisados);
            Assert.Empty(analise.CommitsAnalisados);
            Assert.NotNull(analise.Metricas);
            Assert.Empty(analise.Metricas);
            Assert.Equal(DateTime.UtcNow.AddDays(-30).Date, analise.DataInicio.Date);
            Assert.Equal(DateTime.UtcNow.Date, analise.DataFim.Date);
        }
        
        [Fact]
        public void CalcularMetricas_ListaCommitsVazia_RetornaDicionarioVazio()
        {
            // Arrange
            var analise = new AnaliseTemporal();
            var commits = new List<Commit>();
            
            // Act
            var metricas = analise.CalcularMetricas(commits);
            
            // Assert
            Assert.NotNull(metricas);
            Assert.Empty(metricas);
        }
        
        [Fact]
        public void CalcularMetricas_ListaCommitsNula_RetornaDicionarioVazio()
        {
            // Arrange
            var analise = new AnaliseTemporal();
            
            // Act
            var metricas = analise.CalcularMetricas(null);
            
            // Assert
            Assert.NotNull(metricas);
            Assert.Empty(metricas);
        }
        
        [Fact]
        public void CalcularMetricas_CommitsComDiferentesTipos_CalculaMetricasCorretamente()
        {
            // Arrange
            var analise = new AnaliseTemporal();
            var commits = new List<Commit>
            {
                new Commit { Mensagem = "feat: nova funcionalidade" },
                new Commit { Mensagem = "feat: outra funcionalidade" },
                new Commit { Mensagem = "fix: correção de bug" },
                new Commit { Mensagem = "docs: atualização de documentação" },
                new Commit { Mensagem = "refactor: melhoria de código" },
                new Commit { Mensagem = "commit sem tipo definido" }
            };
            
            // Act
            var metricas = analise.CalcularMetricas(commits);
            
            // Assert
            Assert.Equal(6, metricas["TotalCommits"]);
            Assert.Equal(2, metricas["CommitFEAT"]);
            Assert.Equal(1, metricas["CommitFIX"]);
            Assert.Equal(1, metricas["CommitDOCS"]);
            Assert.Equal(1, metricas["CommitREFACTOR"]);
            Assert.Equal(1, metricas["CommitOUTRO"]);
            
            // Verificar porcentagens
            Assert.Equal(33.33333333333333, metricas["PorcentagemCommitFEAT"], 0.00001);
            Assert.Equal(16.666666666666664, metricas["PorcentagemCommitFIX"], 0.00001);
            Assert.Equal(16.666666666666664, metricas["PorcentagemCommitDOCS"], 0.00001);
            Assert.Equal(16.666666666666664, metricas["PorcentagemCommitREFACTOR"], 0.00001);
            Assert.Equal(16.666666666666664, metricas["PorcentagemCommitOUTRO"], 0.00001);
        }
        
        [Fact]
        public void CalcularMetricas_TodosCommitsMesmoTipo_CalculaMetricasCorretamente()
        {
            // Arrange
            var analise = new AnaliseTemporal();
            var commits = new List<Commit>
            {
                new Commit { Mensagem = "feat: funcionalidade 1" },
                new Commit { Mensagem = "feat: funcionalidade 2" },
                new Commit { Mensagem = "feat: funcionalidade 3" }
            };
            
            // Act
            var metricas = analise.CalcularMetricas(commits);
            
            // Assert
            Assert.Equal(3, metricas["TotalCommits"]);
            Assert.Equal(3, metricas["CommitFEAT"]);
            Assert.Equal(100.0, metricas["PorcentagemCommitFEAT"]);
        }
        
        [Fact]
        public void CalcularMetricas_CommitsSemTipoDefinido_ClassificaComoOutro()
        {
            // Arrange
            var analise = new AnaliseTemporal();
            var commits = new List<Commit>
            {
                new Commit { Mensagem = "Implementa função X" },
                new Commit { Mensagem = "Adiciona suporte para Y" }
            };
            
            // Act
            var metricas = analise.CalcularMetricas(commits);
            
            // Assert
            Assert.Equal(2, metricas["TotalCommits"]);
            Assert.Equal(2, metricas["CommitOUTRO"]);
            Assert.Equal(100.0, metricas["PorcentagemCommitOUTRO"]);
        }
    }
} 