using System;

namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa uma mudança em um arquivo específico dentro de um commit
    /// </summary>
    public class MudancaDeArquivoNoCommit
    {
        /// <summary>
        /// Caminho do arquivo no repositório
        /// </summary>
        public string CaminhoArquivo { get; set; }
        
        /// <summary>
        /// Caminho do arquivo antes da modificação (em caso de renomeação)
        /// </summary>
        public string CaminhoAntigo { get; set; }
        
        /// <summary>
        /// Tipo de mudança (adicionado, modificado, removido, renomeado)
        /// </summary>
        public TipoMudanca TipoMudanca { get; set; }
        
        /// <summary>
        /// Número de linhas adicionadas
        /// </summary>
        public int LinhasAdicionadas { get; set; }
        
        /// <summary>
        /// Número de linhas removidas
        /// </summary>
        public int LinhasRemovidas { get; set; }
        
        /// <summary>
        /// Conteúdo original do arquivo (antes da mudança)
        /// </summary>
        public string ConteudoOriginal { get; set; }
        
        /// <summary>
        /// Conteúdo modificado do arquivo (após a mudança)
        /// </summary>
        public string ConteudoModificado { get; set; }
        
        /// <summary>
        /// Texto do diff (apenas as mudanças)
        /// </summary>
        public string TextoDiff { get; set; }
        
        /// <summary>
        /// Indica se o arquivo é um arquivo de código
        /// </summary>
        public bool EhCodigoFonte => DeterminarSeEhCodigoFonte();
        
        /// <summary>
        /// Determina se o arquivo é um arquivo de código fonte baseado na extensão
        /// </summary>
        private bool DeterminarSeEhCodigoFonte()
        {
            if (string.IsNullOrEmpty(CaminhoArquivo))
                return false;
                
            string extensao = System.IO.Path.GetExtension(CaminhoArquivo).ToLower();
            
            // Lista de extensões de arquivos de código comum
            return extensao switch
            {
                ".cs" => true,    // C#
                ".java" => true,  // Java
                ".js" => true,    // JavaScript
                ".ts" => true,    // TypeScript
                ".py" => true,    // Python
                ".rb" => true,    // Ruby
                ".php" => true,   // PHP
                ".go" => true,    // Go
                ".c" => true,     // C
                ".cpp" => true,   // C++
                ".h" => true,     // Header C/C++
                ".swift" => true, // Swift
                ".kt" => true,    // Kotlin
                ".rs" => true,    // Rust
                ".sh" => true,    // Shell script
                ".pl" => true,    // Perl
                ".sql" => true,   // SQL
                _ => false
            };
        }
    }
    
    /// <summary>
    /// Tipo de mudança em um arquivo
    /// </summary>
    public enum TipoMudanca
    {
        Adicionado,
        Modificado,
        Removido,
        Renomeado
    }
} 