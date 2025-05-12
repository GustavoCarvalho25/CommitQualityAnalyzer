using System;
using System.Collections.Generic;
using System.Linq;

namespace RefactorScore.Core.Specifications
{
    /// <summary>
    /// Classe que encapsula o resultado de uma operação, podendo conter
    /// um valor de retorno e/ou erros ocorridos durante a execução
    /// </summary>
    /// <typeparam name="T">Tipo do valor de retorno</typeparam>
    public class Result<T>
    {
        /// <summary>
        /// Valor de retorno
        /// </summary>
        public T? Data { get; private set; }
        
        /// <summary>
        /// Indica se a operação foi bem-sucedida
        /// </summary>
        public bool IsSuccess { get; private set; }
        
        /// <summary>
        /// Lista de erros ocorridos durante a execução
        /// </summary>
        public List<string> Errors { get; private set; } = new List<string>();

        /// <summary>
        /// Cria um resultado bem-sucedido com o valor especificado
        /// </summary>
        /// <param name="data">Valor de retorno</param>
        /// <returns>Resultado bem-sucedido</returns>
        public static Result<T> Success(T data)
        {
            return new Result<T> { IsSuccess = true, Data = data };
        }

        /// <summary>
        /// Cria um resultado de falha com a mensagem de erro especificada
        /// </summary>
        /// <param name="message">Mensagem de erro</param>
        /// <returns>Resultado de falha</returns>
        public static Result<T> Fail(string message)
        {
            return new Result<T> 
            { 
                IsSuccess = false,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Cria um resultado de falha com a lista de mensagens de erro especificada
        /// </summary>
        /// <param name="errors">Lista de mensagens de erro</param>
        /// <returns>Resultado de falha</returns>
        public static Result<T> Fail(IEnumerable<string> errors)
        {
            return new Result<T>
            {
                IsSuccess = false,
                Errors = errors.ToList()
            };
        }

        /// <summary>
        /// Cria um resultado de falha com base em uma exceção
        /// </summary>
        /// <param name="ex">Exceção ocorrida</param>
        /// <returns>Resultado de falha</returns>
        public static Result<T> Fail(Exception ex)
        {
            return new Result<T>
            {
                IsSuccess = false,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Classe que encapsula o resultado de uma operação que não retorna valor,
    /// podendo conter apenas erros ocorridos durante a execução
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Indica se a operação foi bem-sucedida
        /// </summary>
        public bool IsSuccess { get; private set; }
        
        /// <summary>
        /// Lista de erros ocorridos durante a execução
        /// </summary>
        public List<string> Errors { get; private set; } = new List<string>();

        /// <summary>
        /// Cria um resultado bem-sucedido
        /// </summary>
        /// <returns>Resultado bem-sucedido</returns>
        public static Result Success()
        {
            return new Result { IsSuccess = true };
        }

        /// <summary>
        /// Cria um resultado de falha com a mensagem de erro especificada
        /// </summary>
        /// <param name="message">Mensagem de erro</param>
        /// <returns>Resultado de falha</returns>
        public static Result Fail(string message)
        {
            return new Result
            {
                IsSuccess = false,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Cria um resultado de falha com a lista de mensagens de erro especificada
        /// </summary>
        /// <param name="errors">Lista de mensagens de erro</param>
        /// <returns>Resultado de falha</returns>
        public static Result Fail(IEnumerable<string> errors)
        {
            return new Result
            {
                IsSuccess = false,
                Errors = errors.ToList()
            };
        }

        /// <summary>
        /// Cria um resultado de falha com base em uma exceção
        /// </summary>
        /// <param name="ex">Exceção ocorrida</param>
        /// <returns>Resultado de falha</returns>
        public static Result Fail(Exception ex)
        {
            return new Result
            {
                IsSuccess = false,
                Errors = new List<string> { ex.Message }
            };
        }
    }
} 