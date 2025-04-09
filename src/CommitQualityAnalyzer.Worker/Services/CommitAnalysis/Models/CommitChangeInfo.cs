using System;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis.Models
{
    /// <summary>
    /// Representa informações sobre uma mudança em um arquivo em um commit
    /// </summary>
    public class CommitChangeInfo
    {
        /// <summary>
        /// Caminho do arquivo
        /// </summary>
        public string FilePath { get; set; } = "";
        
        /// <summary>
        /// Status da mudança (Added, Modified, Deleted, etc.)
        /// </summary>
        public string ChangeType { get; set; } = "";
        
        /// <summary>
        /// Conteúdo original do arquivo (antes da mudança)
        /// </summary>
        public string OriginalContent { get; set; } = "";
        
        /// <summary>
        /// Conteúdo modificado do arquivo (após a mudança)
        /// </summary>
        public string ModifiedContent { get; set; } = "";
        
        /// <summary>
        /// Texto de diferença entre as versões
        /// </summary>
        public string DiffText { get; set; } = "";
        
        /// <summary>
        /// Tamanho do arquivo em bytes
        /// </summary>
        public long FileSize { get; set; }
    }
}
