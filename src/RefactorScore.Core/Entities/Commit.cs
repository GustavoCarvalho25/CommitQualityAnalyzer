using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa um commit em um repositório Git
    /// </summary>
    public class Commit
    {
        /// <summary>
        /// Identificador único do commit (hash)
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Autor do commit
        /// </summary>
        public string Autor { get; set; }
        
        /// <summary>
        /// Email do autor do commit
        /// </summary>
        public string Email { get; set; }
        
        /// <summary>
        /// Data e hora em que o commit foi realizado
        /// </summary>
        public DateTime Data { get; set; }
        
        /// <summary>
        /// Mensagem do commit
        /// </summary>
        public string Mensagem { get; set; }
        
        /// <summary>
        /// Lista de mudanças de arquivo associadas a este commit
        /// </summary>
        public List<MudancaDeArquivoNoCommit> Mudancas { get; set; } = new List<MudancaDeArquivoNoCommit>();
        
        /// <summary>
        /// Tipo de commit (feat, fix, docs, etc) baseado na mensagem
        /// </summary>
        public string Tipo => DeterminarTipoCommit();
        
        /// <summary>
        /// Determina o tipo de commit baseado na mensagem
        /// </summary>
        private string DeterminarTipoCommit()
        {
            if (string.IsNullOrEmpty(Mensagem))
                return "desconhecido";

            string lowerMessage = Mensagem.ToLower();
            
            if (lowerMessage.StartsWith("feat"))
                return "feat";
            if (lowerMessage.StartsWith("fix"))
                return "fix";
            if (lowerMessage.StartsWith("docs"))
                return "docs";
            if (lowerMessage.StartsWith("style"))
                return "style";
            if (lowerMessage.StartsWith("refactor"))
                return "refactor";
            if (lowerMessage.StartsWith("test"))
                return "test";
            if (lowerMessage.StartsWith("chore"))
                return "chore";
            if (lowerMessage.StartsWith("ci"))
                return "ci";
            if (lowerMessage.StartsWith("build"))
                return "build";
            if (lowerMessage.StartsWith("perf"))
                return "perf";

            return "outro";
        }
    }
} 