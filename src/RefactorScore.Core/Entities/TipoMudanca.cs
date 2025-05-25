namespace RefactorScore.Core.Entities
{
    /// <summary>
    /// Representa os tipos de mudanças possíveis em um arquivo
    /// </summary>
    public enum TipoMudanca
    {
        /// <summary>
        /// Arquivo adicionado
        /// </summary>
        Adicionado,
        
        /// <summary>
        /// Arquivo modificado
        /// </summary>
        Modificado,
        
        /// <summary>
        /// Arquivo removido
        /// </summary>
        Removido,
        
        /// <summary>
        /// Arquivo renomeado
        /// </summary>
        Renomeado,
        
        /// <summary>
        /// Arquivo copiado
        /// </summary>
        Copiado,
        
        /// <summary>
        /// Tipo não identificado
        /// </summary>
        Desconhecido
    }
} 