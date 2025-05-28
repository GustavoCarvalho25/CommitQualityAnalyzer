namespace RefactorScore.Infrastructure.MongoDB
{
    /// <summary>
    /// Opções de configuração para o MongoDB
    /// </summary>
    public class MongoDbOptions
    {
        /// <summary>
        /// String de conexão com o MongoDB
        /// </summary>
        public string ConnectionString { get; set; } = "mongodb://localhost:27017";
        
        /// <summary>
        /// Nome do banco de dados
        /// </summary>
        public string DatabaseName { get; set; } = "RefactorScore";
        
        /// <summary>
        /// Nome da coleção de análises de commits
        /// </summary>
        public string AnaliseCommitCollectionName { get; set; } = "AnaliseDeCommits";
        
        /// <summary>
        /// Nome da coleção de análises de arquivos
        /// </summary>
        public string AnaliseArquivoCollectionName { get; set; } = "AnaliseDeArquivos";
        
        /// <summary>
        /// Nome da coleção de recomendações
        /// </summary>
        public string RecomendacoesCollectionName { get; set; } = "Recomendacoes";
    }
} 