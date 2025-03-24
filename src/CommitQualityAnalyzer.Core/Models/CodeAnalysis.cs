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
}
