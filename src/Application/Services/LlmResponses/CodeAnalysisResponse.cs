using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RefactorScore.Application.Services.LlmResponses
{
    /// <summary>
    /// Classe para desserializar as respostas do LLM relacionadas a análise de código
    /// </summary>
    public class CodeAnalysisResponse
    {
        /// <summary>
        /// Pontuação relacionada a nomenclatura de variáveis (0-10)
        /// </summary>
        [JsonPropertyName("variableNaming")]
        public int VariableNaming { get; set; }
        
        /// <summary>
        /// Justificativa para a pontuação de nomenclatura
        /// </summary>
        [JsonPropertyName("namingJustification")]
        public string NamingJustification { get; set; } = string.Empty;
        
        /// <summary>
        /// Pontuação relacionada ao tamanho das funções (0-10)
        /// </summary>
        [JsonPropertyName("functionSize")]
        public int FunctionSize { get; set; }
        
        /// <summary>
        /// Justificativa para a pontuação de tamanho de funções
        /// </summary>
        [JsonPropertyName("functionSizeJustification")]
        public string FunctionSizeJustification { get; set; } = string.Empty;
        
        /// <summary>
        /// Pontuação relacionada ao uso de comentários (0-10)
        /// </summary>
        [JsonPropertyName("commentUsage")]
        public int CommentUsage { get; set; }
        
        /// <summary>
        /// Justificativa para a pontuação de uso de comentários
        /// </summary>
        [JsonPropertyName("commentJustification")]
        public string CommentJustification { get; set; } = string.Empty;
        
        /// <summary>
        /// Pontuação relacionada à coesão dos métodos (0-10)
        /// </summary>
        [JsonPropertyName("methodCohesion")]
        public int MethodCohesion { get; set; }
        
        /// <summary>
        /// Justificativa para a pontuação de coesão dos métodos
        /// </summary>
        [JsonPropertyName("cohesionJustification")]
        public string CohesionJustification { get; set; } = string.Empty;
        
        /// <summary>
        /// Pontuação relacionada à evitação de código morto ou redundante (0-10)
        /// </summary>
        [JsonPropertyName("deadCodeAvoidance")]
        public int DeadCodeAvoidance { get; set; }
        
        /// <summary>
        /// Justificativa para a pontuação de evitação de código morto ou redundante
        /// </summary>
        [JsonPropertyName("deadCodeJustification")]
        public string DeadCodeJustification { get; set; } = string.Empty;
        
        /// <summary>
        /// Pontuação geral (0-10)
        /// </summary>
        [JsonPropertyName("overallScore")]
        public double OverallScore { get; set; }
        
        /// <summary>
        /// Justificativa geral para as pontuações
        /// </summary>
        [JsonPropertyName("justification")]
        public string Justification { get; set; } = string.Empty;
        
        /// <summary>
        /// Sugestões de melhorias
        /// </summary>
        [JsonPropertyName("suggestions")]
        public List<string> Suggestions { get; set; } = new List<string>();
        
        /// <summary>
        /// Qualquer pontuação ou métrica adicional
        /// </summary>
        [JsonPropertyName("additionalMetrics")]
        public Dictionary<string, int> AdditionalMetrics { get; set; } = new Dictionary<string, int>();
    }
} 