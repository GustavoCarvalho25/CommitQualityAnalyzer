using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Entities
{
    public class Recomendacao
    {
        public string Id { get; set; }
        public string Titulo { get; set; }
        public string Descricao { get; set; }
        public string Prioridade { get; set; }
        public string Tipo { get; set; }
        public string Dificuldade { get; set; }
        public string ReferenciaArquivo { get; set; }
        public string IdCommit { get; set; }
        public DateTime DataCriacao { get; set; }
        public List<string> RecursosEstudo { get; set; } = new List<string>();
        
        public Recomendacao()
        {
            Id = Guid.NewGuid().ToString();
            DataCriacao = DateTime.UtcNow;
        }
        
        public Recomendacao(string titulo, string descricao, string tipo, string prioridade)
        {
            Id = Guid.NewGuid().ToString();
            Titulo = titulo;
            Descricao = descricao;
            Tipo = tipo;
            Prioridade = prioridade;
            DataCriacao = DateTime.UtcNow;
        }
    }
} 