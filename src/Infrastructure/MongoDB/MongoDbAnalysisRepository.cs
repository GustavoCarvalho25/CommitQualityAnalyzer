using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RefactorScore.Core.Entities;
using RefactorScore.Core.Interfaces;
using RefactorScore.Core.Specifications;

namespace RefactorScore.Infrastructure.MongoDB
{
    public class MongoDbAnalysisRepository : IAnalysisRepository
    {
        private readonly IMongoCollection<CodeAnalysis> _analyses;
        private readonly ILogger<MongoDbAnalysisRepository> _logger;

        public MongoDbAnalysisRepository(
            IOptions<MongoDbOptions> options,
            ILogger<MongoDbAnalysisRepository> logger)
        {
            _logger = logger;
            
            try
            {
                var mongoClient = new MongoClient(options.Value.ConnectionString);
                var database = mongoClient.GetDatabase(options.Value.DatabaseName);
                _analyses = database.GetCollection<CodeAnalysis>(options.Value.CollectionName);
                
                CreateIndexes();
                
                _logger.LogInformation("Repositório MongoDB initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing MongoDB repository");
                throw;
            }
        }
        
        /// <summary>
        /// Cria índices para otimização de consultas
        /// </summary>
        private void CreateIndexes()
        {
            try
            {
                // Índice para consultas por CommitId
                var commitIdIndexModel = new CreateIndexModel<CodeAnalysis>(
                    Builders<CodeAnalysis>.IndexKeys.Ascending(a => a.CommitId),
                    new CreateIndexOptions { Background = true });
                
                // Índice para consultas por autor
                var authorIndexModel = new CreateIndexModel<CodeAnalysis>(
                    Builders<CodeAnalysis>.IndexKeys.Ascending(a => a.Author),
                    new CreateIndexOptions { Background = true });
                
                // Índice composto por data do commit (para ordenação) e nota geral (para filtros)
                var dateScoreIndexModel = new CreateIndexModel<CodeAnalysis>(
                    Builders<CodeAnalysis>.IndexKeys
                        .Descending(a => a.CommitDate)
                        .Ascending(a => a.OverallScore),
                    new CreateIndexOptions { Background = true });
                
                _analyses.Indexes.CreateMany(new[] { commitIdIndexModel, authorIndexModel, dateScoreIndexModel });
                
                _logger.LogInformation("Índices criados/atualizados com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating indexes in MongoDB");
                // Não lançamos a exceção para não interromper a inicialização
            }
        }

