using System.Collections.Generic;

namespace RefactorScore.Application.Services
{
    /// <summary>
    /// Opções de configuração para o serviço de análise de código
    /// </summary>
    public class CodeAnalyzerOptions
    {
        /// <summary>
        /// Tamanho máximo em caracteres para o código a ser enviado para análise
        /// </summary>
        public int MaxCodeLength { get; set; } = 6000;
        
        /// <summary>
        /// Tamanho máximo (em caracteres) dos diffs para análise
        /// </summary>
        public int MaxDiffLength { get; set; } = 3000;
        
        /// <summary>
        /// Nome do modelo LLM a ser usado para análise (se diferente do padrão)
        /// </summary>
        public string ModelName { get; set; } = "refactorscore";
        
        /// <summary>
        /// Porcentagem de sobreposição entre chunks de código (para garantir que funções não sejam divididas)
        /// </summary>
        public int ChunkOverlapPercentage { get; set; } = 10;
        
        /// <summary>
        /// Tempo em horas para manter análises em cache
        /// </summary>
        public int AnalysisCacheTimeInHours { get; set; } = 24;
        
        /// <summary>
        /// Se deve pular arquivos binários
        /// </summary>
        public bool SkipBinaryFiles { get; set; } = true;
        
        /// <summary>
        /// Se deve pular arquivos gerados automaticamente
        /// </summary>
        public bool SkipGeneratedFiles { get; set; } = true;
        
        /// <summary>
        /// Lista de extensões de arquivo a serem analisadas
        /// </summary>
        public List<string> FileExtensionsToAnalyze { get; set; } = new List<string>
        {
            ".cs", ".java", ".js", ".ts", ".py", ".rb", ".php", ".go", ".swift",
            ".kt", ".cpp", ".c", ".h", ".hpp", ".jsx", ".tsx"
        };
        
        /// <summary>
        /// Templates de prompt para análise específica por linguagem
        /// </summary>
        public Dictionary<string, string> LanguagePrompts { get; set; } = new Dictionary<string, string>();
    }
} 