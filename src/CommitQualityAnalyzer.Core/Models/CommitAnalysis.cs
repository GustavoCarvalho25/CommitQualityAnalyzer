namespace CommitQualityAnalyzer.Core.Models
{
    public class CommitAnalysis
    {
        public required string CommitId { get; set; }
        public required string AuthorName { get; set; }
        public DateTime CommitDate { get; set; }
        public double QualityScore { get; set; }
        public required string Analysis { get; set; }
        public Dictionary<string, double> Metrics { get; set; }
        public required string FilePath { get; set; }
        
        public CommitAnalysis()
        {
            Metrics = new Dictionary<string, double>();
        }
    }
}
