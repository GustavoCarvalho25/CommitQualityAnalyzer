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
                
                cm.UnmapProperty(r => r.Note);
                cm.UnmapProperty(r => r.Quality);
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(CommitAnalysis)))
        {
            BsonClassMap.RegisterClassMap<CommitAnalysis>(cm =>
            {
                cm.AutoMap();

                cm.MapField("_files").SetElementName("Files");
                cm.MapField("_suggestions").SetElementName("Suggestions");

                cm.UnmapProperty(a => a.OverallNote);
            });
        }
        
        if (!BsonClassMap.IsClassMapRegistered(typeof(CommitFile)))
        {
            BsonClassMap.RegisterClassMap<CommitFile>(cm =>
            {
                cm.AutoMap();
                
                cm.MapField("_suggestions").SetElementName("Suggestions");
                
                cm.UnmapProperty(f => f.HasAnalysis);
            });
        }
    }
}