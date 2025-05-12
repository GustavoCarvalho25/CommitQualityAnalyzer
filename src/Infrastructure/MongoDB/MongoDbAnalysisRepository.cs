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
                
                _logger.LogInformation("Repositório MongoDB inicializado com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao inicializar repositório MongoDB");
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
                _logger.LogError(ex, "Erro ao criar índices no MongoDB");
                // Não lançamos a exceção para não interromper a inicialização
            }
        }

        /// <inheritdoc />
        public async Task<string> SaveAnalysisAsync(CodeAnalysis analysis)
        {
            try
            {
                _logger.LogInformation("Salvando análise para o commit {CommitId}, arquivo {FilePath}", 
                    analysis.CommitId, analysis.FilePath);
                
                await _analyses.InsertOneAsync(analysis);
                
                _logger.LogInformation("Análise salva com sucesso, ID: {Id}", analysis.Id);
                
                return analysis.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar análise");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CodeAnalysis> GetAnalysisByIdAsync(string id)
        {
            try
            {
                _logger.LogInformation("Buscando análise por ID: {Id}", id);
                
                var filter = Builders<CodeAnalysis>.Filter.Eq(a => a.Id, id);
                var analysis = await _analyses.Find(filter).FirstOrDefaultAsync();
                
                if (analysis == null)
                {
                    _logger.LogWarning("Análise não encontrada para o ID: {Id}", id);
                }
                
                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar análise por ID: {Id}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CodeAnalysis>> GetAnalysesByCommitIdAsync(string commitId)
        {
            try
            {
                _logger.LogInformation("Buscando análises para o commit: {CommitId}", commitId);
                
                var filter = Builders<CodeAnalysis>.Filter.Eq(a => a.CommitId, commitId);
                var analyses = await _analyses.Find(filter).ToListAsync();
                
                _logger.LogInformation("Encontradas {Count} análises para o commit {CommitId}", 
                    analyses.Count, commitId);
                
                return analyses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar análises por commit: {CommitId}", commitId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CodeAnalysis> GetAnalysisByCommitAndFileAsync(string commitId, string filePath)
        {
            try
            {
                _logger.LogInformation("Buscando análise para o commit {CommitId} e arquivo {FilePath}", 
                    commitId, filePath);
                
                var filter = Builders<CodeAnalysis>.Filter.And(
                    Builders<CodeAnalysis>.Filter.Eq(a => a.CommitId, commitId),
                    Builders<CodeAnalysis>.Filter.Eq(a => a.FilePath, filePath)
                );
                
                var analysis = await _analyses.Find(filter).FirstOrDefaultAsync();
                
                if (analysis == null)
                {
                    _logger.LogWarning("Análise não encontrada para o commit {CommitId} e arquivo {FilePath}", 
                        commitId, filePath);
                }
                
                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar análise por commit e arquivo");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CodeAnalysis>> GetAnalysesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation("Buscando análises no período de {StartDate} a {EndDate}", 
                    startDate, endDate);
                
                var filter = Builders<CodeAnalysis>.Filter.And(
                    Builders<CodeAnalysis>.Filter.Gte(a => a.CommitDate, startDate),
                    Builders<CodeAnalysis>.Filter.Lte(a => a.CommitDate, endDate)
                );
                
                var analyses = await _analyses.Find(filter)
                    .Sort(Builders<CodeAnalysis>.Sort.Descending(a => a.CommitDate))
                    .ToListAsync();
                
                _logger.LogInformation("Encontradas {Count} análises no período especificado", analyses.Count);
                
                return analyses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar análises por período");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CodeAnalysis>> GetAnalysesWithScoreLessThanAsync(double scoreThreshold, int limit = 100)
        {
            try
            {
                _logger.LogInformation("Buscando análises com nota menor que {ScoreThreshold}, limite: {Limit}", 
                    scoreThreshold, limit);
                
                var filter = Builders<CodeAnalysis>.Filter.Lt(a => a.OverallScore, scoreThreshold);
                
                var analyses = await _analyses.Find(filter)
                    .Sort(Builders<CodeAnalysis>.Sort.Ascending(a => a.OverallScore))
                    .Limit(limit)
                    .ToListAsync();
                
                _logger.LogInformation("Encontradas {Count} análises com nota menor que {ScoreThreshold}", 
                    analyses.Count, scoreThreshold);
                
                return analyses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar análises por nota");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<CodeAnalysis>> GetLatestAnalysesAsync(int limit = 100)
        {
            try
            {
                _logger.LogInformation("Buscando as {Limit} análises mais recentes", limit);
                
                var analyses = await _analyses.Find(_ => true)
                    .Sort(Builders<CodeAnalysis>.Sort.Descending(a => a.AnalysisDate))
                    .Limit(limit)
                    .ToListAsync();
                
                _logger.LogInformation("Encontradas {Count} análises recentes", analyses.Count);
                
                return analyses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar análises recentes");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAnalysisAsync(CodeAnalysis analysis)
        {
            try
            {
                _logger.LogInformation("Atualizando análise com ID: {Id}", analysis.Id);
                
                var filter = Builders<CodeAnalysis>.Filter.Eq(a => a.Id, analysis.Id);
                var result = await _analyses.ReplaceOneAsync(filter, analysis);
                
                var success = result.ModifiedCount > 0;
                
                _logger.LogInformation("Análise atualizada: {Success}, ID: {Id}", success, analysis.Id);
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar análise, ID: {Id}", analysis.Id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAnalysisAsync(string id)
        {
            try
            {
                _logger.LogInformation("Excluindo análise com ID: {Id}", id);
                
                var filter = Builders<CodeAnalysis>.Filter.Eq(a => a.Id, id);
                var result = await _analyses.DeleteOneAsync(filter);
                
                var success = result.DeletedCount > 0;
                
                _logger.LogInformation("Análise excluída: {Success}, ID: {Id}", success, id);
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao excluir análise, ID: {Id}", id);
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