using System.Text.Json.Serialization;

namespace RefactorScore.Application.Services.LlmResponses
{
    /// <summary>
    /// Modelo para a análise de Clean Code do LLM
    /// </summary>
    internal class LlmCleanCodeAnalysis
    {
        [JsonPropertyName("nomeclatura_variaveis")]
        public double NomeclaturaVariaveis { get; set; }
        
        [JsonPropertyName("justificativa_nomenclatura")]
        public string JustificativaNomenclatura { get; set; } = string.Empty;
        
        [JsonPropertyName("tamanho_funcoes")]
        public double TamanhoFuncoes { get; set; }
        
        [JsonPropertyName("justificativa_funcoes")]
        public string JustificativaFuncoes { get; set; } = string.Empty;
        
        [JsonPropertyName("uso_de_comentarios_relevantes")]
        public double UsoDeComentariosRelevantes { get; set; }
        
        [JsonPropertyName("justificativa_comentarios")]
        public string JustificativaComentarios { get; set; } = string.Empty;
        
        [JsonPropertyName("cohesao_dos_metodos")]
        public double CohesaoDosMetodos { get; set; }
        
        [JsonPropertyName("justificativa_cohesao")]
        public string JustificativaCohesao { get; set; } = string.Empty;
        
        [JsonPropertyName("evitacao_de_codigo_morto")]
        public double EvitacaoDeCodigoMorto { get; set; }
        
        [JsonPropertyName("justificativa_codigo_morto")]
        public string JustificativaCodigoMorto { get; set; } = string.Empty;
    }
} 