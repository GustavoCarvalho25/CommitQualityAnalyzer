using MongoDB.Bson.Serialization;
using RefactorScore.Domain.Entities;
using RefactorScore.Domain.ValueObjects;

namespace RefactorScore.Infrastructure.Mappers;

public static class MongoDbMapper
{
    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(CleanCodeRating)))
        {
            BsonClassMap.RegisterClassMap<CleanCodeRating>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(r => new CleanCodeRating(
                    r.VariableNaming,
                    r.FunctionSizes,
                    r.NoNeedsComments,
                    r.MethodCohesion,
                    r.DeadCode,
                    r.Justifies as Dictionary<string, string>
                ));
                
                cm.UnmapProperty(r => r.Note);
                cm.UnmapProperty(r => r.Quality);
            });
        }
    }
    
}