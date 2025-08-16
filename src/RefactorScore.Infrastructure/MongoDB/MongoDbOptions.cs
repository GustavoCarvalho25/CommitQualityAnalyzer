namespace RefactorScore.Infrastructure.MongoDB
{
    public class MongoDbOptions
    {
        public string ConnectionString { get; set; } = "mongodb://localhost:27017";
        public string DatabaseName { get; set; } = "RefactorScore";
        public string AnaliseCommitCollectionName { get; set; } = "AnaliseDeCommits";
        public string AnaliseArquivoCollectionName { get; set; } = "AnaliseDeArquivos";
        public string RecomendacoesCollectionName { get; set; } = "Recomendacoes";
    }
} 