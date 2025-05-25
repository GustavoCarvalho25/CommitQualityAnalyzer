using System;

namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa um trecho específico de código em um arquivo
    /// </summary>
    public class TrechoCodigo
    {
        /// <summary>
        /// Linha de início do trecho de código
        /// </summary>
        public int LinhaInicio { get; set; }
        
        /// <summary>
        /// Linha de fim do trecho de código
        /// </summary>
        public int LinhaFim { get; set; }
        
        /// <summary>
        /// Conteúdo do trecho de código
        /// </summary>
        public string Conteudo { get; set; }
        
        /// <summary>
        /// Nome do arquivo ao qual o trecho pertence
        /// </summary>
        public string NomeArquivo { get; set; }
        
        /// <summary>
        /// Caminho completo do arquivo
        /// </summary>
        public string CaminhoArquivo { get; set; }
        
        /// <summary>
        /// Contexto do trecho (por exemplo, nome da classe ou método)
        /// </summary>
        public string Contexto { get; set; }
        
        /// <summary>
        /// Quantidade de linhas no trecho
        /// </summary>
        public int QuantidadeLinhas => LinhaFim - LinhaInicio + 1;
    }
} 