using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa a análise temporal de commits para avaliação de evolução
    /// </summary>
    public class AnaliseTemporal
    {
        /// <summary>
        /// Identificador único da análise temporal
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Data de início do período de análise
        /// </summary>
        public DateTime DataInicio { get; set; }
        
        /// <summary>
        /// Data de fim do período de análise
        /// </summary>
        public DateTime DataFim { get; set; }
        
        /// <summary>
        /// Lista de commits analisados no período
        /// </summary>
        public List<string> CommitsAnalisados { get; set; } = new List<string>();
        
        /// <summary>
        /// Autor principal dos commits (se todos forem do mesmo autor)
        /// </summary>
        public string Autor { get; set; }
        
        /// <summary>
        /// Dicionário com métricas calculadas para o período
        /// </summary>
        public Dictionary<string, double> Metricas { get; set; } = new Dictionary<string, double>();
        
        /// <summary>
        /// Calcula métricas temporais baseadas em uma lista de commits
        /// </summary>
        /// <param name="commits">Lista de commits para análise</param>
        /// <returns>Dicionário com métricas calculadas</returns>
        public Dictionary<string, double> CalcularMetricas(List<Commit> commits)
        {
            var metricas = new Dictionary<string, double>();
            
            if (commits == null || commits.Count == 0)
                return metricas;
                
            // Exemplos de métricas que poderiam ser calculadas
            int totalCommits = commits.Count;
            
            // Contagem por tipo de commit
            Dictionary<string, int> contagemPorTipo = new Dictionary<string, int>();
            
            // Tendência de qualidade de código (assumindo que cada commit tem uma análise)
            List<double> notasGerais = new List<double>();
            
            foreach (var commit in commits)
            {
                // Contar tipos de commit
                string tipoCommit = commit.Tipo ?? "outro";
                if (!contagemPorTipo.ContainsKey(tipoCommit))
                    contagemPorTipo[tipoCommit] = 0;
                
                contagemPorTipo[tipoCommit]++;
                
                // Aqui seria adicionada a nota geral da análise do commit
                // notasGerais.Add(commit.Analise?.NotaGeral ?? 0);
            }
            
            // Salvar contagens no dicionário de métricas
            metricas["TotalCommits"] = totalCommits;
            
            foreach (var kvp in contagemPorTipo)
            {
                metricas[$"Commit{kvp.Key.ToUpper()}"] = kvp.Value;
                metricas[$"PorcentagemCommit{kvp.Key.ToUpper()}"] = (double)kvp.Value / totalCommits * 100;
            }
            
            // Mais métricas podem ser adicionadas aqui
            
            return metricas;
        }
        
        /// <summary>
        /// Construtor padrão
        /// </summary>
        public AnaliseTemporal()
        {
            Id = Guid.NewGuid().ToString();
            DataInicio = DateTime.UtcNow.AddDays(-30); // Padrão: último mês
            DataFim = DateTime.UtcNow;
        }
    }
} 