        /// <inheritdoc />
        public async Task<string> SaveAnalysisAsync(CodeAnalysis analysis)
        {
            try
            {
                _logger.LogInformation("Saving analysis for commit {CommitId}, file {FilePath}", 
                    analysis.CommitId, analysis.FilePath);
                
                await _analyses.InsertOneAsync(analysis);
                
                _logger.LogInformation("Analysis saved successfully, ID: {Id}", analysis.Id);
                
                return analysis.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving analysis");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CodeAnalysis> GetAnalysisByIdAsync(string id)
        {
            try
            {
                _logger.LogInformation("Searching analysis by ID: {Id}", id);
                
                var filter = Builders<CodeAnalysis>.Filter.Eq(a => a.Id, id);
                var analysis = await _analyses.Find(filter).FirstOrDefaultAsync();
                
                if (analysis == null)
                {
                    _logger.LogWarning("Analysis not found for ID: {Id}", id);
                }
                
                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching analysis by ID: {Id}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CodeAnalysis>> GetAnalysesByCommitIdAsync(string commitId)
        {
            try
            {
                _logger.LogInformation("Searching analyses for commit: {CommitId}", commitId);
                
                var filter = Builders<CodeAnalysis>.Filter.Eq(a => a.CommitId, commitId);
                var analyses = await _analyses.Find(filter).ToListAsync();
                
                _logger.LogInformation("Found {Count} analyses for commit {CommitId}", 
                    analyses.Count, commitId);
                
                return analyses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching analyses by commit: {CommitId}", commitId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CodeAnalysis> GetAnalysisByCommitAndFileAsync(string commitId, string filePath)
        {
            try
            {
                _logger.LogInformation("Searching analysis for commit {CommitId} and file {FilePath}", 
                    commitId, filePath);
                
                var filter = Builders<CodeAnalysis>.Filter.And(
                    Builders<CodeAnalysis>.Filter.Eq(a => a.CommitId, commitId),
                    Builders<CodeAnalysis>.Filter.Eq(a => a.FilePath, filePath)
                );
                
                var analysis = await _analyses.Find(filter).FirstOrDefaultAsync();
                
                if (analysis == null)
                {
                    _logger.LogWarning("Analysis not found for commit {CommitId} and file {FilePath}", 
                        commitId, filePath);
                }
                
                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching analysis by commit and file");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CodeAnalysis>> GetAnalysesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation("Searching analyses in the period from {StartDate} to {EndDate}", 
                    startDate, endDate);
                
                var filter = Builders<CodeAnalysis>.Filter.And(
                    Builders<CodeAnalysis>.Filter.Gte(a => a.CommitDate, startDate),
                    Builders<CodeAnalysis>.Filter.Lte(a => a.CommitDate, endDate)
                );
                
                var analyses = await _analyses.Find(filter)
                    .Sort(Builders<CodeAnalysis>.Sort.Descending(a => a.CommitDate))
                    .ToListAsync();
                
                _logger.LogInformation("Found {Count} analyses in the specified period", analyses.Count);
                
                return analyses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching analyses by period");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CodeAnalysis>> GetAnalysesWithScoreLessThanAsync(double scoreThreshold, int limit = 100)
        {
            try
            {
                _logger.LogInformation("Searching analyses with score less than {ScoreThreshold}, limit: {Limit}", 
                    scoreThreshold, limit);
                
                var filter = Builders<CodeAnalysis>.Filter.Lt(a => a.OverallScore, scoreThreshold);
                
                var analyses = await _analyses.Find(filter)
                    .Sort(Builders<CodeAnalysis>.Sort.Ascending(a => a.OverallScore))
                    .Limit(limit)
                    .ToListAsync();
                
                _logger.LogInformation("Found {Count} analyses with score less than {ScoreThreshold}", 
                    analyses.Count, scoreThreshold);
                
                return analyses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching analyses by score");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CodeAnalysis>> GetLatestAnalysesAsync(int limit = 100)
        {
            try
            {
                _logger.LogInformation("Searching the {Limit} most recent analyses", limit);
                
                var analyses = await _analyses.Find(_ => true)
                    .Sort(Builders<CodeAnalysis>.Sort.Descending(a => a.AnalysisDate))
                    .Limit(limit)
                    .ToListAsync();
                
                _logger.LogInformation("Found {Count} recent analyses", analyses.Count);
                
                return analyses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching recent analyses");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAnalysisAsync(CodeAnalysis analysis)
        {
            try
            {
                _logger.LogInformation("Updating analysis with ID: {Id}", analysis.Id);
                
                var filter = Builders<CodeAnalysis>.Filter.Eq(a => a.Id, analysis.Id);
                var result = await _analyses.ReplaceOneAsync(filter, analysis);
                
                var success = result.ModifiedCount > 0;
                
                _logger.LogInformation("Analysis updated: {Success}, ID: {Id}", success, analysis.Id);
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating analysis, ID: {Id}", analysis.Id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAnalysisAsync(string id)
        {
            try
            {
                _logger.LogInformation("Deleting analysis with ID: {Id}", id);
                
                var filter = Builders<CodeAnalysis>.Filter.Eq(a => a.Id, id);
                var result = await _analyses.DeleteOneAsync(filter);
                
                var success = result.DeletedCount > 0;
                
                _logger.LogInformation("Analysis deleted: {Success}, ID: {Id}", success, id);
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting analysis, ID: {Id}", id);
                throw;
            }
        }
    }

    /// <summary>
    /// Opções de configuração para o MongoDB
    /// </summary>
    public class MongoDbOptions
    {
        /// <summary>
        /// String de conexão com o MongoDB
        /// </summary>
        public string ConnectionString { get; set; } = "mongodb://admin:admin123@localhost:27017";
        
        /// <summary>
        /// Nome do banco de dados
        /// </summary>
        public string DatabaseName { get; set; } = "RefactorScore";
        
        /// <summary>
        /// Nome da coleção para armazenar análises
        /// </summary>
        public string CollectionName { get; set; } = "CodeAnalyses";
    }
} 