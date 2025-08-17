using System;
using System.Collections.Generic;
using System.Linq;
using RefactorScore.Domain.Common;
using Xunit;

namespace RefactorScore.Core.Tests.Common
{
    public class ResultTests
    {
        #region Result<T> Tests
        
        [Fact]
        public void ResultT_Ok_RetornaSucessoComDados()
        {
            // Arrange
            string dadosTeste = "Dados de teste";
            
            // Act
            var result = Result<string>.Ok(dadosTeste);
            
            // Assert
            Assert.True(result.Sucesso);
            Assert.Equal(dadosTeste, result.Dados);
            Assert.Empty(result.Erros);
        }
        
        [Fact]
        public void ResultT_Ok_ComMensagem_RetornaSucessoComDadosEMensagem()
        {
            // Arrange
            string dadosTeste = "Dados de teste";
            string mensagemTeste = "Operação realizada com sucesso";
            
            // Act
            var result = Result<string>.Ok(dadosTeste, mensagemTeste);
            
            // Assert
            Assert.True(result.Sucesso);
            Assert.Equal(dadosTeste, result.Dados);
            Assert.Equal(mensagemTeste, result.Mensagem);
            Assert.Empty(result.Erros);
        }
        
        [Fact]
        public void ResultT_Falha_ComStringErro_RetornaFalhaComMensagemErro()
        {
            // Arrange
            string erroTeste = "Erro de teste";
            
            // Act
            var result = Result<string>.Falha(erroTeste);
            
            // Assert
            Assert.False(result.Sucesso);
            Assert.Null(result.Dados);
            Assert.Single(result.Erros);
            Assert.Equal(erroTeste, result.Erros.First());
        }
        
        [Fact]
        public void ResultT_Falha_ComListaErros_RetornaFalhaComListaErros()
        {
            // Arrange
            var errosTeste = new List<string> { "Erro 1", "Erro 2", "Erro 3" };
            
            // Act
            var result = Result<string>.Falha(errosTeste);
            
            // Assert
            Assert.False(result.Sucesso);
            Assert.Null(result.Dados);
            Assert.Equal(3, result.Erros.Count);
            Assert.Equal(errosTeste, result.Erros);
        }
        
        [Fact]
        public void ResultT_Falha_ComExcecao_RetornaFalhaComMensagemExcecao()
        {
            // Arrange
            var excecaoTeste = new Exception("Mensagem de exceção");
            
            // Act
            var result = Result<string>.Falha(excecaoTeste);
            
            // Assert
            Assert.False(result.Sucesso);
            Assert.Null(result.Dados);
            Assert.Single(result.Erros);
            Assert.Equal(excecaoTeste.Message, result.Erros.First());
        }
        
        [Fact]
        public void ResultT_Falha_ComExcecaoInterna_RetornaFalhaComAmbasMensagens()
        {
            // Arrange
            var excecaoInterna = new InvalidOperationException("Exceção interna");
            var excecaoTeste = new Exception("Exceção externa", excecaoInterna);
            
            // Act
            var result = Result<string>.Falha(excecaoTeste);
            
            // Assert
            Assert.False(result.Sucesso);
            Assert.Null(result.Dados);
            Assert.Equal(2, result.Erros.Count);
            Assert.Equal(excecaoTeste.Message, result.Erros[0]);
            Assert.Equal(excecaoInterna.Message, result.Erros[1]);
        }
        
        #endregion
        
        #region Result Tests (Sem tipo genérico)
        
        [Fact]
        public void Result_Ok_RetornaSucesso()
        {
            // Act
            var result = Result.Ok();
            
            // Assert
            Assert.True(result.Sucesso);
            Assert.Empty(result.Erros);
        }
        
        [Fact]
        public void Result_Ok_ComMensagem_RetornaSucessoComMensagem()
        {
            // Arrange
            string mensagemTeste = "Operação realizada com sucesso";
            
            // Act
            var result = Result.Ok(mensagemTeste);
            
            // Assert
            Assert.True(result.Sucesso);
            Assert.Equal(mensagemTeste, result.Mensagem);
            Assert.Empty(result.Erros);
        }
        
        [Fact]
        public void Result_Falha_ComStringErro_RetornaFalhaComMensagemErro()
        {
            // Arrange
            string erroTeste = "Erro de teste";
            
            // Act
            var result = Result.Falha(erroTeste);
            
            // Assert
            Assert.False(result.Sucesso);
            Assert.Single(result.Erros);
            Assert.Equal(erroTeste, result.Erros.First());
        }
        
        [Fact]
        public void Result_Falha_ComListaErros_RetornaFalhaComListaErros()
        {
            // Arrange
            var errosTeste = new List<string> { "Erro 1", "Erro 2", "Erro 3" };
            
            // Act
            var result = Result.Falha(errosTeste);
            
            // Assert
            Assert.False(result.Sucesso);
            Assert.Equal(3, result.Erros.Count);
            Assert.Equal(errosTeste, result.Erros);
        }
        
        [Fact]
        public void Result_Falha_ComExcecao_RetornaFalhaComMensagemExcecao()
        {
            // Arrange
            var excecaoTeste = new Exception("Mensagem de exceção");
            
            // Act
            var result = Result.Falha(excecaoTeste);
            
            // Assert
            Assert.False(result.Sucesso);
            Assert.Single(result.Erros);
            Assert.Equal(excecaoTeste.Message, result.Erros.First());
        }
        
        [Fact]
        public void Result_Falha_ComExcecaoInterna_RetornaFalhaComAmbasMensagens()
        {
            // Arrange
            var excecaoInterna = new InvalidOperationException("Exceção interna");
            var excecaoTeste = new Exception("Exceção externa", excecaoInterna);
            
            // Act
            var result = Result.Falha(excecaoTeste);
            
            // Assert
            Assert.False(result.Sucesso);
            Assert.Equal(2, result.Erros.Count);
            Assert.Equal(excecaoTeste.Message, result.Erros[0]);
            Assert.Equal(excecaoInterna.Message, result.Erros[1]);
        }
        
        #endregion
    }
} 