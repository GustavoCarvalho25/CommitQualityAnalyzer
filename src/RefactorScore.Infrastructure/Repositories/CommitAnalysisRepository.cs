using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RefactorScore.Domain.Entities;
using RefactorScore.Domain.Repositories;

namespace RefactorScore.Infrastructure.Repositories;

public class CommitAnalysisRepository : ICommitAnalysisRepository
{
    private readonly IMongoCollection<CommitAnalysis> _collection;
    private readonly ILogger<CommitAnalysisRepository> _logger;

    public CommitAnalysisRepository(IMongoDatabase database, ILogger<CommitAnalysisRepository> logger)
    {
        _collection = database.GetCollection<CommitAnalysis>("CommitAnalysis");
        _logger = logger;
    }

    public async Task<CommitAnalysis?> GetByIdAsync(string id)
    {
        try
        {
            return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting commit analysis by id: {Id}", id);
            throw;
        }
    }

    public async Task<CommitAnalysis?> GetByCommitIdAsync(string commitId)
    {
        try
        {
            return await _collection.Find(x => x.CommitId == commitId).FirstOrDefaultAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting commit analysis by commit id: {CommitId}", commitId);
            throw;
        }
    }

    public async Task<List<CommitAnalysis>> GetAllAsync()
    {
        try
        {
            return await _collection.Find(_ => true).ToListAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting all commit analyses");
            throw;
        }
    }

    public async Task AddAsync(CommitAnalysis commitAnalysis)
    {
        try
        {
            await _collection.InsertOneAsync(commitAnalysis);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public Task UpdateAsync(CommitAnalysis commitAnalysis)
    {
        try
        {
            return _collection.ReplaceOneAsync(x => x.Id == commitAnalysis.Id, commitAnalysis);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error updating commit analysis: {CommitAnalysis}", commitAnalysis);
            throw;
        }
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _collection.DeleteOneAsync(x => x.Id == id);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error deleting commit analysis: {Id}", id);
            throw;
        }
    }
}