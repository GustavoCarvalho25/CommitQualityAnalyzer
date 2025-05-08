using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace CommitQualityAnalyzer.Core.Models
{
    public class CodeAnalysis
    {
        [BsonId]
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
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
        
        [JsonPropertyName("propostaRefatoracao")]
        public RefactoringProposal PropostaRefatoracao { get; set; } = new RefactoringProposal();
    }

    public class CriteriaAnalysis
    {
        [JsonPropertyName("nota")]
        public int Nota { get; set; }

        [JsonPropertyName("comentario")]
        public required string Comentario { get; set; }
        
        [JsonPropertyName("subcriteria")]
        public Dictionary<string, SubcriteriaAnalysis> Subcriteria { get; set; } = new Dictionary<string, SubcriteriaAnalysis>();
    }
    
    public class SubcriteriaAnalysis
    {
        [JsonPropertyName("nota")]
        public int Nota { get; set; }

        [JsonPropertyName("comentario")]
        public string Comentario { get; set; } = "";
    }

    public class RefactoringProposal
    {
        [BsonId]
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
        public string? Id { get; set; }

        // Campos obrigatórios apenas para propostas salvas no banco de dados
        [JsonIgnore]
        public string? CommitId { get; set; }
        
        [JsonIgnore]
        public string? FilePath { get; set; }
        
        [JsonIgnore]
        public DateTime ProposalDate { get; set; }
        
        // Campos do novo formato de proposta de refatoração
        [JsonPropertyName("titulo")]
        public string Titulo { get; set; } = "";
        
        [JsonPropertyName("descricao")]
        public string Descricao { get; set; } = "";
        
        [JsonPropertyName("codigoOriginal")]
        public string CodigoOriginal { get; set; } = "";
        
        [JsonPropertyName("codigoRefatorado")]
        public string CodigoRefatorado { get; set; } = "";
        
        // Mapeamento para compatibilidade com o formato antigo
        [JsonIgnore]
        public string OriginalCode { 
            get => CodigoOriginal; 
            set => CodigoOriginal = value; 
        }
        
        [JsonIgnore]
        public string ProposedCode { 
            get => CodigoRefatorado; 
            set => CodigoRefatorado = value; 
        }
        
        [JsonIgnore]
        public string Justification { 
            get => Descricao; 
            set => Descricao = value; 
        }
        
        [JsonIgnore]
        public int Priority { get; set; } = 3; // Valor padrão médio
        
        public RefactoringProposal() {}
        
        // Método para converter para o formato antigo para salvar no banco
        public RefactoringProposal ToLegacyFormat(string commitId, string filePath)
        {
            return new RefactoringProposal
            {
                CommitId = commitId,
                FilePath = filePath,
                ProposalDate = DateTime.Now,
                OriginalCode = CodigoOriginal,
                ProposedCode = CodigoRefatorado,
                Justification = Descricao,
                Priority = 3, // Prioridade média por padrão
                Titulo = Titulo
            };
        }
    }
}
