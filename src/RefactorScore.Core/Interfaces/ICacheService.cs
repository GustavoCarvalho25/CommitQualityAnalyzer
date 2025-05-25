using System;
using System.Threading.Tasks;

namespace RefactorScore.Core.Interfaces
{
    /// <summary>
    /// Interface para serviço de cache
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Verifica se o serviço de cache está disponível
        /// </summary>
        /// <returns>True se disponível, False caso contrário</returns>
        Task<bool> IsAvailableAsync();
        
        /// <summary>
        /// Obtém um item do cache
        /// </summary>
        /// <typeparam name="T">Tipo do item</typeparam>
        /// <param name="chave">Chave do item</param>
        /// <returns>O item ou default(T) se não encontrado</returns>
        Task<T> ObterAsync<T>(string chave);
        
        /// <summary>
        /// Armazena um item no cache
        /// </summary>
        /// <typeparam name="T">Tipo do item</typeparam>
        /// <param name="chave">Chave do item</param>
        /// <param name="valor">Valor a ser armazenado</param>
        /// <param name="tempoExpiracao">Tempo de expiração (opcional)</param>
        /// <returns>True se armazenado com sucesso, False caso contrário</returns>
        Task<bool> ArmazenarAsync<T>(string chave, T valor, TimeSpan? tempoExpiracao = null);
        
        /// <summary>
        /// Remove um item do cache
        /// </summary>
        /// <param name="chave">Chave do item</param>
        /// <returns>True se removido com sucesso, False caso contrário</returns>
        Task<bool> RemoverAsync(string chave);
        
        /// <summary>
        /// Verifica se um item existe no cache
        /// </summary>
        /// <param name="chave">Chave do item</param>
        /// <returns>True se existir, False caso contrário</returns>
        Task<bool> ExisteAsync(string chave);
        
        /// <summary>
        /// Limpa todo o cache
        /// </summary>
        /// <returns>True se limpo com sucesso, False caso contrário</returns>
        Task<bool> LimparAsync();
        
        /// <summary>
        /// Obtém ou adiciona um item no cache
        /// </summary>
        /// <typeparam name="T">Tipo do item</typeparam>
        /// <param name="chave">Chave do item</param>
        /// <param name="factory">Função para criar o item se não existir</param>
        /// <param name="tempoExpiracao">Tempo de expiração (opcional)</param>
        /// <returns>O item do cache ou o item criado pela factory</returns>
        Task<T> ObterOuAdicionarAsync<T>(string chave, Func<Task<T>> factory, TimeSpan? tempoExpiracao = null);
    }
} 