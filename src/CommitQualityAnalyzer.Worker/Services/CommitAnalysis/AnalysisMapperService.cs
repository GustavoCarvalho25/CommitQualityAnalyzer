using CommitQualityAnalyzer.Core.Models;
using CommitQualityAnalyzer.Worker.Services.CommitAnalysis.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis
{
    /// <summary>
    /// Serviço responsável por mapear entre os modelos de análise do Worker e do Core
    /// </summary>
    public class AnalysisMapperService
    {
        private readonly ILogger<AnalysisMapperService> _logger;

        public AnalysisMapperService(ILogger<AnalysisMapperService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Mapeia um CommitAnalysisResult para um CodeAnalysis
        /// </summary>
        public CodeAnalysis MapToCodeAnalysis(CommitAnalysisResult analysisResult, CommitInfo commit, string filePath)
        {
            _logger.LogInformation("Mapeando resultado de análise para CodeAnalysis");
            
            try
            {
                // Criar o objeto de análise de código para salvar no banco de dados
                var codeAnalysis = new CodeAnalysis
                {
                    CommitId = commit.Sha,
                    FilePath = filePath,
                    AuthorName = commit.AuthorName,
                    CommitDate = commit.AuthorDate.DateTime,
                    AnalysisDate = DateTime.UtcNow,
                    Analysis = new AnalysisResult
                    {
                        AnaliseGeral = new Dictionary<string, Core.Models.CriteriaAnalysis>(),
                        NotaFinal = analysisResult.NotaFinal,
                        ComentarioGeral = analysisResult.ComentarioGeral,
                        PropostaRefatoracao = MapRefactoringProposal(analysisResult.PropostaRefatoracao, commit.Sha, filePath)
                    }
                };
                
                // Converter as análises de critérios
                foreach (var criterio in analysisResult.AnaliseGeral)
                {
                    var criteriaMapeado = new Core.Models.CriteriaAnalysis
                    {
                        Nota = criterio.Value.Nota,
                        Comentario = criterio.Value.Comentario
                    };
                    
                    // Mapear subcritérios se existirem
                    if (criterio.Value.Subcriteria != null && criterio.Value.Subcriteria.Count > 0)
                    {
                        foreach (var subcriteria in criterio.Value.Subcriteria)
                        {
                            criteriaMapeado.Subcriteria[subcriteria.Key] = new Core.Models.SubcriteriaAnalysis
                            {
                                Nota = subcriteria.Value.Nota,
                                Comentario = subcriteria.Value.Comentario
                            };
                        }
                        
                        _logger.LogInformation("Mapeados {Count} subcritérios para o critério {Criterio}", 
                            criterio.Value.Subcriteria.Count, criterio.Key);
                    }
                    
                    codeAnalysis.Analysis.AnaliseGeral[criterio.Key] = criteriaMapeado;
                }
                
                return codeAnalysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao mapear resultado de análise: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Mapeia uma proposta de refatoração do Worker para o Core
        /// </summary>
        private Core.Models.RefactoringProposal MapRefactoringProposal(Models.RefactoringProposal source, string commitId, string filePath)
        {
            return new Core.Models.RefactoringProposal
            {
                CommitId = commitId,
                FilePath = filePath,
                ProposalDate = DateTime.UtcNow,
                Titulo = source.Titulo,
                Descricao = source.Descricao,
                CodigoOriginal = source.CodigoOriginal,
                CodigoRefatorado = source.CodigoRefatorado,
                OriginalCode = source.OriginalCode,
                ProposedCode = source.ProposedCode,
                Justification = source.Justification,
                Priority = source.Priority
            };
        }
    }
}
