using System;
using System.Collections.Generic;

namespace RefactorScore.Domain.Common
{
    public class Result<T>
    {
        public bool Sucesso { get; private set; }
        public T Dados { get; private set; }
        public List<string> Erros { get; private set; } = new List<string>();
        public string Mensagem { get; private set; }
        
        public static Result<T> Ok(T dados, string mensagem = null)
        {
            return new Result<T>
            {
                Sucesso = true,
                Dados = dados,
                Mensagem = mensagem
            };
        }
        
        public static Result<T> Falha(IEnumerable<string> erros)
        {
            var result = new Result<T> { Sucesso = false };
            result.Erros.AddRange(erros);
            return result;
        }
        
        public static Result<T> Falha(string erro)
        {
            var result = new Result<T> { Sucesso = false };
            result.Erros.Add(erro);
            return result;
        }
        
        public static Result<T> Falha(Exception ex)
        {
            var result = new Result<T> { Sucesso = false };
            result.Erros.Add(ex.Message);
            
            if (ex.InnerException != null)
                result.Erros.Add(ex.InnerException.Message);
                
            return result;
        }
    }
    
    public class Result
    {
        public bool Sucesso { get; private set; }
        public List<string> Erros { get; private set; } = new List<string>();
        public string Mensagem { get; private set; }
        
        public static Result Ok(string mensagem = null)
        {
            return new Result
            {
                Sucesso = true,
                Mensagem = mensagem
            };
        }
        
        public static Result Falha(IEnumerable<string> erros)
        {
            var result = new Result { Sucesso = false };
            result.Erros.AddRange(erros);
            return result;
        }
        
        public static Result Falha(string erro)
        {
            var result = new Result { Sucesso = false };
            result.Erros.Add(erro);
            return result;
        }
        
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