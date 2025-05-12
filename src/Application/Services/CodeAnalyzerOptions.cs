namespace RefactorScore.Application.Services
{
    /// <summary>
    /// Opções de configuração para o serviço de análise de código
    /// </summary>
    public class CodeAnalyzerOptions
    {
        /// <summary>
        /// Nome do modelo de LLM a ser usado
        /// </summary>
        public string ModelName { get; set; } = "refactorscore";
        
        /// <summary>
        /// Tamanho máximo de código para análise (em caracteres)
        /// </summary>
        public int MaxCodeLength { get; set; } = 30000;
        
        /// <summary>
        /// Tamanho máximo de diff para análise (em caracteres)
        /// </summary>
        public int MaxDiffLength { get; set; } = 10000;
    }
} 