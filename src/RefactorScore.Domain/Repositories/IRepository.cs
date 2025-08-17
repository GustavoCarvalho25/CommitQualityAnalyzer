namespace RefactorScore.Domain.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<T> ObterPorIdAsync(string id);
        Task<IEnumerable<T>> ObterTodosAsync();
        Task<T> AdicionarAsync(T entity);
        Task<T> AtualizarAsync(T entity);
        Task<bool> RemoverAsync(string id);
    }
} 