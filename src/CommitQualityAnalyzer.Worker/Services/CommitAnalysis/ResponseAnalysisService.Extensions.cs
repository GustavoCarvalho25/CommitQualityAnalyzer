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
    /// Métodos de extensão para o ResponseAnalysisService
    /// </summary>
    public partial class ResponseAnalysisService
    {
        /// <summary>
        /// Extrai informações de análise diretamente do texto quando não é possível extrair JSON
        /// </summary>
        private CommitAnalysisResult ExtractAnalysisFromText(string textResponse)
        {
            _logger.LogInformation("Extraindo informações de análise diretamente do texto");
            
            var analysisResult = new CommitAnalysisResult
            {
                AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                NotaFinal = 0,
                ComentarioGeral = "",
                PropostaRefatoracao = new RefactoringProposal()
            };
            
            // Inicializar todos os critérios com valores padrão
            InitializeDefaultCriteria(analysisResult);
            
            try
            {
                // Extrair comentário geral
                var commentMatch = Regex.Match(textResponse, @"(?:Comentário\s*Geral|Análise\s*Geral|Conclusão)(?:\s*:|\s*-|\s*)\s*([^\n]*(?:\n(?!\*)[^\n]*)*)", RegexOptions.IgnoreCase);
                if (commentMatch.Success && commentMatch.Groups.Count > 1)
                {
                    analysisResult.ComentarioGeral = commentMatch.Groups[1].Value.Trim();
                    _logger.LogDebug("Comentário geral extraído: {Length} caracteres", analysisResult.ComentarioGeral.Length);
                }
                else
                {
                    // Se não encontrou um comentário específico, usar o início do texto
                    analysisResult.ComentarioGeral = textResponse.Length > 500 ? 
                        textResponse.Substring(0, 500) + "..." : 
                        textResponse;
                    _logger.LogDebug("Usando texto como comentário geral: {Length} caracteres", analysisResult.ComentarioGeral.Length);
                }
                
                // Extrair nota final
                var scoreMatch = Regex.Match(textResponse, @"(?:Nota\s*Final|Pontuação\s*Final|Final\s*Score)(?:\s*:|\s*-|\s*)\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                if (scoreMatch.Success && scoreMatch.Groups.Count > 1)
                {
                    if (double.TryParse(scoreMatch.Groups[1].Value, out double score))
                    {
                        analysisResult.NotaFinal = (int)NormalizeScore(score);
                        _logger.LogDebug("Nota final extraída: {Score}", analysisResult.NotaFinal);
                    }
                }
                
                // Extrair análises de critérios
                ExtractCriteriaAnalysis(textResponse, "Clean Code", analysisResult);
                ExtractCriteriaAnalysis(textResponse, "SOLID", analysisResult);
                ExtractCriteriaAnalysis(textResponse, "Design Patterns", analysisResult);
                ExtractCriteriaAnalysis(textResponse, "Testabilidade", analysisResult);
                ExtractCriteriaAnalysis(textResponse, "Seguranca", analysisResult);
                
                // Tentar extrações alternativas para critérios não encontrados
                if (!analysisResult.AnaliseGeral.ContainsKey("CleanCode") || analysisResult.AnaliseGeral["CleanCode"].Nota == 0)
                {
                    ExtractCriteriaAnalysis(textResponse, "Código Limpo", analysisResult, "CleanCode");
                }
                
                if (!analysisResult.AnaliseGeral.ContainsKey("DesignPatterns") || analysisResult.AnaliseGeral["DesignPatterns"].Nota == 0)
                {
                    ExtractCriteriaAnalysis(textResponse, "Padrões de Projeto", analysisResult, "DesignPatterns");
                }
                
                if (!analysisResult.AnaliseGeral.ContainsKey("Testabilidade") || analysisResult.AnaliseGeral["Testabilidade"].Nota == 0)
                {
                    ExtractCriteriaAnalysis(textResponse, "Testes", analysisResult, "Testabilidade");
                }
                
                if (!analysisResult.AnaliseGeral.ContainsKey("Seguranca") || analysisResult.AnaliseGeral["Seguranca"].Nota == 0)
                {
                    _logger.LogDebug("Tentando extrair critério Seguranca com variações alternativas");
                    
                    // Tentar extrair diretamente do JSON
                    var segurancaJsonMatch = Regex.Match(textResponse, @"""Seguranca""\s*:\s*\{\s*""Nota""\s*:\s*(\d+(?:\.\d+)?)\s*,\s*""Comentario""\s*:\s*""([^""]*)""\s*\}", RegexOptions.IgnoreCase);
                    if (segurancaJsonMatch.Success)
                    {
                        _logger.LogDebug("Encontrado critério Seguranca diretamente no JSON");
                        if (double.TryParse(segurancaJsonMatch.Groups[1].Value, out double scoreDouble))
                        {
                            double normalizedScore = NormalizeScore(scoreDouble);
                            int score = (int)Math.Round(normalizedScore);
                            
                            analysisResult.AnaliseGeral["Seguranca"] = new CriteriaAnalysis
                            {
                                Nota = score,
                                Comentario = segurancaJsonMatch.Groups[2].Value.Trim()
                            };
                        }
                    }
                    else
                    {
                        // Tentar variações do termo
                        ExtractCriteriaAnalysis(textResponse, "Segurança", analysisResult, "Seguranca");
                        
                        // Tentar outras variações do termo seguranca
                        if (!analysisResult.AnaliseGeral.ContainsKey("Seguranca") || analysisResult.AnaliseGeral["Seguranca"].Nota == 0)
                        {
                            ExtractCriteriaAnalysis(textResponse, "Security", analysisResult, "Seguranca");
                        }
                        
                        // Tentar com termo "Seguranca" exato
                        if (!analysisResult.AnaliseGeral.ContainsKey("Seguranca") || analysisResult.AnaliseGeral["Seguranca"].Nota == 0)
                        {
                            ExtractCriteriaAnalysis(textResponse, "Seguranca", analysisResult, "Seguranca");
                        }
                    }
                }
                
                // Extrair proposta de refatoração
                ExtractRefactoringProposalFromText(textResponse, analysisResult.PropostaRefatoracao);
                
                // Calcular nota final se não foi encontrada
                if (analysisResult.NotaFinal == 0)
                {
                    // Calcular a média apenas dos critérios que têm nota > 0
                    var validCriteria = analysisResult.AnaliseGeral.Values.Where(a => a.Nota > 0).ToList();
                    
                    _logger.LogDebug("Calculando nota final a partir de {Count} critérios válidos", validCriteria.Count);
                    
                    if (validCriteria.Count > 0)
                    {
                        // Calcular a média e arredondar para um número inteiro
                        double average = validCriteria.Average(a => a.Nota);
                        analysisResult.NotaFinal = (int)Math.Round(average);
                        
                        _logger.LogDebug("Nota final calculada: {Score}", analysisResult.NotaFinal);
                    }
                    else
                    {
                        // Se não há critérios válidos, usar um valor neutro
                        analysisResult.NotaFinal = 50;
                        _logger.LogDebug("Nenhum critério válido encontrado. Usando nota final padrão: {Score}", analysisResult.NotaFinal);
                    }
                }
                
                return analysisResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair informações do texto: {ErrorMessage}", ex.Message);
                return CreateFallbackAnalysisResult();
            }
        }

        /// <summary>
        /// Extrai informações de um critério específico do texto
        /// </summary>
        private void ExtractCriteriaAnalysis(string textResponse, string criteriaName, CommitAnalysisResult analysisResult, string criteriaKey = null)
        {
            try
            {
                criteriaKey = criteriaKey ?? criteriaName.Replace(" ", "");
                
                // Padrões para capturar notas em diferentes formatos
                var patterns = new[]
                {
                    // Padrão 1: "Clean Code: 8/10 - Comentário"
                    "(?:\\*\\s*)?" + Regex.Escape(criteriaName) + "(?:\\s*|\\s*:\\s*)(\\d+(?:\\.\\d+)?)(?:/\\d+)?(?:\\s*[-:]\\s*|\\s+)([^\\n]*(?:\\n(?!\\*)[^\\n]*)*)",
                    
                    // Padrão 2: "Clean Code (8/10): Comentário"
                    "(?:\\*\\s*)?" + Regex.Escape(criteriaName) + "\\s*\\((\\d+(?:\\.\\d+)?)(?:/\\d+)?\\)(?:\\s*[-:]\\s*|\\s+)([^\\n]*(?:\\n(?!\\*)[^\\n]*)*)",
                    
                    // Padrão 3: "* Clean Code: Comentário (8/10)"
                    "(?:\\*\\s*)?" + Regex.Escape(criteriaName) + "(?:\\s*|\\s*:\\s*)([^\\n]*(?:\\n(?!\\*)[^\\n]*)*)(?:\\s*\\((\\d+(?:\\.\\d+)?)(?:/\\d+)?\\))",
                    
                    // Padrão 4: "Clean Code - Nota: 8.5 - Comentário"
                    "(?:\\*\\s*)?" + Regex.Escape(criteriaName) + "(?:\\s*|\\s*[-:]\\s*)(?:[^\\n]*?Nota\\s*:\\s*|\\s*)(\\d+(?:\\.\\d+)?)(?:\\s*[-:]\\s*|\\s+)([^\\n]*(?:\\n(?!\\*)[^\\n]*)*)",
                    
                    // Padrão 5: JSON-like format "CleanCode": { "Nota": 8.5, "Comentario": "..."
                    "\"" + Regex.Escape(criteriaName) + "\"\\s*:\\s*\\{\\s*\"Nota\"\\s*:\\s*(\\d+(?:\\.\\d+)?)\\s*,\\s*\"Comentario\"\\s*:\\s*\"([^\"]*)\"",
                    
                    // Padrão 6: Formato JSON sem aspas "CleanCode": { Nota: 8.5, Comentario: "..."
                    "\"" + Regex.Escape(criteriaName) + "\"\\s*:\\s*\\{\\s*Nota\\s*:\\s*(\\d+(?:\\.\\d+)?)\\s*,\\s*Comentario\\s*:\\s*\"([^\"]*)\""
                };
                
                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(textResponse, pattern, RegexOptions.IgnoreCase);
                    
                    if (match.Success && match.Groups.Count > 2)
                    {
                        string scoreStr;
                        string comment;
                        
                        // Verificar qual padrão está sendo usado e extrair os grupos corretamente
                        if (pattern == patterns[2]) // Padrão 3 tem grupos invertidos
                        {
                            scoreStr = match.Groups[2].Value.Trim();
                            comment = match.Groups[1].Value.Trim();
                        }
                        else // Padrões 0, 1, 3, 4, 5 têm o mesmo formato de grupos
                        {
                            scoreStr = match.Groups[1].Value.Trim();
                            comment = match.Groups[2].Value.Trim();
                        }
                        
                        _logger.LogDebug("Critério {CriteriaName} encontrado com padrão {PatternIndex}: Score={Score}, Comentário={Comment}", 
                            criteriaName, Array.IndexOf(patterns, pattern), scoreStr, comment);
                        
                        if (double.TryParse(scoreStr, out double scoreDouble))
                        {
                            // Normalizar para escala 0-100
                            double normalizedScore = NormalizeScore(scoreDouble);
                            
                            // Converter para int, já que a propriedade Nota é int
                            int score = (int)Math.Round(normalizedScore);
                            
                            // Criar o critério com suporte a subcritérios
                            var criteriaAnalysis = new CriteriaAnalysis
                            {
                                Nota = score,
                                Comentario = comment
                            };
                            
                            // Se for o critério CleanCode, tentar extrair subcritérios
                            if (criteriaKey == "CleanCode")
                            {
                                ExtractCleanCodeSubcriteria(textResponse, criteriaAnalysis);
                            }
                            
                            analysisResult.AnaliseGeral[criteriaKey] = criteriaAnalysis;
                            
                            _logger.LogDebug("Extraído critério {CriteriaName}: Nota={Score}, Comentário={Comment}", 
                                criteriaName, score, comment);
                            
                            return; // Encontrou uma correspondência, não precisa continuar
                        }
                    }
                }
                
                // Se chegou aqui, não encontrou o critério com os padrões específicos
                // Tentar um padrão mais genérico para extrair apenas o comentário
                var genericPattern = $@"(?:\*\s*)?{Regex.Escape(criteriaName)}(?:\s*|\s*:\s*)([^\n]*(?:\n(?!\*)[^\n]*)*)";
                var genericMatch = Regex.Match(textResponse, genericPattern, RegexOptions.IgnoreCase);
                
                if (genericMatch.Success && genericMatch.Groups.Count > 1)
                {
                    var comment = genericMatch.Groups[1].Value.Trim();
                    
                    if (!string.IsNullOrEmpty(comment))
                    {
                        // Tentar extrair um número do comentário
                        var numberMatch = Regex.Match(comment, @"(\d+(?:\.\d+)?)");
                        int score = 0;
                        
                        if (numberMatch.Success)
                        {
                            if (double.TryParse(numberMatch.Groups[1].Value, out double scoreDouble))
                            {
                                score = (int)Math.Round(NormalizeScore(scoreDouble));
                            }
                        }
                        
                        // Se não encontrou um número ou o score é 0, usar um valor padrão baseado no sentimento do comentário
                        if (score == 0)
                        {
                            // Análise de sentimento simplificada
                            var positiveWords = new[] { "bom", "ótimo", "excelente", "adequado", "correto", "bem", "positivo" };
                            var negativeWords = new[] { "ruim", "inadequado", "problema", "falha", "erro", "mal", "negativo", "pobre" };
                            
                            int positiveCount = positiveWords.Count(word => comment.ToLower().Contains(word));
                            int negativeCount = negativeWords.Count(word => comment.ToLower().Contains(word));
                            
                            if (positiveCount > negativeCount)
                            {
                                score = 70; // Valor positivo padrão
                            }
                            else if (negativeCount > positiveCount)
                            {
                                score = 30; // Valor negativo padrão
                            }
                            else
                            {
                                score = 50; // Valor neutro padrão
                            }
                        }
                        
                        analysisResult.AnaliseGeral[criteriaKey] = new CriteriaAnalysis
                        {
                            Nota = score,
                            Comentario = comment
                        };
                        
                        _logger.LogDebug("Extraído critério {CriteriaName} (apenas comentário): Nota={Score}, Comentário={Comment}", 
                            criteriaName, score, comment);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao extrair análise do critério {CriteriaName}: {ErrorMessage}", 
                    criteriaName, ex.Message);
            }
        }

        /// <summary>
        /// Extrai subcritérios de Clean Code do texto (nomenclaturaVariaveis, nomenclaturaMetodos, etc.)
        /// </summary>
        private void ExtractCleanCodeSubcriteria(string textResponse, CriteriaAnalysis cleanCodeCriteria)
        {
            try
            {
                // Extrair nota e comentários sobre nomenclatura de variáveis
                var nomenclaturaVariaveisMatch = Regex.Match(textResponse, @"nomenclatura\s+das\s+variáveis[^\d]+(\d+)[^\d]+([^\n]+)", RegexOptions.IgnoreCase);
                
                // Extrair nota e comentários sobre nomenclatura de métodos
                var nomenclaturaMetodosMatch = Regex.Match(textResponse, @"nomenclatura\s+dos\s+métodos[^\d]+(\d+)[^\d]+([^\n]+)", RegexOptions.IgnoreCase);
                
                // Extrair nota e comentários sobre tamanho de funções
                var tamanhoFuncoesMatch = Regex.Match(textResponse, @"tamanho\s+das\s+funções[^\d]+(\d+)[^\d]+([^\n]+)", RegexOptions.IgnoreCase);
                
                // Extrair nota e comentários sobre comentários
                var comentariosMatch = Regex.Match(textResponse, @"comentários[^\d]+(\d+)[^\d]+([^\n]+)", RegexOptions.IgnoreCase);
                
                // Extrair nota e comentários sobre duplicação de código
                var duplicacaoCodigoMatch = Regex.Match(textResponse, @"duplicação\s+de\s+código[^\d]+(\d+)[^\d]+([^\n]+)", RegexOptions.IgnoreCase);
                
                // Adicionar subcritérios se encontrados
                if (nomenclaturaVariaveisMatch.Success)
                {
                    int nota = int.TryParse(nomenclaturaVariaveisMatch.Groups[1].Value, out int n) ? n : 5;
                    string comentario = nomenclaturaVariaveisMatch.Groups[2].Value.Trim();
                    cleanCodeCriteria.Subcriteria["nomenclaturaVariaveis"] = new SubcriteriaAnalysis { Nota = nota, Comentario = comentario };
                    _logger.LogDebug("Extraído subcritério nomenclaturaVariaveis: Nota={Nota}, Comentário={Comentario}", nota, comentario);
                }
                
                if (nomenclaturaMetodosMatch.Success)
                {
                    int nota = int.TryParse(nomenclaturaMetodosMatch.Groups[1].Value, out int n) ? n : 5;
                    string comentario = nomenclaturaMetodosMatch.Groups[2].Value.Trim();
                    cleanCodeCriteria.Subcriteria["nomenclaturaMetodos"] = new SubcriteriaAnalysis { Nota = nota, Comentario = comentario };
                    _logger.LogDebug("Extraído subcritério nomenclaturaMetodos: Nota={Nota}, Comentário={Comentario}", nota, comentario);
                }
                
                if (tamanhoFuncoesMatch.Success)
                {
                    int nota = int.TryParse(tamanhoFuncoesMatch.Groups[1].Value, out int n) ? n : 5;
                    string comentario = tamanhoFuncoesMatch.Groups[2].Value.Trim();
                    cleanCodeCriteria.Subcriteria["tamanhoFuncoes"] = new SubcriteriaAnalysis { Nota = nota, Comentario = comentario };
                    _logger.LogDebug("Extraído subcritério tamanhoFuncoes: Nota={Nota}, Comentário={Comentario}", nota, comentario);
                }
                
                if (comentariosMatch.Success)
                {
                    int nota = int.TryParse(comentariosMatch.Groups[1].Value, out int n) ? n : 5;
                    string comentario = comentariosMatch.Groups[2].Value.Trim();
                    cleanCodeCriteria.Subcriteria["comentarios"] = new SubcriteriaAnalysis { Nota = nota, Comentario = comentario };
                    _logger.LogDebug("Extraído subcritério comentarios: Nota={Nota}, Comentário={Comentario}", nota, comentario);
                }
                
                if (duplicacaoCodigoMatch.Success)
                {
                    int nota = int.TryParse(duplicacaoCodigoMatch.Groups[1].Value, out int n) ? n : 5;
                    string comentario = duplicacaoCodigoMatch.Groups[2].Value.Trim();
                    cleanCodeCriteria.Subcriteria["duplicacaoCodigo"] = new SubcriteriaAnalysis { Nota = nota, Comentario = comentario };
                    _logger.LogDebug("Extraído subcritério duplicacaoCodigo: Nota={Nota}, Comentário={Comentario}", nota, comentario);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao extrair subcritérios de Clean Code: {ErrorMessage}", ex.Message);
            }
        }
        
        /// <summary>
        /// Extrai informações de proposta de refatoração do texto
        /// </summary>
        private void ExtractRefactoringProposalFromText(string textResponse, RefactoringProposal proposal)
        {
            try
            {
                // Extrair título da proposta
                var titleMatch = Regex.Match(textResponse, @"(?:Proposta\s*de\s*Refatoração|Refactoring\s*Proposal)(?:\s*:|\s*-|\s*)\s*([^\n]*)", RegexOptions.IgnoreCase);
                if (titleMatch.Success && titleMatch.Groups.Count > 1)
                {
                    proposal.Titulo = titleMatch.Groups[1].Value.Trim();
                    _logger.LogDebug("Título da proposta de refatoração extraído: {Title}", proposal.Titulo);
                }
                
                // Extrair descrição da proposta
                var descriptionMatch = Regex.Match(textResponse, @"(?:Proposta\s*de\s*Refatoração|Refactoring\s*Proposal)(?:\s*:|\s*-|\s*)\s*([^\n]*(?:\n(?!\#\#)[^\n]*)*)", RegexOptions.IgnoreCase);
                if (descriptionMatch.Success && descriptionMatch.Groups.Count > 1)
                {
                    proposal.Descricao = descriptionMatch.Groups[1].Value.Trim();
                    _logger.LogDebug("Descrição da proposta de refatoração extraída: {Length} caracteres", proposal.Descricao.Length);
                }
                
                // Extrair código original
                var originalCodeMatch = Regex.Match(textResponse, @"(?:Código\s*Original|Original\s*Code)(?:\s*:|\s*-|\s*)\s*```(?:csharp|cs|java|javascript|js)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
                if (originalCodeMatch.Success && originalCodeMatch.Groups.Count > 1)
                {
                    proposal.CodigoOriginal = originalCodeMatch.Groups[1].Value.Trim();
                    proposal.OriginalCode = proposal.CodigoOriginal; // Para compatibilidade
                    _logger.LogDebug("Código original extraído: {Length} caracteres", proposal.CodigoOriginal.Length);
                }
                
                // Extrair código refatorado
                var refactoredCodeMatch = Regex.Match(textResponse, @"(?:Código\s*Refatorado|Refactored\s*Code|Proposed\s*Code)(?:\s*:|\s*-|\s*)\s*```(?:csharp|cs|java|javascript|js)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
                if (refactoredCodeMatch.Success && refactoredCodeMatch.Groups.Count > 1)
                {
                    proposal.CodigoRefatorado = refactoredCodeMatch.Groups[1].Value.Trim();
                    proposal.ProposedCode = proposal.CodigoRefatorado; // Para compatibilidade
                    _logger.LogDebug("Código refatorado extraído: {Length} caracteres", proposal.CodigoRefatorado.Length);
                }
                
                // Extrair justificativa
                var justificationMatch = Regex.Match(textResponse, @"(?:Justificativa|Justification|Razão|Reason)(?:\s*:|\s*-|\s*)\s*([^\n]*(?:\n(?!\#\#)[^\n]*)*)", RegexOptions.IgnoreCase);
                if (justificationMatch.Success && justificationMatch.Groups.Count > 1)
                {
                    proposal.Justification = justificationMatch.Groups[1].Value.Trim();
                    _logger.LogDebug("Justificativa extraída: {Length} caracteres", proposal.Justification.Length);
                }
                
                // Extrair prioridade
                var priorityMatch = Regex.Match(textResponse, @"(?:Prioridade|Priority)(?:\s*:|\s*-|\s*)\s*(\d+)", RegexOptions.IgnoreCase);
                if (priorityMatch.Success && priorityMatch.Groups.Count > 1)
                {
                    if (int.TryParse(priorityMatch.Groups[1].Value, out int priority))
                    {
                        proposal.Priority = Math.Min(5, Math.Max(1, priority)); // Garantir que esteja entre 1 e 5
                        _logger.LogDebug("Prioridade extraída: {Priority}", proposal.Priority);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao extrair proposta de refatoração: {ErrorMessage}", ex.Message);
            }
        }

        /// <summary>
        /// Extrai informações de proposta de refatoração de um elemento JSON
        /// </summary>
        private void ExtractRefactoringProposal(JsonElement element, RefactoringProposal proposal)
        {
            try
            {
                if (element.TryGetProperty("Titulo", out var tituloElement) || 
                    element.TryGetProperty("titulo", out tituloElement) ||
                    element.TryGetProperty("Title", out tituloElement) ||
                    element.TryGetProperty("title", out tituloElement))
                {
                    proposal.Titulo = tituloElement.GetString() ?? "";
                }
                
                if (element.TryGetProperty("Descricao", out var descricaoElement) || 
                    element.TryGetProperty("descricao", out descricaoElement) ||
                    element.TryGetProperty("Description", out descricaoElement) ||
                    element.TryGetProperty("description", out descricaoElement))
                {
                    proposal.Descricao = descricaoElement.GetString() ?? "";
                }
                
                if (element.TryGetProperty("CodigoOriginal", out var codigoOriginalElement) || 
                    element.TryGetProperty("codigoOriginal", out codigoOriginalElement) ||
                    element.TryGetProperty("OriginalCode", out codigoOriginalElement) ||
                    element.TryGetProperty("originalCode", out codigoOriginalElement))
                {
                    proposal.CodigoOriginal = codigoOriginalElement.GetString() ?? "";
                    proposal.OriginalCode = proposal.CodigoOriginal; // Para compatibilidade
                }
                
                if (element.TryGetProperty("CodigoRefatorado", out var codigoRefatoradoElement) || 
                    element.TryGetProperty("codigoRefatorado", out codigoRefatoradoElement) ||
                    element.TryGetProperty("ProposedCode", out codigoRefatoradoElement) ||
                    element.TryGetProperty("proposedCode", out codigoRefatoradoElement))
                {
                    proposal.CodigoRefatorado = codigoRefatoradoElement.GetString() ?? "";
                    proposal.ProposedCode = proposal.CodigoRefatorado; // Para compatibilidade
                }
                
                if (element.TryGetProperty("Justification", out var justificationElement) || 
                    element.TryGetProperty("justification", out justificationElement) ||
                    element.TryGetProperty("Justificativa", out justificationElement) ||
                    element.TryGetProperty("justificativa", out justificationElement))
                {
                    proposal.Justification = justificationElement.GetString() ?? "";
                }
                
                if (element.TryGetProperty("Priority", out var priorityElement) || 
                    element.TryGetProperty("priority", out priorityElement) ||
                    element.TryGetProperty("Prioridade", out priorityElement) ||
                    element.TryGetProperty("prioridade", out priorityElement))
                {
                    if (priorityElement.TryGetInt32(out int priority))
                    {
                        proposal.Priority = Math.Min(5, Math.Max(1, priority)); // Garantir que esteja entre 1 e 5
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao extrair proposta de refatoração do JSON: {ErrorMessage}", ex.Message);
            }
        }
    }
}
