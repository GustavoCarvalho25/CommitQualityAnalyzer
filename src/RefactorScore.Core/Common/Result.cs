using System;
using System.Collections.Generic;

namespace RefactorScore.Core.Common
{
    /// <summary>
    /// Classe para representar o resultado de uma operação
    /// </summary>
    /// <typeparam name="T">Tipo do dado retornado</typeparam>
    public class Result<T>
    {
        /// <summary>
        /// Indica se a operação foi bem-sucedida
        /// </summary>
        public bool Sucesso { get; private set; }
        
        /// <summary>
        /// Dados retornados pela operação
        /// </summary>
        public T Dados { get; private set; }
        
        /// <summary>
        /// Lista de mensagens de erro
        /// </summary>
        public List<string> Erros { get; private set; } = new List<string>();
        
        /// <summary>
        /// Mensagem informativa sobre o resultado
        /// </summary>
        public string Mensagem { get; private set; }
        
        /// <summary>
        /// Cria um resultado de sucesso
        /// </summary>
        /// <param name="dados">Dados retornados</param>
        /// <param name="mensagem">Mensagem informativa (opcional)</param>
        /// <returns>Resultado de sucesso</returns>
        public static Result<T> Ok(T dados, string mensagem = null)
        {
            return new Result<T>
            {
                Sucesso = true,
                Dados = dados,
                Mensagem = mensagem
            };
        }
        
        /// <summary>
        /// Cria um resultado de falha
        /// </summary>
        /// <param name="erros">Lista de mensagens de erro</param>
        /// <returns>Resultado de falha</returns>
        public static Result<T> Falha(IEnumerable<string> erros)
        {
            var result = new Result<T> { Sucesso = false };
            result.Erros.AddRange(erros);
            return result;
        }
        
        /// <summary>
        /// Cria um resultado de falha
        /// </summary>
        /// <param name="erro">Mensagem de erro</param>
        /// <returns>Resultado de falha</returns>
        public static Result<T> Falha(string erro)
        {
            var result = new Result<T> { Sucesso = false };
            result.Erros.Add(erro);
            return result;
        }
        
        /// <summary>
        /// Cria um resultado de falha a partir de uma exceção
        /// </summary>
        /// <param name="ex">Exceção</param>
        /// <returns>Resultado de falha</returns>
        public static Result<T> Falha(Exception ex)
        {
            var result = new Result<T> { Sucesso = false };
            result.Erros.Add(ex.Message);
            
            if (ex.InnerException != null)
                result.Erros.Add(ex.InnerException.Message);
                
            return result;
        }
    }
    
    /// <summary>
    /// Classe para representar o resultado de uma operação sem retorno de dados
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Indica se a operação foi bem-sucedida
        /// </summary>
        public bool Sucesso { get; private set; }
        
        /// <summary>
        /// Lista de mensagens de erro
        /// </summary>
        public List<string> Erros { get; private set; } = new List<string>();
        
        /// <summary>
        /// Mensagem informativa sobre o resultado
        /// </summary>
        public string Mensagem { get; private set; }
        
        /// <summary>
        /// Cria um resultado de sucesso
        /// </summary>
        /// <param name="mensagem">Mensagem informativa (opcional)</param>
        /// <returns>Resultado de sucesso</returns>
        public static Result Ok(string mensagem = null)
        {
            return new Result
            {
                Sucesso = true,
                Mensagem = mensagem
            };
        }
        
        /// <summary>
        /// Cria um resultado de falha
        /// </summary>
        /// <param name="erros">Lista de mensagens de erro</param>
        /// <returns>Resultado de falha</returns>
        public static Result Falha(IEnumerable<string> erros)
        {
            var result = new Result { Sucesso = false };
            result.Erros.AddRange(erros);
            return result;
        }
        
        /// <summary>
        /// Cria um resultado de falha
        /// </summary>
        /// <param name="erro">Mensagem de erro</param>
        /// <returns>Resultado de falha</returns>
        public static Result Falha(string erro)
        {
            var result = new Result { Sucesso = false };
            result.Erros.Add(erro);
            return result;
        }
        
        /// <summary>
        /// Cria um resultado de falha a partir de uma exceção
        /// </summary>
        /// <param name="ex">Exceção</param>
        /// <returns>Resultado de falha</returns>
        public static Result Falha(Exception ex)
        {
            var result = new Result { Sucesso = false };
            result.Erros.Add(ex.Message);
            
            if (ex.InnerException != null)
                result.Erros.Add(ex.InnerException.Message);
                
            return result;
        }
    }
} 