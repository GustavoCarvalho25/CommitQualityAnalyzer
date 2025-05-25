using RefactorScore.Core.Entities;

namespace RefactorScore.Core.Common
{
    /// <summary>
    /// Classe para validação de arquivos antes de enviá-los para análise
    /// </summary>
    public static class ValidacaoArquivo
    {
        /// <summary>
        /// Tamanho máximo de arquivo em caracteres (aproximadamente 100KB)
        /// </summary>
        public const int TAMANHO_MAXIMO_ARQUIVO = 100_000;
        
        /// <summary>
        /// Lista de extensões de arquivo que não devem ser analisadas
        /// </summary>
        public static readonly HashSet<string> ExtensoesProibidas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bin", ".obj", ".pdb", 
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico",
            ".zip", ".rar", ".7z", ".tar", ".gz",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".mp3", ".mp4", ".avi", ".mov", ".wav"
        };
        
        /// <summary>
        /// Valida se um arquivo pode ser enviado para análise
        /// </summary>
        /// <param name="arquivo">Arquivo a ser validado</param>
        /// <returns>Resultado da validação</returns>
        public static Result<bool> ValidarArquivo(MudancaDeArquivoNoCommit arquivo)
        {
            // Verificar se o arquivo é nulo
            if (arquivo == null)
                return Result<bool>.Falha("Arquivo não fornecido");
                
            // Verificar se o caminho é válido
            if (string.IsNullOrWhiteSpace(arquivo.CaminhoArquivo))
                return Result<bool>.Falha("Caminho do arquivo não fornecido");
                
            // Verificar extensão proibida
            string extensao = Path.GetExtension(arquivo.CaminhoArquivo);
            if (ExtensoesProibidas.Contains(extensao))
                return Result<bool>.Falha($"Tipo de arquivo não suportado: {extensao}");
                
            // Verificar se é um arquivo de código
            if (!arquivo.EhCodigoFonte)
                return Result<bool>.Falha($"Arquivo não é código fonte: {arquivo.CaminhoArquivo}");
                
            // Verificar se o conteúdo é válido
            if (string.IsNullOrEmpty(arquivo.ConteudoModificado))
                return Result<bool>.Falha("Conteúdo do arquivo está vazio");
                
            // Verificar tamanho do conteúdo
            if (arquivo.ConteudoModificado.Length > TAMANHO_MAXIMO_ARQUIVO)
                return Result<bool>.Falha($"Arquivo muito grande: {arquivo.ConteudoModificado.Length / 1024} KB (máximo: {TAMANHO_MAXIMO_ARQUIVO / 1024} KB)");
                
            // Verificar caracteres não imprimíveis
            if (ContemCaracteresNaoImprimiveis(arquivo.ConteudoModificado))
                return Result<bool>.Falha("Arquivo contém caracteres binários");
                
            return Result<bool>.Ok(true);
        }
        
        /// <summary>
        /// Verifica se uma string contém muitos caracteres não imprimíveis (possível arquivo binário)
        /// </summary>
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