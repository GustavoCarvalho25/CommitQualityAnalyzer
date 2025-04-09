using CommitQualityAnalyzer.Worker.Services.CommitAnalysis.Models;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Métodos utilitários para o ResponseAnalysisService
    /// </summary>
    public partial class ResponseAnalysisService
    {
        /// <summary>
        /// Cria um resultado de análise padrão para casos de falha
        /// </summary>
        public CommitAnalysisResult CreateFallbackAnalysisResult()
        {
            var result = new CommitAnalysisResult
            {
                AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                NotaFinal = 50,
                ComentarioGeral = "Não foi possível extrair uma análise válida da resposta.",
                PropostaRefatoracao = new RefactoringProposal()
            };
            
            // Inicializar critérios padrão
            InitializeDefaultCriteria(result);
            
            return result;
        }
        
        /// <summary>
        /// Inicializa critérios padrão em um resultado de análise
        /// </summary>
        private void InitializeDefaultCriteria(CommitAnalysisResult result)
        {
            // Garantir que todos os critérios padrão estejam presentes
            var defaultCriteria = new[] { "CleanCode", "SOLID", "DesignPatterns", "Testabilidade", "Seguranca" };
            
            foreach (var criteria in defaultCriteria)
            {
                if (!result.AnaliseGeral.ContainsKey(criteria))
                {
                    result.AnaliseGeral[criteria] = new CriteriaAnalysis
                    {
                        Nota = 50, // Valor neutro padrão
                        Comentario = "Não foi possível extrair informações para este critério"
                    };
                }
            }
        }
        
        /// <summary>
        /// Normaliza uma nota para a escala 0-100
        /// </summary>
        private double NormalizeScore(double score)
        {
            if (score > 0 && score <= 10)
            {
                return Math.Round(score * 10, 0);
            }
            else if (score > 0 && score <= 100)
            {
                return Math.Round(score, 0);
            }
            else if (score > 100)
            {
                return 100;
            }
            else
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Tenta corrigir problemas comuns em strings JSON mal formatadas
        /// </summary>
        private string TryFixJsonFormat(string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent))
                return jsonContent;
            
            string result = jsonContent;
            
            try
            {
                // Corrigir propriedades sem aspas (ex: {name: "value"} -> {"name": "value"})
                result = Regex.Replace(result, @"([{,])\s*([a-zA-Z0-9_]+)\s*:", "$1\"$2\":");
                
                // Corrigir aspas simples em propriedades (ex: {'name': "value"} -> {"name": "value"})
                result = Regex.Replace(result, @"'([^']*)'\s*:", "\"$1\":");
                
                // Corrigir aspas simples em valores (ex: {"name": 'value'} -> {"name": "value"})
                result = Regex.Replace(result, @":\s*'([^']*)'([,}])", ": \"$1\"$2");
                
                // Corrigir valores string sem aspas (ex: {"name": value} -> {"name": "value"})
                result = Regex.Replace(result, @":\s*([a-zA-Z][a-zA-Z0-9_]*)([,}])", ": \"$1\"$2");
                
                // Remover comentários (ex: {"name": "value"} // comentário -> {"name": "value"})
                result = Regex.Replace(result, @"//.*?$", "", RegexOptions.Multiline);
                result = Regex.Replace(result, @"/\*.*?\*/", "", RegexOptions.Singleline);
                
                // Corrigir vírgulas extras (ex: {"name": "value",} -> {"name": "value"})
                result = Regex.Replace(result, @",\s*}", "}");
                result = Regex.Replace(result, @",\s*]", "]");
                
                return result;
            }
            catch (Exception)
            {
                // Em caso de erro, retornar o JSON original
                return jsonContent;
            }
        }
    }
}
