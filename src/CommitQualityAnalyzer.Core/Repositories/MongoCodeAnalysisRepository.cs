using CommitQualityAnalyzer.Core.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace CommitQualityAnalyzer.Core.Repositories
{
    public class MongoCodeAnalysisRepository : ICodeAnalysisRepository
    {
        private readonly IMongoCollection<CodeAnalysis> _collection;

        public MongoCodeAnalysisRepository(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MongoDB");
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase("CommitQualityAnalyzer");
            _collection = database.GetCollection<CodeAnalysis>("CodeAnalyses");

            CreateIndexes();
        }

        private void CreateIndexes()
        {
            var indexKeysDefinition = Builders<CodeAnalysis>.IndexKeys
                .Ascending(x => x.CommitId)
                .Ascending(x => x.FilePath);

            var indexOptions = new CreateIndexOptions { Name = "CommitId_FilePath" };
            _collection.Indexes.CreateOne(new CreateIndexModel<CodeAnalysis>(indexKeysDefinition, indexOptions));
        }

        public async Task SaveAnalysisAsync(CodeAnalysis analysis)
        {
            await _collection.InsertOneAsync(analysis);
        }

        public async Task<IEnumerable<CodeAnalysis>> GetAnalysesByCommitIdAsync(string commitId)
        {
            return await _collection.Find(x => x.CommitId == commitId).ToListAsync();
        }

        public async Task<IEnumerable<CodeAnalysis>> GetAnalysesByDateRangeAsync(DateTime start, DateTime end)
        {
            var filter = Builders<CodeAnalysis>.Filter.And(
                Builders<CodeAnalysis>.Filter.Gte(x => x.CommitDate, start),
                Builders<CodeAnalysis>.Filter.Lte(x => x.CommitDate, end)
            );

            return await _collection.Find(filter).ToListAsync();
        }
        
        public async Task<CodeAnalysis> GetAnalysisByCommitAndFileAsync(string commitId, string filePath)
        {
            var filter = Builders<CodeAnalysis>.Filter.And(
                Builders<CodeAnalysis>.Filter.Eq(x => x.CommitId, commitId),
                Builders<CodeAnalysis>.Filter.Eq(x => x.FilePath, filePath)
            );

            return await _collection.Find(filter).FirstOrDefaultAsync();
        }
    }
}
