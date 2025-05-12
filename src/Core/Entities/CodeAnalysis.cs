using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa o resultado da análise de qualidade de código
    /// </summary>
    public class CodeAnalysis
    {
        /// <summary>
        /// Identificador único da análise
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// ID do commit analisado
        /// </summary>
        public string CommitId { get; set; }
        
        /// <summary>
        /// Caminho do arquivo analisado
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// Autor do commit
        /// </summary>
        public string Author { get; set; }
        
        /// <summary>
        /// Data do commit
        /// </summary>
        public DateTime CommitDate { get; set; }
        
        /// <summary>
        /// Data da realização da análise
        /// </summary>
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Resultados da análise de qualidade do código
        /// </summary>
        public CleanCodeAnalysis CleanCodeAnalysis { get; set; }
        
        /// <summary>
        /// Nota geral da análise (0-10)
        /// </summary>
        public double OverallScore { get; set; }
        
        /// <summary>
        /// Justificativa para as notas atribuídas
        /// </summary>
        public string Justification { get; set; }
    }
    
    /// <summary>
    /// Análise detalhada de aspectos de Clean Code
    /// </summary>
    public class CleanCodeAnalysis
    {
        /// <summary>
        /// Avaliação de nomenclatura de variáveis (0-10)
        /// </summary>
        public int VariableNaming { get; set; }
        
        /// <summary>
        /// Avaliação de tamanho de funções (0-10)
        /// </summary>
        public int FunctionSize { get; set; }
        
        /// <summary>
        /// Avaliação de uso de comentários relevantes (0-10)
        /// </summary>
        public int CommentUsage { get; set; }
        
        /// <summary>
        /// Avaliação de coesão dos métodos (0-10)
        /// </summary>
        public int MethodCohesion { get; set; }
        
        /// <summary>
        /// Avaliação de evitação de código morto ou redundante (0-10)
        /// </summary>
        public int DeadCodeAvoidance { get; set; }
        
        /// <summary>
        /// Critérios adicionais de avaliação (extensível)
        /// </summary>
        public Dictionary<string, int> AdditionalCriteria { get; set; } = new Dictionary<string, int>();
    }
} 