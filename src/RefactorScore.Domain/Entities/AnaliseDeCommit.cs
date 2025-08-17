namespace RefactorScore.Domain.Entities
{ 
    public class AnaliseDeCommit
    { 
        public string Id { get; set; }
        public string IdCommit { get; set; }
        public string Autor { get; set; }
        public string Email { get; set; }
        public DateTime DataDoCommit { get; set; }
        public DateTime DataDaAnalise { get; set; }
        public CodigoLimpo AnaliseCodigoLimpo { get; set; }
        public double NotaGeral { get; set; }
        public string Justificativa { get; set; }
        public string TipoCommit { get; set; }
        public List<Recomendacao> Recomendacoes { get; set; } = new List<Recomendacao>();
        public Commit Commit { get; set; }
        public List<AnaliseDeArquivo> AnalisesDeArquivos { get; set; } = new List<AnaliseDeArquivo>();
        public AnaliseDeCommit()
        {
            Id = Guid.NewGuid().ToString();
            DataDaAnalise = DateTime.UtcNow;
            AnaliseCodigoLimpo = new CodigoLimpo();
        }
    }
} 