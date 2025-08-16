namespace RefactorScore.Core.Entities
{
    public class MudancaDeArquivoNoCommit
    {
        public string CaminhoArquivo { get; set; }
        public string CaminhoAntigo { get; set; }
        public TipoMudanca TipoMudanca { get; set; }
        public int LinhasAdicionadas { get; set; }
        public int LinhasRemovidas { get; set; }
        public string ConteudoOriginal { get; set; }
        public string ConteudoModificado { get; set; }
        public string TextoDiff { get; set; }
        public bool EhCodigoFonte => VerificarSeArquivoEhCodigoFonte();
        
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