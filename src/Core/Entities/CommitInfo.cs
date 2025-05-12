using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa informações básicas de um commit no repositório Git
    /// </summary>
    public class CommitInfo
    {
        /// <summary>
        /// Identificador único do commit (SHA)
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Autor do commit
        /// </summary>
        public string Author { get; set; }
        
        /// <summary>
        /// Email do autor do commit
        /// </summary>
        public string AuthorEmail { get; set; }
        
        /// <summary>
        /// Data e hora do commit
        /// </summary>
        public DateTime CommitDate { get; set; }
        
        /// <summary>
        /// Mensagem associada ao commit
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// Lista de arquivos modificados neste commit
        /// </summary>
        public List<CommitFileChange> Changes { get; set; } = new List<CommitFileChange>();
    }
} 