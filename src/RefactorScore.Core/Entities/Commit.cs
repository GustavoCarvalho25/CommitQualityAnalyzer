using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Entities
{
    public class Commit
    {
        public string Id { get; set; }
        public string Autor { get; set; }
        public string Email { get; set; }
        public DateTime Data { get; set; }
        public string Mensagem { get; set; }
        public List<MudancaDeArquivoNoCommit> Mudancas { get; set; } = new List<MudancaDeArquivoNoCommit>();
        public string Tipo => DeterminarTipoCommit();
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