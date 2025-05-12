using System;
using System.Collections.Generic;
using System.Linq;

namespace RefactorScore.Core.Specifications
{
    /// <summary>
    /// Representa o resultado de uma operação, com suporte a notificações de erro
    /// </summary>
    /// <typeparam name="T">Tipo do dado retornado pela operação</typeparam>
    public class Result<T>
    {
        /// <summary>
        /// Dados retornados pela operação
        /// </summary>
        public T Data { get; private set; }
        
        /// <summary>
        /// Indica se a operação foi bem-sucedida
        /// </summary>
        public bool IsSuccess => !Errors.Any();
        
        /// <summary>
        /// Lista de erros ocorridos durante a operação
        /// </summary>
        public List<Error> Errors { get; } = new List<Error>();
        
        /// <summary>
        /// Adiciona um erro à lista de erros
        /// </summary>
        /// <param name="message">Mensagem de erro</param>
        /// <param name="code">Código de erro (opcional)</param>
        public void AddError(string message, string code = null)
        {
            Errors.Add(new Error(message, code));
        }
        
        /// <summary>
        /// Adiciona vários erros à lista de erros
        /// </summary>
        /// <param name="errors">Coleção de erros</param>
        public void AddErrors(IEnumerable<Error> errors)
        {
            Errors.AddRange(errors);
        }
        
        /// <summary>
        /// Cria um resultado bem-sucedido
        /// </summary>
        /// <param name="data">Dados da operação</param>
        /// <returns>Resultado bem-sucedido</returns>
        public static Result<T> Success(T data)
        {
            return new Result<T> { Data = data };
        }
        
        /// <summary>
        /// Cria um resultado com falha
        /// </summary>
        /// <param name="message">Mensagem de erro</param>
        /// <param name="code">Código de erro (opcional)</param>
        /// <returns>Resultado com falha</returns>
        public static Result<T> Fail(string message, string code = null)
        {
            var result = new Result<T>();
            result.AddError(message, code);
            return result;
        }
        
        /// <summary>
        /// Cria um resultado com falha a partir de uma exceção
        /// </summary>
        /// <param name="exception">A exceção ocorrida</param>
        /// <returns>Resultado com falha</returns>
        public static Result<T> Fail(Exception exception)
        {
            var result = new Result<T>();
            result.AddError(exception.Message, "Exception");
            return result;
        }
        
        /// <summary>
        /// Cria um resultado com falha a partir de uma lista de erros
        /// </summary>
        /// <param name="errors">Lista de erros</param>
        /// <returns>Resultado com falha</returns>
        public static Result<T> Fail(IEnumerable<Error> errors)
        {
            var result = new Result<T>();
            result.AddErrors(errors);
            return result;
        }
    }
    
    /// <summary>
    /// Versão simplificada do Result para operações que não retornam dados
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Indica se a operação foi bem-sucedida
        /// </summary>
        public bool IsSuccess => !Errors.Any();
        
        /// <summary>
        /// Lista de erros ocorridos durante a operação
        /// </summary>
        public List<Error> Errors { get; } = new List<Error>();
        
        /// <summary>
        /// Adiciona um erro à lista de erros
        /// </summary>
        /// <param name="message">Mensagem de erro</param>
        /// <param name="code">Código de erro (opcional)</param>
        public void AddError(string message, string code = null)
        {
            Errors.Add(new Error(message, code));
        }
        
        /// <summary>
        /// Adiciona vários erros à lista de erros
        /// </summary>
        /// <param name="errors">Coleção de erros</param>
        public void AddErrors(IEnumerable<Error> errors)
        {
            Errors.AddRange(errors);
        }
        
        /// <summary>
        /// Cria um resultado bem-sucedido
        /// </summary>
        /// <returns>Resultado bem-sucedido</returns>
        public static Result Success()
        {
            return new Result();
        }
        
        /// <summary>
        /// Cria um resultado com falha
        /// </summary>
        /// <param name="message">Mensagem de erro</param>
        /// <param name="code">Código de erro (opcional)</param>
        /// <returns>Resultado com falha</returns>
        public static Result Fail(string message, string code = null)
        {
            var result = new Result();
            result.AddError(message, code);
            return result;
        }
        
        /// <summary>
        /// Cria um resultado com falha a partir de uma exceção
        /// </summary>
        /// <param name="exception">A exceção ocorrida</param>
        /// <returns>Resultado com falha</returns>
        public static Result Fail(Exception exception)
        {
            var result = new Result();
            result.AddError(exception.Message, "Exception");
            return result;
        }
        
        /// <summary>
        /// Cria um resultado com falha a partir de uma lista de erros
        /// </summary>
        /// <param name="errors">Lista de erros</param>
        /// <returns>Resultado com falha</returns>
        public static Result Fail(IEnumerable<Error> errors)
        {
            var result = new Result();
            result.AddErrors(errors);
            return result;
        }
        
        /// <summary>
        /// Converte um resultado tipado para um resultado não tipado
        /// </summary>
        /// <typeparam name="T">Tipo do resultado de origem</typeparam>
        /// <param name="result">Resultado tipado</param>
        /// <returns>Resultado não tipado</returns>
        public static Result FromTyped<T>(Result<T> result)
        {
            var newResult = new Result();
            
            if (!result.IsSuccess)
            {
                newResult.AddErrors(result.Errors);
            }
            
            return newResult;
        }
    }
    
    /// <summary>
    /// Representa um erro ocorrido durante uma operação
    /// </summary>
    public class Error
    {
        /// <summary>
        /// Mensagem de erro
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// Código do erro (opcional)
        /// </summary>
        public string Code { get; }
        
        /// <summary>
        /// Cria uma nova instância de Error
        /// </summary>
        /// <param name="message">Mensagem de erro</param>
        /// <param name="code">Código de erro (opcional)</param>
        public Error(string message, string code = null)
        {
            Message = message;
            Code = code;
        }
    }
} 