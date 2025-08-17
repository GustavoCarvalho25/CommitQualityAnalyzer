using RefactorScore.Domain.Entities;

namespace RefactorScore.Domain.Interfaces;
public interface ILLMService
{
    Task<bool> IsAvailableAsync();
    Task<CodigoLimpo> AnalisarCodigoAsync(string codigo, string linguagem, string? contexto = null);
        
    Task<List<Recomendacao>> GerarRecomendacoesAsync(CodigoLimpo analise, string codigo, string linguagem);
    Task<List<string>> ObterModelosDisponiveisAsync();
}