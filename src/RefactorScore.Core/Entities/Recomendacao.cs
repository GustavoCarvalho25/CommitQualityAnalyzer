using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa uma recomendação de melhoria baseada na análise de código
    /// </summary>
    public class Recomendacao
    {
        /// <summary>
        /// Identificador único da recomendação
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Título da recomendação
        /// </summary>
        public string Titulo { get; set; }
        
        /// <summary>
        /// Descrição detalhada da recomendação
        /// </summary>
        public string Descricao { get; set; }
        
        /// <summary>
        /// Prioridade da recomendação (alta, média, baixa)
        /// </summary>
        public string Prioridade { get; set; }
        
        /// <summary>
        /// Tipo da recomendação (refatoração, melhoria, boas práticas, etc)
        /// </summary>
        public string Tipo { get; set; }
        
        /// <summary>
        /// Nível de dificuldade para implementar a recomendação
        /// </summary>
        public string Dificuldade { get; set; }
        
        /// <summary>
        /// Referência ao arquivo ou arquivos relacionados
        /// </summary>
        public string ReferenciaArquivo { get; set; }
        
        /// <summary>
        /// Links ou recursos para estudo relacionados à recomendação
        /// </summary>
        public List<string> RecursosEstudo { get; set; } = new List<string>();
        
        /// <summary>
        /// Construtor padrão
        /// </summary>
        public Recomendacao()
        {
            Id = Guid.NewGuid().ToString();
        }
        
        /// <summary>
        /// Construtor com parâmetros básicos
        /// </summary>
        public Recomendacao(string titulo, string descricao, string tipo, string prioridade)
        {
            Id = Guid.NewGuid().ToString();
            Titulo = titulo;
            Descricao = descricao;
            Tipo = tipo;
            Prioridade = prioridade;
        }
    }
} 