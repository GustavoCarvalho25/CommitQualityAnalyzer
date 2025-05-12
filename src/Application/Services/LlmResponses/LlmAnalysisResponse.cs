using System.Text.Json.Serialization;

namespace RefactorScore.Application.Services.LlmResponses
{
    /// <summary>
    /// Modelo para a resposta do LLM
    /// </summary>
    internal class LlmAnalysisResponse
    {
        [JsonPropertyName("commit_id")]
        public string CommitId { get; set; } = string.Empty;
        
        [JsonPropertyName("autor")]
        public string Autor { get; set; } = string.Empty;
        
        [JsonPropertyName("analise_clean_code")]
        public LlmCleanCodeAnalysis AnaliseCleanCode { get; set; } = new LlmCleanCodeAnalysis();
        
        [JsonPropertyName("nota_geral")]
        public double NotaGeral { get; set; }
        
        [JsonPropertyName("justificativa")]
        public string Justificativa { get; set; } = string.Empty;
    }
} 