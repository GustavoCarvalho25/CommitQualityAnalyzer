using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa uma análise completa de um commit
    /// </summary>
    public class AnaliseDeCommit
    {
        /// <summary>
        /// Identificador único da análise
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// ID do commit analisado
        /// </summary>
        public string IdCommit { get; set; }
        
        /// <summary>
        /// Autor do commit
        /// </summary>
        public string Autor { get; set; }
        
        /// <summary>
        /// Email do autor
        /// </summary>
        public string Email { get; set; }
        
        /// <summary>
        /// Data e hora do commit
        /// </summary>
        public DateTime DataDoCommit { get; set; }
        
        /// <summary>
        /// Data e hora da análise
        /// </summary>
        public DateTime DataDaAnalise { get; set; }
        
        /// <summary>
        /// Análise de código limpo para este commit
        /// </summary>
        public CodigoLimpo AnaliseCodigoLimpo { get; set; }
        
        /// <summary>
        /// Nota geral para o commit (média das análises de arquivos)
        /// </summary>
        public double NotaGeral { get; set; }
        
        /// <summary>
        /// Justificativa geral para a nota
        /// </summary>
        public string Justificativa { get; set; }
        
        /// <summary>
        /// Tipo do commit (feat, fix, docs, etc.)
        /// </summary>
        public string TipoCommit { get; set; }
        
        /// <summary>
        /// Lista de recomendações geradas para este commit
        /// </summary>
        public List<Recomendacao> Recomendacoes { get; set; } = new List<Recomendacao>();
        
        /// <summary>
        /// Referência ao commit analisado
        /// </summary>
        public Commit Commit { get; set; }
        
        /// <summary>
        /// Análises individuais por arquivo
        /// </summary>
        public List<AnaliseDeArquivo> AnalisesDeArquivos { get; set; } = new List<AnaliseDeArquivo>();
        
        /// <summary>
        /// Construtor padrão
        /// </summary>
        public AnaliseDeCommit()
        {
            Id = Guid.NewGuid().ToString();
            DataDaAnalise = DateTime.UtcNow;
            AnaliseCodigoLimpo = new CodigoLimpo();
        }
    }
} 