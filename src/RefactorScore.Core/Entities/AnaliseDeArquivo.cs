using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa a análise de um arquivo específico em um commit
    /// </summary>
    public class AnaliseDeArquivo
    {
        /// <summary>
        /// Identificador único da análise
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// ID do commit analisado
        /// </summary>
        public string IdCommit { get; set; }
        
        /// <summary>
        /// Caminho do arquivo analisado
        /// </summary>
        public string CaminhoArquivo { get; set; }
        
        /// <summary>
        /// Data da análise
        /// </summary>
        public DateTime DataAnalise { get; set; }
        
        /// <summary>
        /// Tipo/extensão do arquivo
        /// </summary>
        public string TipoArquivo { get; set; }
        
        /// <summary>
        /// Linguagem de programação do arquivo
        /// </summary>
        public string Linguagem { get; set; }
        
        /// <summary>
        /// Quantidade de linhas adicionadas
        /// </summary>
        public int LinhasAdicionadas { get; set; }
        
        /// <summary>
        /// Quantidade de linhas removidas
        /// </summary>
        public int LinhasRemovidas { get; set; }
        
        /// <summary>
        /// Análise de código limpo do arquivo
        /// </summary>
        public CodigoLimpo Analise { get; set; }
        
        /// <summary>
        /// Recomendações para o arquivo
        /// </summary>
        public List<Recomendacao> Recomendacoes { get; set; } = new List<Recomendacao>();
        
        /// <summary>
        /// Nota geral da análise
        /// </summary>
        public double NotaGeral => Analise?.NotaGeral ?? 0;
    }
} 