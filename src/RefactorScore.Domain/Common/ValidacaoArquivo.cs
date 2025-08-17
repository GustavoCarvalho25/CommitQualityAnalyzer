using RefactorScore.Domain.Entities;

namespace RefactorScore.Domain.Common
{
    public static class ValidacaoArquivo
    {
        public const int TAMANHO_MAXIMO_ARQUIVO = 100_000;
        
        public static readonly HashSet<string> ExtensoesProibidas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bin", ".obj", ".pdb", 
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico",
            ".zip", ".rar", ".7z", ".tar", ".gz",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".mp3", ".mp4", ".avi", ".mov", ".wav"
        };
        
        public static Result<bool> ValidarArquivo(MudancaDeArquivoNoCommit arquivo)
        {
            if (arquivo == null)
                return Result<bool>.Falha("Arquivo não fornecido");
                
            if (string.IsNullOrWhiteSpace(arquivo.CaminhoArquivo))
                return Result<bool>.Falha("Caminho do arquivo não fornecido");
                
            string extensao = Path.GetExtension(arquivo.CaminhoArquivo);
            if (ExtensoesProibidas.Contains(extensao))
                return Result<bool>.Falha($"Tipo de arquivo não suportado: {extensao}");
                
            if (!arquivo.EhCodigoFonte)
                return Result<bool>.Falha($"Arquivo não é código fonte: {arquivo.CaminhoArquivo}");
                
            if (string.IsNullOrEmpty(arquivo.ConteudoModificado))
                return Result<bool>.Falha("Conteúdo do arquivo está vazio");
                
            if (arquivo.ConteudoModificado.Length > TAMANHO_MAXIMO_ARQUIVO)
                return Result<bool>.Falha($"Arquivo muito grande: {arquivo.ConteudoModificado.Length / 1024} KB (máximo: {TAMANHO_MAXIMO_ARQUIVO / 1024} KB)");
                
            if (ContemCaracteresNaoImprimiveis(arquivo.ConteudoModificado))
                return Result<bool>.Falha("Arquivo contém caracteres binários");
                
            return Result<bool>.Ok(true);
        }
        
        private static bool ContemCaracteresNaoImprimiveis(string conteudo)
        {
            if (string.IsNullOrEmpty(conteudo)) return false;
            
            // Contar caracteres não imprimíveis, exceto tabs, LF e CR
            int contagem = conteudo.Count(c => c < 32 && c != 9 && c != 10 && c != 13);
            
            // Se mais de 5% são caracteres não imprimíveis, provavelmente é binário
            return contagem > conteudo.Length * 0.05;
        }
    }
} 