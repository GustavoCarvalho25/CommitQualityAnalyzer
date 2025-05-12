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
        
        [JsonPropertyName("tamanho_funcoes")]
        public double TamanhoFuncoes { get; set; }
        
        [JsonPropertyName("uso_de_comentarios_relevantes")]
        public double UsoDeComentariosRelevantes { get; set; }
        
        [JsonPropertyName("cohesao_dos_metodos")]
        public double CohesaoDosMetodos { get; set; }
        
        [JsonPropertyName("evitacao_de_codigo_morto")]
        public double EvitacaoDeCodigoMorto { get; set; }
    }
} 