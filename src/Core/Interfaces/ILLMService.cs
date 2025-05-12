using System.Threading.Tasks;

namespace RefactorScore.Core.Interfaces
{
    /// <summary>
    /// Interface para serviços de modelos de linguagem (LLM)
    /// </summary>
    public interface ILLMService
    {
        /// <summary>
        /// Processa um prompt com o modelo especificado e retorna a resposta
        /// </summary>
        /// <param name="prompt">Texto do prompt a ser processado</param>
        /// <param name="model">Nome do modelo a ser usado (opcional)</param>
        /// <param name="temperature">Temperatura para geração (controla aleatoriedade)</param>
        /// <param name="maxTokens">Número máximo de tokens de resposta</param>
        /// <returns>Texto da resposta gerada pelo modelo</returns>
        Task<string> ProcessPromptAsync(string prompt, string? model = null, float temperature = 0.1f, int maxTokens = 2048);
        
        /// <summary>
        /// Verifica se o serviço LLM está disponível
        /// </summary>
        /// <returns>True se o serviço estiver disponível, False caso contrário</returns>
        Task<bool> IsAvailableAsync();
        
        /// <summary>
        /// Verifica se um modelo específico está disponível
        /// </summary>
        /// <param name="modelName">Nome do modelo a verificar</param>
        /// <returns>True se o modelo estiver disponível, False caso contrário</returns>
        Task<bool> IsModelAvailableAsync(string modelName);
        
        /// <summary>
        /// Obtém a lista de modelos disponíveis
        /// </summary>
        /// <returns>Array com os nomes dos modelos disponíveis</returns>
        Task<string[]> GetAvailableModelsAsync();
    }
} 