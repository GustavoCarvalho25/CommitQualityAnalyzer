namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa uma mudança em um arquivo em um commit
    /// </summary>
    public class MudancaDeArquivoNoCommit
    {
        /// <summary>
        /// Caminho do arquivo
        /// </summary>
        public string CaminhoArquivo { get; set; }
        
        /// <summary>
        /// Caminho antigo do arquivo (em caso de renomeação)
        /// </summary>
        public string CaminhoAntigo { get; set; }
        
        /// <summary>
        /// Tipo da mudança (adicionado, modificado, removido, renomeado)
        /// </summary>
        public TipoMudanca TipoMudanca { get; set; }
        
        /// <summary>
        /// Quantidade de linhas adicionadas
        /// </summary>
        public int LinhasAdicionadas { get; set; }
        
        /// <summary>
        /// Quantidade de linhas removidas
        /// </summary>
        public int LinhasRemovidas { get; set; }
        
        /// <summary>
        /// Conteúdo original do arquivo antes da mudança
        /// </summary>
        public string ConteudoOriginal { get; set; }
        
        /// <summary>
        /// Conteúdo modificado do arquivo após a mudança
        /// </summary>
        public string ConteudoModificado { get; set; }
        
        /// <summary>
        /// Texto do diff entre as versões
        /// </summary>
        public string TextoDiff { get; set; }
        
        /// <summary>
        /// Indica se o arquivo é código fonte
        /// </summary>
        public bool EhCodigoFonte => VerificarSeArquivoEhCodigoFonte();
        
        /// <summary>
        /// Verifica se o arquivo é código fonte baseado na extensão
        /// </summary>
        private bool VerificarSeArquivoEhCodigoFonte()
        {
            if (string.IsNullOrEmpty(CaminhoArquivo))
                return false;
                
            // Extensões de arquivos de código fonte comuns
            string[] extensoesCodigo = new[] {
                ".cs", ".java", ".js", ".ts", ".py", ".rb", ".php", ".go",
                ".c", ".cpp", ".h", ".swift", ".kt", ".rs", ".sh", ".pl", ".sql"
            };
            
            string extensao = Path.GetExtension(CaminhoArquivo).ToLower();
            return extensoesCodigo.Contains(extensao);
        }
    }
} 