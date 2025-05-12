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
        public string FilePath { get; set; }
        
        /// <summary>
        /// Tipo de modificação (Adicionado, Modificado, Removido, Renomeado)
        /// </summary>
        public FileChangeType ChangeType { get; set; }
        
        /// <summary>
        /// Número de linhas adicionadas
        /// </summary>
        public int LinesAdded { get; set; }
        
        /// <summary>
        /// Número de linhas removidas
        /// </summary>
        public int LinesRemoved { get; set; }
        
        /// <summary>
        /// Conteúdo do arquivo antes da modificação
        /// </summary>
        public string OriginalContent { get; set; }
        
        /// <summary>
        /// Conteúdo do arquivo após a modificação
        /// </summary>
        public string ModifiedContent { get; set; }
        
        /// <summary>
        /// Texto de diferença (diff) entre as versões
        /// </summary>
        public string DiffText { get; set; }
    }
    
    /// <summary>
    /// Tipos de modificações em arquivos
    /// </summary>
    public enum FileChangeType
    {
        Added,
        Modified,
        Deleted,
        Renamed
    }
} 