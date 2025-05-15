namespace RefactorScore.WorkerService
{
    /// <summary>
    /// Opções de configuração para o Worker Service
    /// </summary>
    public class WorkerOptions
    {
        /// <summary>
        /// Intervalo em minutos para verificação de novos commits
        /// </summary>
        public int ScanIntervalMinutes { get; set; } = 60;
        
        /// <summary>
        /// Número máximo de commits para processar em cada ciclo
        /// </summary>
        public int MaxProcessingCommits { get; set; } = 10;
        
        /// <summary>
        /// Número máximo de arquivos a processar por commit
        /// </summary>
        public int MaxFilesPerCommit { get; set; } = 10;
        
        /// <summary>
        /// Tamanho máximo de arquivo em KB
        /// </summary>
        public int MaxFileSizeKB { get; set; } = 200;
    }
} 