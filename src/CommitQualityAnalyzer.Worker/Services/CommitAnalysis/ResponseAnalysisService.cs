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
                // Extrair JSON da resposta
                string jsonContent = ExtractJsonFromText(textResponse);
                
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    try
                    {
                        // Tentar corrigir o formato do JSON se necessário
                        jsonContent = TryFixJsonFormat(jsonContent);
                        
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
                        
                        // Verificar se estamos recebendo o formato antigo (nomenclaturaVariaveis, etc.)
                        bool isOldFormat = false;
                        JsonElement analiseGeral;
                        
                        if (root.TryGetProperty("analiseGeral", out analiseGeral) || 
                            root.TryGetProperty("AnaliseGeral", out analiseGeral))
                        {
                            foreach (var property in analiseGeral.EnumerateObject())
                            {
                                string propName = property.Name.ToLower();
                                if (propName == "nomenclaturametodos" || propName == "nomenclaturavariáveis" || 
                                    propName == "nomenclaturavariaveis" || propName == "tamanhofuncoes" || 
                                    propName == "duplicacaocodigo" || propName == "comentarios")
                                {
                                    isOldFormat = true;
                                    break;
                                }
                            }
                            
                            if (isOldFormat)
                            {
                                _logger.LogInformation("Detectado formato antigo de resposta com subcritérios específicos");
                                ProcessOldFormatResponse(root, analysisResult);
                                return analysisResult;
                            }
                        }
                        
                        // Processar formato padrão esperado
                        foreach (var property in analiseGeral.EnumerateObject())
                        {
                            string criterioKey = property.Name;
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
                        
                        // Extrair comentário geral
                        if (root.TryGetProperty("comentarioGeral", out var comentarioGeralElement) ||
                            root.TryGetProperty("ComentarioGeral", out comentarioGeralElement))
                        {
                            analysisResult.ComentarioGeral = comentarioGeralElement.GetString() ?? "";
                        }
                        
                        // Extrair proposta de refatoração
                        if (root.TryGetProperty("SugestaoRefatoracao", out var sugestaoElement) ||
                            root.TryGetProperty("sugestaoRefatoracao", out sugestaoElement))
                        {
                            string sugestao = sugestaoElement.GetString() ?? "";
                            if (!string.IsNullOrEmpty(sugestao))
                            {
                                analysisResult.PropostaRefatoracao.Descricao = sugestao;
                                analysisResult.PropostaRefatoracao.Titulo = "Sugestão de Refatoração";
                            }
                        }
                        
                        // Calcular nota final (média das notas dos critérios)
                        double totalScore = 0;
                        int criteriaCount = 0;
                        
                        foreach (var criteria in analysisResult.AnaliseGeral.Values)
                        {
                            totalScore += criteria.Nota;
                            criteriaCount++;
                        }
                        
                        analysisResult.NotaFinal = criteriaCount > 0 ? (int)Math.Round(totalScore / criteriaCount) : 50;
                        
                        return analysisResult;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar JSON: {ErrorMessage}", ex.Message);
                    }
                }
                
                // Se não conseguiu extrair JSON ou ocorreu erro, tentar extrair informações diretamente do texto
                var result = ExtractAnalysisFromText(textResponse);
                
                // Verificar se o resultado contém apenas valores padrão
                bool hasOnlyDefaultValues = true;
                foreach (var criteria in result.AnaliseGeral.Values)
                {
                    if (criteria.Nota != 50 || criteria.Comentario != "Não foi possível extrair informações para este critério")
                    {
                        hasOnlyDefaultValues = false;
                        break;
                    }
                }
                
                // Se contém apenas valores padrão, mas tem comentário geral, extrair informações do comentário
                if (hasOnlyDefaultValues && !string.IsNullOrEmpty(result.ComentarioGeral))
                {
                    _logger.LogInformation("Extraindo informações do comentário geral...");
                    ExtractInfoFromGeneralComment(result);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao converter resposta para JSON: {ErrorMessage}", ex.Message);
                return CreateFallbackAnalysisResult();
            }
        }
        
        /// <summary>
        /// Processa a resposta no formato antigo com critérios específicos (nomenclaturaVariaveis, etc.)
        /// </summary>
        private void ProcessOldFormatResponse(JsonElement root, CommitAnalysisResult analysisResult)
        {
            try
            {
                _logger.LogInformation("Processando resposta no formato antigo");
                
                // Criar o critério CleanCode para armazenar os subcritérios
                var cleanCodeCriteria = new CriteriaAnalysis
                {
                    Nota = 0,
                    Comentario = "Análise de Clean Code baseada em múltiplos critérios."
                };
                
                double totalScore = 0;
                int criteriaCount = 0;
                
                // Processar os subcritérios do formato antigo
                if (root.TryGetProperty("analiseGeral", out var analiseGeral) || 
                    root.TryGetProperty("AnaliseGeral", out analiseGeral))
                {
                    foreach (var property in analiseGeral.EnumerateObject())
                    {
                        if (property.Value.TryGetProperty("nota", out var notaElement))
                        {
                            int nota = 0;
                            if (notaElement.TryGetInt32(out int notaInt))
                            {
                                nota = notaInt;
                            }
                            else if (notaElement.TryGetDouble(out double notaDouble))
                            {
                                nota = (int)Math.Round(notaDouble);
                            }
                            else if (notaElement.ValueKind == JsonValueKind.String)
                            {
                                string notaStr = notaElement.GetString();
                                if (double.TryParse(notaStr, out double notaFromStr))
                                {
                                    nota = (int)Math.Round(notaFromStr);
                                }
                            }
                            
                            // Extrair comentário
                            string comentario = "";
                            if (property.Value.TryGetProperty("comentario", out var comentarioElement))
                            {
                                comentario = comentarioElement.GetString() ?? "";
                            }
                            
                            // Adicionar subcritério
                            cleanCodeCriteria.Subcriteria[property.Name] = new SubcriteriaAnalysis
                            {
                                Nota = nota,
                                Comentario = comentario
                            };
                            
                            // Acumular para calcular a média
                            totalScore += nota * 10; // Converter para escala 0-100
                            criteriaCount++;
                        }
                    }
                }
                
                // Calcular a nota média para CleanCode
                int cleanCodeScore = criteriaCount > 0 ? (int)Math.Round(totalScore / criteriaCount) : 50;
                cleanCodeCriteria.Nota = cleanCodeScore;
                
                // Adicionar CleanCode como critério principal
                analysisResult.AnaliseGeral["CleanCode"] = cleanCodeCriteria;
                
                // Adicionar outros critérios com valores neutros
                analysisResult.AnaliseGeral["SOLID"] = new CriteriaAnalysis
                {
                    Nota = 50,
                    Comentario = "Não foi possível extrair informações para este critério"
                };
                
                analysisResult.AnaliseGeral["DesignPatterns"] = new CriteriaAnalysis
                {
                    Nota = 50,
                    Comentario = "Não foi possível extrair informações para este critério"
                };
                
                analysisResult.AnaliseGeral["Testabilidade"] = new CriteriaAnalysis
                {
                    Nota = 50,
                    Comentario = "Não foi possível extrair informações para este critério"
                };
                
                analysisResult.AnaliseGeral["Seguranca"] = new CriteriaAnalysis
                {
                    Nota = 50,
                    Comentario = "Não foi possível extrair informações para este critério"
                };
                
                // Extrair comentário geral
                if (root.TryGetProperty("comentarioGeral", out var comentarioGeralElement) ||
                    root.TryGetProperty("ComentarioGeral", out comentarioGeralElement))
                {
                    analysisResult.ComentarioGeral = comentarioGeralElement.GetString() ?? "";
                }
                
                // Extrair proposta de refatoração
                if (root.TryGetProperty("SugestaoRefatoracao", out var sugestaoElement) ||
                    root.TryGetProperty("sugestaoRefatoracao", out sugestaoElement))
                {
                    string sugestao = sugestaoElement.GetString() ?? "";
                    if (!string.IsNullOrEmpty(sugestao))
                    {
                        analysisResult.PropostaRefatoracao.Descricao = sugestao;
                        analysisResult.PropostaRefatoracao.Titulo = "Sugestão de Refatoração";
                    }
                }
                
                // Calcular nota final
                analysisResult.NotaFinal = cleanCodeScore;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar resposta no formato antigo: {ErrorMessage}", ex.Message);
            }
        }
        
        /// <summary>
        /// Extrai informações do comentário geral quando o JSON não foi processado corretamente
        /// </summary>
        private void ExtractInfoFromGeneralComment(CommitAnalysisResult result)
        {
            try
            {
                string comment = result.ComentarioGeral;
                
                // Extrair nota e comentários sobre nomenclatura de variáveis
                var nomenclaturaVariaveisMatch = Regex.Match(comment, @"nomenclatura\s+das\s+variáveis[^\d]+(\d+)[^\d]+([^\n]+)", RegexOptions.IgnoreCase);
                
                // Extrair nota e comentários sobre nomenclatura de métodos
                var nomenclaturaMetodosMatch = Regex.Match(comment, @"nomenclatura\s+dos\s+métodos[^\d]+(\d+)[^\d]+([^\n]+)", RegexOptions.IgnoreCase);
                
                // Extrair nota e comentários sobre tamanho de funções
                var tamanhoFuncoesMatch = Regex.Match(comment, @"tamanho\s+das\s+funções[^\d]+(\d+)[^\d]+([^\n]+)", RegexOptions.IgnoreCase);
                
                // Extrair nota e comentários sobre comentários
                var comentariosMatch = Regex.Match(comment, @"comentários[^\d]+(\d+)[^\d]+([^\n]+)", RegexOptions.IgnoreCase);
                
                // Extrair nota e comentários sobre duplicação de código
                var duplicacaoCodigoMatch = Regex.Match(comment, @"duplicação\s+de\s+código[^\d]+(\d+)[^\d]+([^\n]+)", RegexOptions.IgnoreCase);
                
                // Criar critério CleanCode com subcritérios
                var cleanCodeCriteria = new CriteriaAnalysis
                {
                    Nota = 0,
                    Comentario = "Análise baseada em múltiplos critérios de Clean Code."
                };
                
                int totalScore = 0;
                int criteriaCount = 0;
                
                // Adicionar subcritérios se encontrados
                if (nomenclaturaVariaveisMatch.Success)
                {
                    int nota = int.TryParse(nomenclaturaVariaveisMatch.Groups[1].Value, out int n) ? n : 5;
                    string comentario = nomenclaturaVariaveisMatch.Groups[2].Value.Trim();
                    cleanCodeCriteria.Subcriteria["nomenclaturaVariaveis"] = new SubcriteriaAnalysis { Nota = nota, Comentario = comentario };
                    totalScore += nota * 10;
                    criteriaCount++;
                }
                
                if (nomenclaturaMetodosMatch.Success)
                {
                    int nota = int.TryParse(nomenclaturaMetodosMatch.Groups[1].Value, out int n) ? n : 5;
                    string comentario = nomenclaturaMetodosMatch.Groups[2].Value.Trim();
                    cleanCodeCriteria.Subcriteria["nomenclaturaMetodos"] = new SubcriteriaAnalysis { Nota = nota, Comentario = comentario };
                    totalScore += nota * 10;
                    criteriaCount++;
                }
                
                if (tamanhoFuncoesMatch.Success)
                {
                    int nota = int.TryParse(tamanhoFuncoesMatch.Groups[1].Value, out int n) ? n : 5;
                    string comentario = tamanhoFuncoesMatch.Groups[2].Value.Trim();
                    cleanCodeCriteria.Subcriteria["tamanhoFuncoes"] = new SubcriteriaAnalysis { Nota = nota, Comentario = comentario };
                    totalScore += nota * 10;
                    criteriaCount++;
                }
                
                if (comentariosMatch.Success)
                {
                    int nota = int.TryParse(comentariosMatch.Groups[1].Value, out int n) ? n : 5;
                    string comentario = comentariosMatch.Groups[2].Value.Trim();
                    cleanCodeCriteria.Subcriteria["comentarios"] = new SubcriteriaAnalysis { Nota = nota, Comentario = comentario };
                    totalScore += nota * 10;
                    criteriaCount++;
                }
                
                if (duplicacaoCodigoMatch.Success)
                {
                    int nota = int.TryParse(duplicacaoCodigoMatch.Groups[1].Value, out int n) ? n : 5;
                    string comentario = duplicacaoCodigoMatch.Groups[2].Value.Trim();
                    cleanCodeCriteria.Subcriteria["duplicacaoCodigo"] = new SubcriteriaAnalysis { Nota = nota, Comentario = comentario };
                    totalScore += nota * 10;
                    criteriaCount++;
                }
                
                // Calcular nota média para CleanCode
                if (criteriaCount > 0)
                {
                    cleanCodeCriteria.Nota = totalScore / criteriaCount;
                }
                
                // Extrair sugestão de refatoração
                var refactoringMatch = Regex.Match(comment, @"Sugestão\s+de\s+refatoração:([^.]*(?:\.[^.]*)*)", RegexOptions.IgnoreCase);
                if (refactoringMatch.Success)
                {
                    string refactoringText = refactoringMatch.Groups[1].Value.Trim();
                    result.PropostaRefatoracao.Descricao = refactoringText;
                    result.PropostaRefatoracao.Titulo = "Sugestão de Refatoração";
                }
                
                // Atualizar o critério CleanCode
                result.AnaliseGeral["CleanCode"] = cleanCodeCriteria;
                
                // Atualizar a nota final
                result.NotaFinal = cleanCodeCriteria.Nota;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair informações do comentário geral: {ErrorMessage}", ex.Message);
            }
        }
        
        /// <summary>
        /// Extrai o JSON da resposta em texto
        /// </summary>
        private string ExtractJsonFromText(string textResponse)
        {
            try
            {
                // Tentar extrair JSON usando padrão de bloco de código
                var jsonMatch = Regex.Match(textResponse, @"```(?:json)?\s*({[\s\S]*?})\s*```", RegexOptions.Singleline);
                if (jsonMatch.Success && jsonMatch.Groups.Count > 1)
                {
                    string jsonContent = jsonMatch.Groups[1].Value.Trim();
                    _logger.LogDebug("JSON extraído de bloco de código: {JsonLength} caracteres", jsonContent.Length);
                    return jsonContent;
                }
                
                // Tentar extrair JSON diretamente (sem bloco de código)
                jsonMatch = Regex.Match(textResponse, @"({\s*""(?:analiseGeral|AnaliseGeral|nomenclaturaVariaveis|nomenclaturaMetodos|tamanhoFuncoes|comentarios|duplicacaoCodigo)""\s*:.*})", RegexOptions.Singleline);
                if (jsonMatch.Success && jsonMatch.Groups.Count > 1)
                {
                    string jsonContent = jsonMatch.Groups[1].Value.Trim();
                    _logger.LogDebug("JSON extraído diretamente: {JsonLength} caracteres", jsonContent.Length);
                    return jsonContent;
                }
                
                _logger.LogWarning("Não foi possível extrair JSON da resposta");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair JSON da resposta: {ErrorMessage}", ex.Message);
                return string.Empty;
            }
        }
    }
}
