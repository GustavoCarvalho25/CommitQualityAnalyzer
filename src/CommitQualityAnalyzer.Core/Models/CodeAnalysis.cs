using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace CommitQualityAnalyzer.Core.Models
{
    public class CodeAnalysis
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public required string CommitId { get; set; }
        public required string FilePath { get; set; }
        public required string AuthorName { get; set; }
        public DateTime CommitDate { get; set; }
        public DateTime AnalysisDate { get; set; }
        public required AnalysisResult Analysis { get; set; }
        public List<RefactoringProposal> RefactoringProposals { get; set; } = new List<RefactoringProposal>();
        public string? OriginalCode { get; set; }
        public string? ModifiedCode { get; set; }
    }

    public class AnalysisResult
    {
        [JsonPropertyName("analiseGeral")]
        public required Dictionary<string, CriteriaAnalysis> AnaliseGeral { get; set; }

        [JsonPropertyName("notaFinal")]
        public double NotaFinal { get; set; }

        [JsonPropertyName("comentarioGeral")]
        public required string ComentarioGeral { get; set; }
    }

    public class CriteriaAnalysis
    {
        [JsonPropertyName("nota")]
        public int Nota { get; set; }

        [JsonPropertyName("comentario")]
        public required string Comentario { get; set; }
    }

    public class RefactoringProposal
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        
        public required string Description { get; set; }
        public required string OriginalCodeSnippet { get; set; }
        public required string RefactoredCodeSnippet { get; set; }
        public required string Category { get; set; }
        public int LineStart { get; set; }
        public int LineEnd { get; set; }
        public double ImprovementScore { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RefactoringResult
    {
        [JsonPropertyName("sugestoesRefatoracao")]
        public required List<RefactoringSuggestion> SugestoesRefatoracao { get; set; }
    }

    public class RefactoringSuggestion
    {
        [JsonPropertyName("descricao")]
        public required string Descricao { get; set; }

        [JsonPropertyName("codigoOriginal")]
        public required string CodigoOriginal { get; set; }

        [JsonPropertyName("codigoRefatorado")]
        public required string CodigoRefatorado { get; set; }

        [JsonPropertyName("categoria")]
        public required string Categoria { get; set; }

        [JsonPropertyName("linhaInicio")]
        public int LinhaInicio { get; set; }

        [JsonPropertyName("linhaFim")]
        public int LinhaFim { get; set; }

        [JsonPropertyName("pontuacaoMelhoria")]
        public double PontuacaoMelhoria { get; set; }
    }
}
