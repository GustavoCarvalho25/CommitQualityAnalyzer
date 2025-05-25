using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RefactorScore.Core.Interfaces
{
    /// <summary>
    /// Interface genérica para operações de repositório
    /// </summary>
    /// <typeparam name="T">Tipo da entidade</typeparam>
    public interface IRepository<T> where T : class
    {
        /// <summary>
        /// Obtém uma entidade pelo ID
        /// </summary>
        /// <param name="id">ID da entidade</param>
        /// <returns>A entidade ou null se não encontrada</returns>
        Task<T> ObterPorIdAsync(string id);
        
        /// <summary>
        /// Obtém todas as entidades
        /// </summary>
        /// <returns>Lista de entidades</returns>
        Task<IEnumerable<T>> ObterTodosAsync();
        
        /// <summary>
        /// Adiciona uma nova entidade
        /// </summary>
        /// <param name="entity">Entidade a ser adicionada</param>
        /// <returns>A entidade adicionada</returns>
        Task<T> AdicionarAsync(T entity);
        
        /// <summary>
        /// Atualiza uma entidade existente
        /// </summary>
        /// <param name="entity">Entidade a ser atualizada</param>
        /// <returns>A entidade atualizada</returns>
        Task<T> AtualizarAsync(T entity);
        
        /// <summary>
        /// Remove uma entidade
        /// </summary>
        /// <param name="id">ID da entidade a ser removida</param>
        /// <returns>True se removida com sucesso, False caso contrário</returns>
        Task<bool> RemoverAsync(string id);
    }
} 