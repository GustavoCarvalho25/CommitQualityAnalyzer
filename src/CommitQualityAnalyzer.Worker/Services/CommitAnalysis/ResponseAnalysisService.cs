using CommitQualityAnalyzer.Worker.Services.CommitAnalysis.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Serviço responsável por analisar e extrair informações estruturadas das respostas do modelo
    /// </summary>
    public partial class ResponseAnalysisService
    {
        private readonly ILogger<ResponseAnalysisService> _logger;

        public ResponseAnalysisService(ILogger<ResponseAnalysisService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Converte uma resposta em texto para um objeto CommitAnalysisResult estruturado
        /// </summary>
        public CommitAnalysisResult ConvertTextResponseToJson(string textResponse)
        {
            _logger.LogInformation("Tentando converter resposta em texto para JSON");
            
            if (string.IsNullOrEmpty(textResponse))
            {
                _logger.LogWarning("Resposta em texto vazia");
                return CreateFallbackAnalysisResult();
            }
            
            try
            {
                // Extrair JSON diretamente da resposta usando um padrão mais flexível
                var jsonMatch = Regex.Match(textResponse, @"```(?:json)?\s*({[\s\S]*?})\s*```", RegexOptions.Singleline);
                if (jsonMatch.Success && jsonMatch.Groups.Count > 1)
                {
                    string jsonContent = jsonMatch.Groups[1].Value.Trim();
                    _logger.LogDebug("JSON extraído: {JsonLength} caracteres", jsonContent.Length);
                    
                    try
                    {
                        // Tentar corrigir o formato do JSON se necessário
                        jsonContent = TryFixJsonFormat(jsonContent);
                        
                        // Tentar deserializar o JSON para validar
                        JsonDocument jsonDoc = JsonDocument.Parse(jsonContent);
                        JsonElement root = jsonDoc.RootElement;
                        
                        // Criar um objeto CommitAnalysisResult a partir do JSON
                        var analysisResult = new CommitAnalysisResult
                        {
                            AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                            NotaFinal = 0,
                            ComentarioGeral = "",
                            PropostaRefatoracao = new RefactoringProposal()
                        };
                        
                        // Inicializar todos os critérios com valores padrão
                        InitializeDefaultCriteria(analysisResult);
                        
                        // Extrair análises de critérios
                        if (root.TryGetProperty("AnaliseGeral", out var analiseGeral) || 
                            root.TryGetProperty("analise", out analiseGeral))
                        {
                            foreach (var property in analiseGeral.EnumerateObject())
                            {
                                string criterioKey = property.Name;
                                // Normalizar nomes de critérios usando um dicionário para mapeamento
                                string propertyNameLower = criterioKey.ToLower();
                                
                                // Mapeamento de variações de nomes para os critérios padronizados
                                if (propertyNameLower == "cleancode" || propertyNameLower == "clean_code" || 
                                    propertyNameLower == "clean code" || propertyNameLower == "codigo limpo" || 
                                    propertyNameLower == "código limpo")
                                {
                                    criterioKey = "CleanCode";
                                }
                                else if (propertyNameLower == "solid" || propertyNameLower == "solid_principles" || 
                                         propertyNameLower == "solidprinciples" || propertyNameLower == "principios solid" || 
                                         propertyNameLower == "princípios solid")
                                {
                                    criterioKey = "SOLID";
                                }
                                else if (propertyNameLower == "designpatterns" || propertyNameLower == "design_patterns" || 
                                         propertyNameLower == "design patterns" || propertyNameLower == "padroes de projeto" || 
                                         propertyNameLower == "padrões de projeto")
                                {
                                    criterioKey = "DesignPatterns";
                                }
                                else if (propertyNameLower == "testabilidade" || propertyNameLower == "testability" || 
                                         propertyNameLower == "testes" || propertyNameLower == "tests" || 
                                         propertyNameLower == "testable")
                                {
                                    criterioKey = "Testabilidade";
                                }
                                else if (propertyNameLower == "seguranca" || propertyNameLower == "segurança" || 
                                         propertyNameLower == "security" || propertyNameLower == "seguridad")
                                {
                                    criterioKey = "Seguranca";
                                }
                                
                                // Extrair nota e comentário
                                if (property.Value.TryGetProperty("Nota", out var notaElement) || 
                                    property.Value.TryGetProperty("nota", out notaElement) || 
                                    property.Value.TryGetProperty("Score", out notaElement) || 
                                    property.Value.TryGetProperty("score", out notaElement))
                                {
                                    int nota = 0;
                                    
                                    if (notaElement.TryGetInt32(out int notaInt))
                                    {
                                        nota = (int)NormalizeScore(notaInt);
                                    }
                                    else if (notaElement.TryGetDouble(out double notaDouble))
                                    {
                                        nota = (int)NormalizeScore(notaDouble);
                                    }
                                    else if (notaElement.ValueKind == JsonValueKind.String)
                                    {
                                        string notaStr = notaElement.GetString();
                                        if (double.TryParse(notaStr, out double notaFromStr))
                                        {
                                            nota = (int)NormalizeScore(notaFromStr);
                                        }
                                    }
                                    
                                    string comentario = "";
                                    if (property.Value.TryGetProperty("Comentario", out var comentarioElement) || 
                                        property.Value.TryGetProperty("comentario", out comentarioElement) || 
                                        property.Value.TryGetProperty("Comment", out comentarioElement) || 
                                        property.Value.TryGetProperty("comment", out comentarioElement))
                                    {
                                        comentario = comentarioElement.GetString() ?? "";
                                    }
                                    
                                    analysisResult.AnaliseGeral[criterioKey] = new CriteriaAnalysis
                                    {
                                        Nota = nota,
                                        Comentario = comentario
                                    };
                                }
                            }
                        }
                        
                        // Extrair comentário geral
                        if (root.TryGetProperty("ComentarioGeral", out var comentarioGeralElement) || 
                            root.TryGetProperty("comentarioGeral", out comentarioGeralElement) || 
                            root.TryGetProperty("GeneralComment", out comentarioGeralElement) || 
                            root.TryGetProperty("generalComment", out comentarioGeralElement))
                        {
                            analysisResult.ComentarioGeral = comentarioGeralElement.GetString() ?? "";
                        }
                        
                        // Extrair nota final
                        if (root.TryGetProperty("NotaFinal", out var notaFinalElement) || 
                            root.TryGetProperty("notaFinal", out notaFinalElement) ||
                            root.TryGetProperty("FinalScore", out notaFinalElement) ||
                            root.TryGetProperty("finalScore", out notaFinalElement))
                        {
                            if (notaFinalElement.TryGetInt32(out int notaValue))
                            {
                                analysisResult.NotaFinal = (int)NormalizeScore(notaValue);
                            }
                            else if (notaFinalElement.TryGetDouble(out double notaDouble))
                            {
                                analysisResult.NotaFinal = (int)NormalizeScore(notaDouble);
                            }
                            else if (notaFinalElement.ValueKind == JsonValueKind.String)
                            {
                                string scoreStr = notaFinalElement.GetString();
                                if (double.TryParse(scoreStr, out double scoreDouble))
                                {
                                    analysisResult.NotaFinal = (int)NormalizeScore(scoreDouble);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Nota final não encontrada no JSON. Calculando a partir dos critérios.");
                            // Calcular nota final como média das notas dos critérios
                            if (analysisResult.AnaliseGeral != null && analysisResult.AnaliseGeral.Count > 0)
                            {
                                var validCriteria = analysisResult.AnaliseGeral.Values.Where(c => c.Nota > 0).ToList();
                                if (validCriteria.Any())
                                {
                                    analysisResult.NotaFinal = (int)Math.Round(validCriteria.Average(c => c.Nota), 0);
                                    _logger.LogInformation($"Nota final calculada: {analysisResult.NotaFinal}");
                                }
                                else
                                {
                                    analysisResult.NotaFinal = 50; // Valor neutro padrão
                                    _logger.LogWarning("Nenhum critério com nota válida encontrado. Usando valor neutro padrão (50).");
                                }
                            }
                            else
                            {
                                analysisResult.NotaFinal = 50; // Valor neutro padrão
                                _logger.LogWarning("Nenhum critério encontrado para calcular a nota final. Usando valor neutro padrão (50).");
                            }
                        }
                        
                        // Extrair proposta de refatoração
                        if (root.TryGetProperty("PropostaRefatoracao", out var propostaElement) || 
                            root.TryGetProperty("propostaRefatoracao", out propostaElement) ||
                            root.TryGetProperty("RefactoringProposal", out propostaElement) ||
                            root.TryGetProperty("refactoringProposal", out propostaElement))
                        {
                            ExtractRefactoringProposal(propostaElement, analysisResult.PropostaRefatoracao);
                        }
                        else
                        {
                            // Tentar extrair proposta de refatoração do texto completo
                            ExtractRefactoringProposalFromText(textResponse, analysisResult.PropostaRefatoracao);
                        }
                        
                        return analysisResult;
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogWarning(jsonEx, "Erro ao analisar JSON: {ErrorMessage}. Tentando extrair informações diretamente do texto.", jsonEx.Message);
                    }
                }
                
                // Se não conseguiu extrair JSON ou ocorreu erro, tentar extrair informações diretamente do texto
                return ExtractAnalysisFromText(textResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao converter resposta para JSON: {ErrorMessage}", ex.Message);
                return CreateFallbackAnalysisResult();
            }
        }

        // Restante da implementação em arquivos separados...
    }
}
