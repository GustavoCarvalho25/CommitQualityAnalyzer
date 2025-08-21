using RefactorScore.Domain.Enum;
using RefactorScore.Domain.SeedWork;

namespace RefactorScore.Domain.ValueObjects;

public class CleanCodeRating : ValueObject
{
    public int VariableNaming { get; private set; }
    public int FunctionSizes  { get; private set; }
    public int NoNeedsComments { get; private set; }
    public int MethodCohesion { get; private set; }
    public int DeadCode { get; private set; }

    public double Note => CalculateNote();
    
    public Dictionary<string, string> Justifies { get; private set; }
    
    private double CalculateNote()
        => (VariableNaming + FunctionSizes + NoNeedsComments + MethodCohesion + DeadCode) / 5.0;
    
    public string Quality
    {
        get
        {
            return Note switch
            {
                >= 9.0 => RatingQuality.Excellent.ToString(),
                >= 7.5 => RatingQuality.VeryGood.ToString(),
                >= 6.0 => RatingQuality.Good.ToString(),
                >= 5.0 => RatingQuality.Acceptable.ToString(),
                >= 3.5 => RatingQuality.NeedsImprovement.ToString(),
                _ => RatingQuality.Problematic.ToString()
            };
        }
    }
    
    public CleanCodeRating(int variableNaming, int functionSizes, int noNeedsComments, int methodCohesion, int deadCode, Dictionary<string, string> justifies = null)
    {
        VariableNaming = variableNaming;
        FunctionSizes = functionSizes;
        NoNeedsComments = noNeedsComments;
        MethodCohesion = methodCohesion;
        DeadCode = deadCode;
        Justifies = (justifies ?? new Dictionary<string, string>());
    }

    private CleanCodeRating()
    {
        Justifies = new Dictionary<string, string>();
    }
    
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return VariableNaming;
        yield return FunctionSizes;
        yield return NoNeedsComments;
        yield return MethodCohesion;
        yield return DeadCode;
        yield return Justifies;
    }
}