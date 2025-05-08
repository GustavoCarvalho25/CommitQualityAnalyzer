using CommitQualityAnalyzer.Worker.Services.CommitAnalysis.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Serviço simplificado para analisar respostas do modelo Ollama
    /// Foca apenas no formato padrão solicitado no prompt
    /// </summary>
    public class SimpleResponseAnalysisService
    {
        private readonly ILogger<SimpleResponseAnalysisService> _logger;

        public SimpleResponseAnalysisService(ILogger<SimpleResponseAnalysisService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Converte a resposta do modelo em um objeto CommitAnalysisResult
        /// </summary>
        public CommitAnalysisResult ProcessResponse(string textResponse)
        {
            _logger.LogInformation("Processando resposta do modelo");
            
            if (string.IsNullOrEmpty(textResponse))
            {
                _logger.LogWarning("Resposta vazia recebida");
                return CreateFallbackAnalysisResult();
            }

            try
            {
                // Extrair o JSON da resposta
                string jsonContent = ExtractJsonFromText(textResponse);
                
                if (string.IsNullOrEmpty(jsonContent))
                {
                    _logger.LogWarning("Não foi possível extrair JSON da resposta");
                    return CreateFallbackAnalysisResult();
                }

                try {
                    // Tentar corrigir problemas comuns no JSON
                    jsonContent = TryFixJsonFormat(jsonContent);
                    
                    // Remover comentários que possam estar causando problemas
                    jsonContent = RemoveJsonComments(jsonContent);
                    
                    _logger.LogInformation("JSON após correções: {JsonContent}", jsonContent);
                    
                    // Criar o resultado da análise
                    var result = new CommitAnalysisResult
                    {
                        AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                        NotaFinal = 0,
                        ComentarioGeral = ""
                    };

                    // Parsear o JSON
                    using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                    {
                    JsonElement root = doc.RootElement;
                    
                    // Extrair comentário geral
                    if (root.TryGetProperty("comentarioGeral", out JsonElement comentarioGeralElement))
                    {
                        result.ComentarioGeral = comentarioGeralElement.GetString() ?? "Análise concluída";
                    }
                    
                    // Processar critérios de análise
                    if (root.TryGetProperty("analiseGeral", out JsonElement analiseGeralElement))
                    {
                        // Verificar se há um critério CleanCode explícito
                        bool hasCleanCodeCriteria = false;
                        CriteriaAnalysis cleanCodeCriteria = null;
                        
                        // Primeiro, verificar se temos um critério CleanCode explícito
                        foreach (var property in analiseGeralElement.EnumerateObject())
                        {
                            if (property.Name.Equals("CleanCode", StringComparison.OrdinalIgnoreCase))
                            {
                                hasCleanCodeCriteria = true;
                                _logger.LogInformation("Encontrado critério CleanCode explícito");
                                
                                // Extrair nota e comentário para o critério CleanCode
                                int notaCleanCode = 0;
                                string comentarioCleanCode = "Análise de Clean Code baseada em múltiplos critérios.";
                                
                                if (property.Value.TryGetProperty("nota", out JsonElement notaElement))
                                {
                                    if (notaElement.ValueKind == JsonValueKind.Number)
                                    {
                                        notaCleanCode = notaElement.GetInt32();
                                    }
                                    else if (notaElement.ValueKind == JsonValueKind.String)
                                    {
                                        string notaStr = notaElement.GetString() ?? "0";
                                        int.TryParse(notaStr, out notaCleanCode);
                                    }
                                }
                                
                                if (property.Value.TryGetProperty("comentario", out JsonElement comentarioElement))
                                {
                                    comentarioCleanCode = comentarioElement.GetString() ?? comentarioCleanCode;
                                }
                                
                                // Criar o critério CleanCode
                                cleanCodeCriteria = new CriteriaAnalysis
                                {
                                    Nota = notaCleanCode,
                                    Comentario = comentarioCleanCode,
                                    Subcriteria = new Dictionary<string, SubcriteriaAnalysis>()
                                };
                                
                                // Verificar se há subcritérios dentro do critério CleanCode
                                if (property.Value.TryGetProperty("subcriteria", out JsonElement subcriteriasElement) ||
                                    property.Value.TryGetProperty("subcritérios", out subcriteriasElement) ||
                                    property.Value.TryGetProperty("subcriterios", out subcriteriasElement))
                                {
                                    ProcessSubcriteriosObject(subcriteriasElement, cleanCodeCriteria);
                                }
                                
                                break;
                            }
                        }
                        
                        // Se não encontrou um critério CleanCode explícito, criar um
                        if (!hasCleanCodeCriteria)
                        {
                            _logger.LogInformation("Criando critério CleanCode implícito a partir dos subcritérios");
                            cleanCodeCriteria = new CriteriaAnalysis
                            {
                                Nota = 0,
                                Comentario = "Análise de Clean Code baseada em múltiplos critérios.",
                                Subcriteria = new Dictionary<string, SubcriteriaAnalysis>()
                            };
                            
                            double totalScore = 0;
                            int criteriaCount = 0;
                            
                            // Processar cada propriedade como um possível subcritério
                            foreach (var property in analiseGeralElement.EnumerateObject())
                            {
                                // Verificar se é um dos subcritérios conhecidos
                                if (IsKnownSubcriteria(property.Name))
                                {
                                    string subcriteriaNome = NormalizeSubcriteriaName(property.Name);
                                    int nota = 0;
                                    string comentario = "";
                                    
                                    // Extrair nota e comentário
                                    if (property.Value.TryGetProperty("nota", out JsonElement notaElement))
                                    {
                                        if (notaElement.ValueKind == JsonValueKind.Number)
                                        {
                                            nota = notaElement.GetInt32();
                                        }
                                        else if (notaElement.ValueKind == JsonValueKind.String)
                                        {
                                            string notaStr = notaElement.GetString() ?? "0";
                                            int.TryParse(notaStr, out nota);
                                        }
                                    }
                                    
                                    if (property.Value.TryGetProperty("comentario", out JsonElement comentarioElement))
                                    {
                                        comentario = comentarioElement.GetString() ?? "";
                                    }
                                    
                                    // Adicionar subcritério
                                    cleanCodeCriteria.Subcriteria[subcriteriaNome] = new SubcriteriaAnalysis
                                    {
                                        Nota = nota,
                                        Comentario = comentario
                                    };
                                    
                                    // Acumular para cálculo da média
                                    totalScore += nota;
                                    criteriaCount++;
                                    
                                    _logger.LogInformation("Subcritério processado: {Nome}, Nota: {Nota}", 
                                        subcriteriaNome, nota);
                                }
                                else
                                {
                                    // Processar como critério normal (não subcritério)
                                    ProcessCriterio(property, result);
                                }
                            }
                            
                            // Calcular nota média para o critério CleanCode
                            if (criteriaCount > 0)
                            {
                                cleanCodeCriteria.Nota = (int)Math.Round(totalScore / criteriaCount);
                            }
                        }
                        
                        // Garantir que todos os subcritérios padrão estejam presentes
                        EnsureDefaultSubcriteria(cleanCodeCriteria);
                        
                        // Adicionar o critério CleanCode
                        result.AnaliseGeral["CleanCode"] = cleanCodeCriteria;
                        
                        // Calcular nota final como média de todos os critérios
                        CalculateOverallScore(result);
                    }
                    
                    // Processar proposta de refatoração
                    ProcessRefactoringProposal(root, result);
                    }
                    
                    return result;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Erro ao parsear JSON: {ErrorMessage}. JSON: {JsonContent}", jsonEx.Message, jsonContent);
                    
                    // Tentar extrair subcritérios diretamente do texto usando regex
                    var result = ExtractResultsUsingRegex(textResponse);
                    if (result != null)
                    {
                        _logger.LogInformation("Conseguiu extrair resultados usando regex");
                        return result;
                    }
                    
                    return CreateFallbackAnalysisResult();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar resposta: {ErrorMessage}", ex.Message);
                return CreateFallbackAnalysisResult();
            }
        }
        
        /// <summary>
        /// Processa a proposta de refatoração da resposta
        /// </summary>
        private void ProcessRefactoringProposal(JsonElement root, CommitAnalysisResult result)
        {
            try
            {
                // Verificar se existe proposta de refatoração no formato completo
                if (root.TryGetProperty("propostaRefatoracao", out JsonElement propostaElement))
                {
                    var proposta = new RefactoringProposal();
                    
                    if (propostaElement.TryGetProperty("titulo", out JsonElement tituloElement))
                    {
                        proposta.Titulo = tituloElement.GetString() ?? "";
                    }
                    
                    if (propostaElement.TryGetProperty("descricao", out JsonElement descricaoElement))
                    {
                        proposta.Descricao = descricaoElement.GetString() ?? "";
                    }
                    
                    if (propostaElement.TryGetProperty("codigoOriginal", out JsonElement originalElement))
                    {
                        proposta.CodigoOriginal = originalElement.GetString() ?? "";
                        proposta.OriginalCode = proposta.CodigoOriginal;
                    }
                    
                    if (propostaElement.TryGetProperty("codigoRefatorado", out JsonElement refatoradoElement))
                    {
                        proposta.CodigoRefatorado = refatoradoElement.GetString() ?? "";
                        proposta.ProposedCode = proposta.CodigoRefatorado;
                    }
                    
                    result.PropostaRefatoracao = proposta;
                }
                // Verificar se existe sugestão de refatoração no formato simples
                else if (root.TryGetProperty("SugestaoRefatoracao", out JsonElement sugestaoElement) ||
                         root.TryGetProperty("sugestaoRefatoracao", out sugestaoElement))
                {
                    string sugestao = sugestaoElement.GetString() ?? "";
                    if (!string.IsNullOrEmpty(sugestao))
                    {
                        result.PropostaRefatoracao = new RefactoringProposal
                        {
                            Descricao = sugestao,
                            Justification = sugestao
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao processar proposta de refatoração: {ErrorMessage}", ex.Message);
            }
        }
        
        /// <summary>
        /// Extrai o conteúdo JSON da resposta em texto
        /// </summary>
        private string ExtractJsonFromText(string text)
        {
            // Padrão para encontrar JSON entre delimitadores de código
            string pattern = @"```(?:json)?\s*({[\s\S]*?})```";
            var match = Regex.Match(text, pattern);
            
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
            
            // Tentar encontrar JSON sem delimitadores
            pattern = @"({[\s\S]*})";
            match = Regex.Match(text, pattern);
            
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// Tenta corrigir problemas comuns em strings JSON
        /// </summary>
        private string TryFixJsonFormat(string jsonContent)
        {
            // Substituir aspas simples por aspas duplas
            jsonContent = Regex.Replace(jsonContent, @"(?<![\\])(')", "\"");
            
            // Remover quebras de linha extras
            jsonContent = Regex.Replace(jsonContent, @"\r\n", "\n");
            
            // Remover comentários no estilo JavaScript
            jsonContent = Regex.Replace(jsonContent, @"//.*?$", "", RegexOptions.Multiline);
            
            // Remover comentários de múltiplas linhas
            jsonContent = Regex.Replace(jsonContent, @"/\*[\s\S]*?\*/", "");
            
            // Corrigir vírgulas extras no final de objetos e arrays
            jsonContent = Regex.Replace(jsonContent, @",(\s*[\}\]])", "$1");
            
            // Adicionar vírgulas faltantes entre propriedades
            jsonContent = Regex.Replace(jsonContent, @"(""\s*:\s*[^,\{\[\}\]]+)\s*("")", "$1,$2");
            
            // Corrigir valores não citados
            jsonContent = Regex.Replace(jsonContent, @":\s*([a-zA-Z][a-zA-Z0-9_]*)\s*([,\}])", ": \"$1\"$2");
            
            return jsonContent;
        }
        
        /// <summary>
        /// Remove comentários de JSON que possam estar causando erros de parsing
        /// </summary>
        private string RemoveJsonComments(string jsonContent)
        {
            // Remover qualquer texto após uma barra em uma linha
            jsonContent = Regex.Replace(jsonContent, @"(""[^""]*"")|(/[^,\}\]]*[,\}\]])", m => 
                m.Groups[1].Success ? m.Groups[1].Value : m.Value.Substring(0, 1) + m.Value.Substring(m.Value.Length - 1));
            
            // Remover qualquer texto entre /* e */
            jsonContent = Regex.Replace(jsonContent, @"/\*[\s\S]*?\*/", "");
            
            return jsonContent;
        }
        
        /// <summary>
        /// Tenta extrair resultados diretamente do texto usando expressões regulares
        /// </summary>
        private CommitAnalysisResult ExtractResultsUsingRegex(string textResponse)
        {
            try
            {
                var result = new CommitAnalysisResult
                {
                    AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                    NotaFinal = 0,
                    ComentarioGeral = "Extraído via regex"
                };
                
                // Criar o critério CleanCode para armazenar os subcritérios
                var cleanCodeCriteria = new CriteriaAnalysis
                {
                    Nota = 0,
                    Comentario = "Análise de Clean Code baseada em múltiplos critérios."
                };
                
                // Padrões para extrair subcritérios
                var subcriteriaPairs = new Dictionary<string, string>
                {
                    { "nomenclaturaVariaveis", @"nomenclatura[\s_]*[vV]ari[aá]veis[\s""]*:?[\s""]*\{[^\}]*nota[\s""]*:?[\s""]*([0-9]+)[^\}]*\}" },
                    { "nomenclaturaMetodos", @"nomenclatura[\s_]*[mM][eé]todos[\s""]*:?[\s""]*\{[^\}]*nota[\s""]*:?[\s""]*([0-9]+)[^\}]*\}" },
                    { "tamanhoFuncoes", @"tamanho[\s_]*[fF]un[cç][õo]es[\s""]*:?[\s""]*\{[^\}]*nota[\s""]*:?[\s""]*([0-9]+)[^\}]*\}" },
                    { "comentarios", @"coment[aá]rios[\s""]*:?[\s""]*\{[^\}]*nota[\s""]*:?[\s""]*([0-9]+)[^\}]*\}" },
                    { "duplicacaoCodigo", @"duplica[cç][aã]o[\s_]*[cC][oó]digo[\s""]*:?[\s""]*\{[^\}]*nota[\s""]*:?[\s""]*([0-9]+)[^\}]*\}" }
                };
                
                double totalScore = 0;
                int criteriaCount = 0;
                
                foreach (var pair in subcriteriaPairs)
                {
                    var match = Regex.Match(textResponse, pair.Value, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        int nota = 0;
                        if (int.TryParse(match.Groups[1].Value, out nota))
                        {
                            // Extrair comentário se possível
                            string comentario = "";
                            var comentarioMatch = Regex.Match(match.Value, @"coment[aá]rio[\s""]*:?[\s""]*[""']?([^""'}\]]+)[""']?", RegexOptions.IgnoreCase);
                            if (comentarioMatch.Success && comentarioMatch.Groups.Count > 1)
                            {
                                comentario = comentarioMatch.Groups[1].Value.Trim();
                            }
                            else
                            {
                                comentario = "Extraído via regex";
                            }
                            
                            cleanCodeCriteria.Subcriteria[pair.Key] = new SubcriteriaAnalysis
                            {
                                Nota = nota,
                                Comentario = comentario
                            };
                            
                            totalScore += nota;
                            criteriaCount++;
                            
                            _logger.LogInformation("Subcritério extraído via regex: {Nome}, Nota: {Nota}", pair.Key, nota);
                        }
                    }
                }
                
                // Se encontrou pelo menos um subcritério, retornar o resultado
                if (criteriaCount > 0)
                {
                    // Calcular nota média para o critério CleanCode
                    cleanCodeCriteria.Nota = (int)Math.Round(totalScore / criteriaCount);
                    result.AnaliseGeral["CleanCode"] = cleanCodeCriteria;
                    result.NotaFinal = cleanCodeCriteria.Nota;
                    return result;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair resultados via regex: {ErrorMessage}", ex.Message);
                return null;
            }
        }
        
        /// <summary>
        /// Processa um objeto JSON que contém subcritérios
        /// </summary>
        private void ProcessSubcriteriosObject(JsonElement subcriteriasElement, CriteriaAnalysis cleanCodeCriteria)
        {
            if (subcriteriasElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var subcriterio in subcriteriasElement.EnumerateObject())
                {
                    string subcriteriaNome = NormalizeSubcriteriaName(subcriterio.Name);
                    int nota = 0;
                    string comentario = "";
                    
                    // Extrair nota e comentário
                    if (subcriterio.Value.TryGetProperty("nota", out JsonElement notaElement))
                    {
                        if (notaElement.ValueKind == JsonValueKind.Number)
                        {
                            nota = notaElement.GetInt32();
                        }
                        else if (notaElement.ValueKind == JsonValueKind.String)
                        {
                            string notaStr = notaElement.GetString() ?? "0";
                            int.TryParse(notaStr, out nota);
                        }
                    }
                    
                    if (subcriterio.Value.TryGetProperty("comentario", out JsonElement comentarioElement))
                    {
                        comentario = comentarioElement.GetString() ?? "";
                    }
                    
                    // Adicionar subcritério
                    cleanCodeCriteria.Subcriteria[subcriteriaNome] = new SubcriteriaAnalysis
                    {
                        Nota = nota,
                        Comentario = comentario
                    };
                    
                    _logger.LogInformation("Subcritério processado de objeto subcritérios: {Nome}, Nota: {Nota}", 
                        subcriteriaNome, nota);
                }
            }
        }
        
        /// <summary>
        /// Verifica se um nome de propriedade é um subcritério conhecido
        /// </summary>
        private bool IsKnownSubcriteria(string propertyName)
        {
            var normalizedName = propertyName.ToLowerInvariant();
            
            // Lista de possíveis nomes de subcritérios
            return normalizedName.Contains("variav") || 
                   normalizedName.Contains("metodo") || 
                   normalizedName.Contains("func") || 
                   normalizedName.Contains("coment") || 
                   normalizedName.Contains("duplic") ||
                   normalizedName.Contains("naming") ||
                   normalizedName.Contains("size") ||
                   normalizedName.Contains("variable") ||
                   normalizedName.Contains("method");
        }
        
        /// <summary>
        /// Normaliza o nome de um subcritério para um formato padrão
        /// </summary>
        private string NormalizeSubcriteriaName(string propertyName)
        {
            var normalizedName = propertyName.ToLowerInvariant();
            
            if (normalizedName.Contains("variav") || normalizedName.Contains("variable") || normalizedName.Contains("naming_var"))
                return "nomenclaturaVariaveis";
                
            if (normalizedName.Contains("metodo") || normalizedName.Contains("method") || normalizedName.Contains("naming_met"))
                return "nomenclaturaMetodos";
                
            if (normalizedName.Contains("func") || normalizedName.Contains("size") || normalizedName.Contains("function"))
                return "tamanhoFuncoes";
                
            if (normalizedName.Contains("coment") || normalizedName.Contains("comment"))
                return "comentarios";
                
            if (normalizedName.Contains("duplic") || normalizedName.Contains("duplication"))
                return "duplicacaoCodigo";
                
            // Se não corresponder a nenhum padrão conhecido, retornar o nome original
            return propertyName;
        }
        
        /// <summary>
        /// Garante que todos os subcritérios padrão estejam presentes
        /// </summary>
        private void EnsureDefaultSubcriteria(CriteriaAnalysis cleanCodeCriteria)
        {
            // Verificar se o dicionário de subcritérios está inicializado
            if (cleanCodeCriteria.Subcriteria == null)
            {
                cleanCodeCriteria.Subcriteria = new Dictionary<string, SubcriteriaAnalysis>();
            }
            
            // Lista de subcritérios padrão
            var defaultSubcriteria = new Dictionary<string, string>
            {
                { "nomenclaturaVariaveis", "Nomenclatura de variáveis" },
                { "nomenclaturaMetodos", "Nomenclatura de métodos" },
                { "tamanhoFuncoes", "Tamanho das funções" },
                { "comentarios", "Comentários" },
                { "duplicacaoCodigo", "Duplicação de código" }
            };
            
            // Adicionar subcritérios padrão se não existirem
            foreach (var subcriteria in defaultSubcriteria)
            {
                if (!cleanCodeCriteria.Subcriteria.ContainsKey(subcriteria.Key))
                {
                    _logger.LogWarning("Adicionando subcritério padrão ausente: {Nome}", subcriteria.Key);
                    cleanCodeCriteria.Subcriteria[subcriteria.Key] = new SubcriteriaAnalysis
                    {
                        Nota = 50, // Nota neutra
                        Comentario = $"Não foi possível analisar {subcriteria.Value}."
                    };
                }
            }
        }
        
        /// <summary>
        /// Calcula a nota final como média de todos os critérios
        /// </summary>
        private void CalculateOverallScore(CommitAnalysisResult result)
        {
            if (result.AnaliseGeral.Count == 0)
            {
                result.NotaFinal = 0;
                return;
            }
            
            double totalScore = 0;
            int criteriaCount = 0;
            
            foreach (var criteria in result.AnaliseGeral.Values)
            {
                if (criteria.Nota > 0)
                {
                    totalScore += criteria.Nota;
                    criteriaCount++;
                }
            }
            
            if (criteriaCount > 0)
            {
                result.NotaFinal = (int)Math.Round(totalScore / criteriaCount);
            }
            else
            {
                // Se não há critérios válidos, usar a nota do CleanCode ou 50 (neutra)
                result.NotaFinal = result.AnaliseGeral.ContainsKey("CleanCode") ? 
                    result.AnaliseGeral["CleanCode"].Nota : 50;
            }
            
            _logger.LogInformation("Nota final calculada: {NotaFinal}", result.NotaFinal);
        }
        
        /// <summary>
        /// Processa um critério geral (não subcritério)
        /// </summary>
        private void ProcessCriterio(JsonProperty property, CommitAnalysisResult result)
        {
            // Ignorar propriedades que não são critérios
            if (property.Name.Equals("comentarioGeral", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals("notaFinal", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals("propostaRefatoracao", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            
            // Extrair nota e comentário
            int nota = 0;
            string comentario = "";
            
            if (property.Value.TryGetProperty("nota", out JsonElement notaElement))
            {
                if (notaElement.ValueKind == JsonValueKind.Number)
                {
                    nota = notaElement.GetInt32();
                }
                else if (notaElement.ValueKind == JsonValueKind.String)
                {
                    string notaStr = notaElement.GetString() ?? "0";
                    int.TryParse(notaStr, out nota);
                }
            }
            
            if (property.Value.TryGetProperty("comentario", out JsonElement comentarioElement))
            {
                comentario = comentarioElement.GetString() ?? "";
            }
            
            // Adicionar critério
            result.AnaliseGeral[property.Name] = new CriteriaAnalysis
            {
                Nota = nota,
                Comentario = comentario,
                Subcriteria = new Dictionary<string, SubcriteriaAnalysis>()
            };
            
            _logger.LogInformation("Critério processado: {Nome}, Nota: {Nota}", property.Name, nota);
        }
        
        /// <summary>
        /// Cria um resultado de análise padrão para casos de erro
        /// </summary>
        private CommitAnalysisResult CreateFallbackAnalysisResult()
        {
            var result = new CommitAnalysisResult
            {
                AnaliseGeral = new Dictionary<string, CriteriaAnalysis>(),
                NotaFinal = 50, // Nota neutra
                ComentarioGeral = "Não foi possível analisar o código. Resultado padrão gerado."
            };
            
            // Adicionar critérios padrão
            var criterios = new[] { "CleanCode", "SOLID", "DesignPatterns", "Testabilidade", "Seguranca" };
            foreach (var criterio in criterios)
            {
                var criteriaAnalysis = new CriteriaAnalysis
                {
                    Nota = 50, // Nota neutra
                    Comentario = $"Não foi possível analisar o critério {criterio}."
                };
                
                // Adicionar subcritérios padrão para CleanCode
                if (criterio == "CleanCode")
                {
                    var subcriteria = new Dictionary<string, SubcriteriaAnalysis>
                    {
                        { "nomenclaturaVariaveis", new SubcriteriaAnalysis { Nota = 50, Comentario = "Não foi possível analisar a nomenclatura de variáveis." } },
                        { "nomenclaturaMetodos", new SubcriteriaAnalysis { Nota = 50, Comentario = "Não foi possível analisar a nomenclatura de métodos." } },
                        { "tamanhoFuncoes", new SubcriteriaAnalysis { Nota = 50, Comentario = "Não foi possível analisar o tamanho das funções." } },
                        { "comentarios", new SubcriteriaAnalysis { Nota = 50, Comentario = "Não foi possível analisar os comentários." } },
                        { "duplicacaoCodigo", new SubcriteriaAnalysis { Nota = 50, Comentario = "Não foi possível analisar a duplicação de código." } }
                    };
                    
                    criteriaAnalysis.Subcriteria = subcriteria;
                }
                
                result.AnaliseGeral[criterio] = criteriaAnalysis;
            }
            
            _logger.LogWarning("Criado resultado de análise padrão com subcritérios inicializados");
            
            return result;
        }
    }
}
