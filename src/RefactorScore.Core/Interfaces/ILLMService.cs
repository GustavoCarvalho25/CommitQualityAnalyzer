using System.Collections.Generic;
using System.Threading.Tasks;
using RefactorScore.Core.Entities;

namespace RefactorScore.Core.Interfaces
{
    /// <summary>
    /// Interface para serviços de Large Language Model (LLM)
    /// </summary>
    public interface ILLMService
    {
        /// <summary>
        /// Verifica se o serviço LLM está disponível
        /// </summary>
        /// <returns>True se disponível, False caso contrário</returns>
        Task<bool> IsAvailableAsync();
        
        /// <summary>
        /// Processa um prompt de texto através do modelo
        /// </summary>
        /// <param name="prompt">Texto do prompt</param>
        /// <param name="modelo">Nome do modelo a ser usado (opcional)</param>
        /// <param name="temperatura">Temperatura para geração (0.0 a 1.0)</param>
        /// <param name="maxTokens">Número máximo de tokens na resposta</param>
        /// <returns>Resposta gerada pelo modelo</returns>
        Task<string> ProcessarPromptAsync(
            string prompt, 
            string? modelo = null, 
            float temperatura = 0.1f, 
            int maxTokens = 2048);
        
        /// <summary>
        /// Avalia código usando o LLM e retorna uma análise de código limpo
        /// </summary>
        /// <param name="codigo">Código a ser analisado</param>
        /// <param name="linguagem">Linguagem de programação do código</param>
        /// <param name="contexto">Contexto adicional sobre o código (opcional)</param>
        /// <returns>Análise de código limpo</returns>
        Task<CodigoLimpo> AnalisarCodigoAsync(
            string codigo, 
            string linguagem, 
            string? contexto = null);
        
        /// <summary>
        /// Gera recomendações educativas baseadas na análise de código
        /// </summary>
        /// <param name="analise">Análise de código limpo</param>
        /// <param name="codigo">Código analisado</param>
        /// <param name="linguagem">Linguagem de programação</param>
        /// <returns>Lista de recomendações</returns>
        Task<List<Recomendacao>> GerarRecomendacoesAsync(
            CodigoLimpo analise, 
            string codigo, 
            string linguagem);
        
        /// <summary>
        /// Obtém a lista de modelos disponíveis
        /// </summary>
        /// <returns>Lista de nomes de modelos</returns>
        Task<List<string>> ObterModelosDisponiveisAsync();
        
        /// <summary>
        /// Verifica se um modelo específico está disponível
        /// </summary>
        /// <param name="nomeModelo">Nome do modelo</param>
        /// <returns>True se disponível, False caso contrário</returns>
        Task<bool> VerificarModeloDisponivelAsync(string nomeModelo);
    }
} 