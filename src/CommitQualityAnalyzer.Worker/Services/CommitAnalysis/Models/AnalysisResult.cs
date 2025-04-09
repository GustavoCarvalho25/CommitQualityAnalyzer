using System;
using System.Collections.Generic;

namespace CommitQualityAnalyzer.Worker.Services.CommitAnalysis.Models
{
    /// <summary>
    /// Representa o resultado da análise de um arquivo em um commit
    /// </summary>
    public class CommitAnalysisResult
    {
        public CommitAnalysisResult()
        {
            AnaliseGeral = new Dictionary<string, CriteriaAnalysis>();
            PropostaRefatoracao = new RefactoringProposal();
        }

        /// <summary>
        /// Análises por critério (CleanCode, SOLID, etc.)
        /// </summary>
        public Dictionary<string, CriteriaAnalysis> AnaliseGeral { get; set; }
        
        /// <summary>
        /// Nota final da análise (média das notas dos critérios)
        /// </summary>
        public int NotaFinal { get; set; }
        
        /// <summary>
        /// Comentário geral sobre a análise
        /// </summary>
        public string ComentarioGeral { get; set; }
        
        /// <summary>
        /// Proposta de refatoração, se houver
        /// </summary>
        public RefactoringProposal PropostaRefatoracao { get; set; }
    }

    /// <summary>
    /// Representa a análise de um critério específico
    /// </summary>
    public class CriteriaAnalysis
    {
        /// <summary>
        /// Nota atribuída ao critério (0-100)
        /// </summary>
        public int Nota { get; set; }
        
        /// <summary>
        /// Comentário sobre o critério
        /// </summary>
        public string Comentario { get; set; }
    }

    /// <summary>
    /// Representa uma proposta de refatoração
    /// </summary>
    public class RefactoringProposal
    {
        /// <summary>
        /// Título da proposta de refatoração
        /// </summary>
        public string Titulo { get; set; } = string.Empty;
        
        /// <summary>
        /// Descrição detalhada da proposta
        /// </summary>
        public string Descricao { get; set; } = string.Empty;
        
        /// <summary>
        /// Código original antes da refatoração
        /// </summary>
        public string CodigoOriginal { get; set; } = string.Empty;
        
        /// <summary>
        /// Código refatorado proposto
        /// </summary>
        public string CodigoRefatorado { get; set; } = string.Empty;
        
        /// <summary>
        /// Código original em inglês (para compatibilidade)
        /// </summary>
        public string OriginalCode { get; set; } = string.Empty;
        
        /// <summary>
        /// Código proposto em inglês (para compatibilidade)
        /// </summary>
        public string ProposedCode { get; set; } = string.Empty;
        
        /// <summary>
        /// Justificativa para a refatoração
        /// </summary>
        public string Justification { get; set; } = string.Empty;
        
        /// <summary>
        /// Prioridade da refatoração (1-5, onde 1 é a mais alta)
        /// </summary>
        public int Priority { get; set; } = 3;
    }
}
