using System;
using System.Threading.Tasks;

namespace RefactorScore.Core.Interfaces
{
    /// <summary>
    /// Interface para o serviço de cache (Redis)
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Verifica se o serviço de cache está disponível
        /// </summary>
        /// <returns>True se o serviço estiver disponível</returns>
        Task<bool> IsAvailableAsync();
        
        /// <summary>
        /// Obtém um item do cache
        /// </summary>
        /// <typeparam name="T">Tipo do item a ser recuperado</typeparam>
        /// <param name="key">Chave do item</param>
        /// <returns>Item recuperado ou default(T) se não encontrado</returns>
        Task<T> GetAsync<T>(string key);
        
        /// <summary>
        /// Armazena um item no cache
        /// </summary>
        /// <typeparam name="T">Tipo do item a ser armazenado</typeparam>
        /// <param name="key">Chave do item</param>
        /// <param name="value">Valor a ser armazenado</param>
        /// <param name="expiry">Tempo de expiração (opcional)</param>
        /// <returns>True se o armazenamento foi bem-sucedido</returns>
        Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        
        /// <summary>
        /// Remove um item do cache
        /// </summary>
        /// <param name="key">Chave do item a ser removido</param>
        /// <returns>True se a remoção foi bem-sucedida</returns>
        Task<bool> RemoveAsync(string key);
        
        /// <summary>
        /// Verifica se uma chave existe no cache
        /// </summary>
        /// <param name="key">Chave a ser verificada</param>
        /// <returns>True se a chave existir</returns>
        Task<bool> ExistsAsync(string key);
        
        /// <summary>
        /// Obtém múltiplas chaves que correspondem a um padrão
        /// </summary>
        /// <param name="pattern">Padrão de chave (ex: "commit:*:parte:*")</param>
        /// <returns>Array de chaves encontradas</returns>
        Task<string[]> GetKeysByPatternAsync(string pattern);
        
        /// <summary>
        /// Incrementa um valor numérico no cache
        /// </summary>
        /// <param name="key">Chave do contador</param>
        /// <param name="value">Valor a incrementar (default: 1)</param>
        /// <returns>Novo valor após incremento</returns>
        Task<long> IncrementAsync(string key, long value = 1);
    }
} 