namespace RefactorScore.Core.Entities
{
    public class AnaliseDeArquivo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string IdCommit { get; set; }
        public string CaminhoArquivo { get; set; }
        public DateTime DataAnalise { get; set; }
        public string TipoArquivo { get; set; }
        public string Linguagem { get; set; }
        public int LinhasAdicionadas { get; set; }
        public int LinhasRemovidas { get; set; }
        public CodigoLimpo Analise { get; set; }
        public List<Recomendacao> Recomendacoes { get; set; } = new List<Recomendacao>();
        public double NotaGeral => Analise?.NotaGeral ?? 0;
    }
} 