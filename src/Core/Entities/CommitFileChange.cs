namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa uma modificação em um arquivo dentro de um commit
    /// </summary>
    public class CommitFileChange
    {
        /// <summary>
        /// Caminho do arquivo modificado
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Caminho do arquivo (propriedade para compatibilidade com testes)
        /// </summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>
        /// Caminho antigo do arquivo (em caso de renomeação)
        /// </summary>
        public string OldPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Tipo de modificação (Adicionado, Modificado, Removido, Renomeado)
        /// </summary>
        public FileChangeType ChangeType { get; set; }
        
        /// <summary>
        /// Tipo de modificação (propriedade para compatibilidade com testes)
        /// </summary>
        public FileChangeType Status { get; set; }
        
        /// <summary>
        /// Número de linhas adicionadas
        /// </summary>
        public int LinesAdded { get; set; }
        
        /// <summary>
        /// Número de linhas removidas
        /// </summary>
        public int LinesRemoved { get; set; }
        
        /// <summary>
        /// Número de linhas excluídas (propriedade para compatibilidade com testes)
        /// </summary>
        public int LinesDeleted { get; set; }
        
        /// <summary>
        /// Conteúdo do arquivo antes da modificação
        /// </summary>
        public string OriginalContent { get; set; } = string.Empty;
        
        /// <summary>
        /// Conteúdo do arquivo após a modificação
        /// </summary>
        public string ModifiedContent { get; set; } = string.Empty;
        
        /// <summary>
        /// Texto de diferença (diff) entre as versões
        /// </summary>
        public string DiffText { get; set; } = string.Empty;
        
        /// <summary>
        /// Indica se o arquivo pode ser analisado (não foi deletado)
        /// </summary>
        public bool IsAnalyzable => Status != FileChangeType.Deleted;
    }
    
    /// <summary>
    /// Tipos de modificações em arquivos
    /// </summary>
    public enum FileChangeType
    {
        Unknown,
        Added,
        Modified,
        Deleted,
        Renamed
    }
} 