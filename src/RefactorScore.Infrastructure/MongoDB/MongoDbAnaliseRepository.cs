using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RefactorScore.Core.Entities;
using RefactorScore.Core.Interfaces;

namespace RefactorScore.Infrastructure.MongoDB
{
    /// <summary>
    /// Implementação do repositório de análises usando MongoDB
    /// </summary>
    public class MongoDbAnaliseRepository : IAnaliseRepository
    {
        private readonly IMongoCollection<AnaliseDeCommit> _analiseCommitCollection;
        private readonly IMongoCollection<AnaliseDeArquivo> _analiseArquivoCollection;
        private readonly IMongoCollection<Recomendacao> _recomendacoesCollection;
        private readonly ILogger<MongoDbAnaliseRepository> _logger;

        /// <summary>
        /// Construtor
        /// </summary>
        public MongoDbAnaliseRepository(
            IOptions<MongoDbOptions> options,
            ILogger<MongoDbAnaliseRepository> logger)
        {
            _logger = logger;
            
            try
            {
                var client = new MongoClient(options.Value.ConnectionString);
                var database = client.GetDatabase(options.Value.DatabaseName);
                
                _analiseCommitCollection = database.GetCollection<AnaliseDeCommit>(
                    options.Value.AnaliseCommitCollectionName);
                
                _analiseArquivoCollection = database.GetCollection<AnaliseDeArquivo>(
                    options.Value.AnaliseArquivoCollectionName);
                    
                _recomendacoesCollection = database.GetCollection<Recomendacao>(
                    options.Value.RecomendacoesCollectionName);
                
                _logger.LogInformation("🗄️ Repositório MongoDB inicializado com sucesso: {ConnectionString}, DB: {DatabaseName}",
                    options.Value.ConnectionString, options.Value.DatabaseName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao inicializar repositório MongoDB");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<AnaliseDeCommit> AdicionarAsync(AnaliseDeCommit entity)
        {
            try
            {
                _logger.LogInformation("📝 Adicionando análise de commit: {CommitId}", entity.IdCommit);
                
                // Verificar se já existe uma análise para este commit
                var analiseExistente = await ObterAnaliseRecentePorCommitAsync(entity.IdCommit);
                if (analiseExistente != null)
                {
                    _logger.LogWarning("⚠️ Já existe uma análise para o commit {CommitId}. Atualizando.", entity.IdCommit);
                    entity.Id = analiseExistente.Id; // Manter o mesmo ID
                    return await AtualizarAsync(entity);
                }
                
                // Garantir que o ID está definido
                if (string.IsNullOrEmpty(entity.Id))
                {
                    entity.Id = Guid.NewGuid().ToString();
                }
                
                // Definir a data da análise
                entity.DataDaAnalise = DateTime.UtcNow;
                
                // Salvar a análise do commit
                await _analiseCommitCollection.InsertOneAsync(entity);
                
                // Se houver análises de arquivos, salvar cada uma
                if (entity.AnalisesDeArquivos != null && entity.AnalisesDeArquivos.Count > 0)
                {
                    foreach (var analiseArquivo in entity.AnalisesDeArquivos)
                    {
                        analiseArquivo.IdCommit = entity.IdCommit;
                        await SalvarAnaliseArquivoAsync(analiseArquivo);
                    }
                }
                
                // Se houver recomendações, salvar cada uma
                if (entity.Recomendacoes != null && entity.Recomendacoes.Count > 0)
                {
                    foreach (var recomendacao in entity.Recomendacoes)
                    {
                        if (string.IsNullOrEmpty(recomendacao.Id))
                        {
                            recomendacao.Id = Guid.NewGuid().ToString();
                        }
                        
                        await _recomendacoesCollection.InsertOneAsync(recomendacao);
                    }
                }
                
                _logger.LogInformation("✅ Análise de commit adicionada com sucesso: {CommitId}", entity.IdCommit);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao adicionar análise de commit: {CommitId}", entity.IdCommit);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<AnaliseDeCommit> AtualizarAsync(AnaliseDeCommit entity)
        {
            try
            {
                _logger.LogInformation("🔄 Atualizando análise de commit: {CommitId}", entity.IdCommit);
                
                var filter = Builders<AnaliseDeCommit>.Filter.Eq(c => c.Id, entity.Id);
                
                // Atualizar a data da análise
                entity.DataDaAnalise = DateTime.UtcNow;
                
                await _analiseCommitCollection.ReplaceOneAsync(filter, entity);
                
                // Se houver análises de arquivos, atualizar cada uma
                if (entity.AnalisesDeArquivos != null && entity.AnalisesDeArquivos.Count > 0)
                {
                    foreach (var analiseArquivo in entity.AnalisesDeArquivos)
                    {
                        analiseArquivo.IdCommit = entity.IdCommit;
                        await SalvarAnaliseArquivoAsync(analiseArquivo);
                    }
                }
                
                _logger.LogInformation("✅ Análise de commit atualizada com sucesso: {CommitId}", entity.IdCommit);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao atualizar análise de commit: {CommitId}", entity.IdCommit);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<AnaliseDeCommit> ObterPorIdAsync(string id)
        {
            try
            {
                _logger.LogInformation("🔍 Buscando análise de commit por ID: {Id}", id);
                
                var filter = Builders<AnaliseDeCommit>.Filter.Eq(c => c.Id, id);
                var analise = await _analiseCommitCollection.Find(filter).FirstOrDefaultAsync();
                
                if (analise != null)
                {
                    // Carregar análises de arquivos relacionadas
                    analise.AnalisesDeArquivos = await ObterAnalisesArquivoPorCommitAsync(analise.IdCommit);
                    
                    _logger.LogInformation("✅ Análise de commit encontrada: {Id}", id);
                }
                else
                {
                    _logger.LogWarning("⚠️ Análise de commit não encontrada: {Id}", id);
                }
                
                return analise;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar análise de commit por ID: {Id}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<AnaliseDeCommit>> ObterTodosAsync()
        {
            try
            {
                _logger.LogInformation("🔍 Buscando todas as análises de commits");
                
                var analises = await _analiseCommitCollection.Find(_ => true).ToListAsync();
                
                _logger.LogInformation("✅ {Count} análises de commits encontradas", analises.Count);
                
                return analises;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar todas as análises de commits");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> RemoverAsync(string id)
        {
            try
            {
                _logger.LogInformation("🗑️ Removendo análise de commit: {Id}", id);
                
                // Primeiro, obter a análise para identificar o commit
                var analise = await ObterPorIdAsync(id);
                if (analise == null)
                {
                    _logger.LogWarning("⚠️ Análise de commit não encontrada para remoção: {Id}", id);
                    return false;
                }
                
                // Remover análises de arquivos relacionadas
                var filterArquivos = Builders<AnaliseDeArquivo>.Filter.Eq(a => a.IdCommit, analise.IdCommit);
                await _analiseArquivoCollection.DeleteManyAsync(filterArquivos);
                
                // Remover a análise do commit
                var filterCommit = Builders<AnaliseDeCommit>.Filter.Eq(c => c.Id, id);
                var resultado = await _analiseCommitCollection.DeleteOneAsync(filterCommit);
                
                bool sucesso = resultado.DeletedCount > 0;
                
                if (sucesso)
                {
                    _logger.LogInformation("✅ Análise de commit removida com sucesso: {Id}", id);
                }
                else
                {
                    _logger.LogWarning("⚠️ Falha ao remover análise de commit: {Id}", id);
                }
                
                return sucesso;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao remover análise de commit: {Id}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AnaliseDeCommit>> ObterAnalisesPorAutorAsync(string autor)
        {
            try
            {
                _logger.LogInformation("🔍 Buscando análises por autor: {Autor}", autor);
                
                var filter = Builders<AnaliseDeCommit>.Filter.Eq(c => c.Autor, autor);
                var analises = await _analiseCommitCollection.Find(filter).ToListAsync();
                
                _logger.LogInformation("✅ {Count} análises encontradas para o autor: {Autor}", analises.Count, autor);
                
                return analises;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar análises por autor: {Autor}", autor);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AnaliseDeCommit>> ObterAnalisesPorCommitAsync(string commitId)
        {
            try
            {
                _logger.LogInformation("🔍 Buscando análises para o commit: {CommitId}", commitId);
                
                var filter = Builders<AnaliseDeCommit>.Filter.Eq(c => c.IdCommit, commitId);
                var analises = await _analiseCommitCollection.Find(filter).ToListAsync();
                
                // Carregar análises de arquivos para cada análise
                foreach (var analise in analises)
                {
                    analise.AnalisesDeArquivos = await ObterAnalisesArquivoPorCommitAsync(commitId);
                }
                
                _logger.LogInformation("✅ {Count} análises encontradas para o commit: {CommitId}", analises.Count, commitId);
                
                return analises;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar análises por commit: {CommitId}", commitId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AnaliseDeCommit>> ObterAnalisesPorNotaMinimaAsync(double notaMinima)
        {
            try
            {
                _logger.LogInformation("🔍 Buscando análises com nota mínima: {NotaMinima}", notaMinima);
                
                var filter = Builders<AnaliseDeCommit>.Filter.Gte(c => c.NotaGeral, notaMinima);
                var analises = await _analiseCommitCollection.Find(filter).ToListAsync();
                
                _logger.LogInformation("✅ {Count} análises encontradas com nota mínima: {NotaMinima}", analises.Count, notaMinima);
                
                return analises;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar análises por nota mínima: {NotaMinima}", notaMinima);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AnaliseDeCommit>> ObterAnalisesPorPeriodoAsync(DateTime dataInicio, DateTime dataFim)
        {
            try
            {
                _logger.LogInformation("🔍 Buscando análises no período: {DataInicio} a {DataFim}", dataInicio, dataFim);
                
                var filter = Builders<AnaliseDeCommit>.Filter.And(
                    Builders<AnaliseDeCommit>.Filter.Gte(c => c.DataDaAnalise, dataInicio),
                    Builders<AnaliseDeCommit>.Filter.Lte(c => c.DataDaAnalise, dataFim)
                );
                
                var analises = await _analiseCommitCollection.Find(filter).ToListAsync();
                
                _logger.LogInformation("✅ {Count} análises encontradas no período", analises.Count);
                
                return analises;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar análises por período: {DataInicio} a {DataFim}", dataInicio, dataFim);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<AnaliseDeCommit> ObterAnaliseRecentePorCommitAsync(string commitId)
        {
            try
            {
                _logger.LogInformation("🔍 Buscando análise mais recente para o commit: {CommitId}", commitId);
                
                var filter = Builders<AnaliseDeCommit>.Filter.Eq(c => c.IdCommit, commitId);
                var analise = await _analiseCommitCollection
                    .Find(filter)
                    .SortByDescending(c => c.DataDaAnalise)
                    .FirstOrDefaultAsync();
                
                if (analise != null)
                {
                    // Carregar análises de arquivos
                    analise.AnalisesDeArquivos = await ObterAnalisesArquivoPorCommitAsync(commitId);
                    
                    _logger.LogInformation("✅ Análise recente encontrada para o commit: {CommitId}", commitId);
                }
                else
                {
                    _logger.LogInformation("⚠️ Nenhuma análise encontrada para o commit: {CommitId}", commitId);
                }
                
                return analise;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar análise recente por commit: {CommitId}", commitId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AnaliseDeArquivo>> ObterAnalisesArquivoPorCommitAsync(string commitId)
        {
            try
            {
                _logger.LogInformation("🔍 Buscando análises de arquivos para o commit: {CommitId}", commitId);
                
                var filter = Builders<AnaliseDeArquivo>.Filter.Eq(a => a.IdCommit, commitId);
                var analises = await _analiseArquivoCollection.Find(filter).ToListAsync();
                
                _logger.LogInformation("✅ {Count} análises de arquivos encontradas para o commit: {CommitId}", 
                    analises.Count, commitId);
                
                return analises;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar análises de arquivos por commit: {CommitId}", commitId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SalvarAnaliseArquivoAsync(AnaliseDeArquivo analiseArquivo)
        {
            try
            {
                _logger.LogInformation("📝 Salvando análise de arquivo: {CommitId}/{CaminhoArquivo}", 
                    analiseArquivo.IdCommit, analiseArquivo.CaminhoArquivo);
                
                // Verificar se já existe uma análise para este arquivo no commit
                var filter = Builders<AnaliseDeArquivo>.Filter.And(
                    Builders<AnaliseDeArquivo>.Filter.Eq(a => a.IdCommit, analiseArquivo.IdCommit),
                    Builders<AnaliseDeArquivo>.Filter.Eq(a => a.CaminhoArquivo, analiseArquivo.CaminhoArquivo)
                );
                
                var analiseExistente = await _analiseArquivoCollection.Find(filter).FirstOrDefaultAsync();
                
                if (analiseExistente != null)
                {
                    // Atualizar análise existente
                    analiseArquivo.Id = analiseExistente.Id;
                    analiseArquivo.DataAnalise = DateTime.UtcNow;
                    
                    var resultado = await _analiseArquivoCollection.ReplaceOneAsync(filter, analiseArquivo);
                    
                    _logger.LogInformation("✅ Análise de arquivo atualizada: {CommitId}/{CaminhoArquivo}", 
                        analiseArquivo.IdCommit, analiseArquivo.CaminhoArquivo);
                        
                    return resultado.ModifiedCount > 0;
                }
                else
                {
                    // Criar nova análise
                    if (string.IsNullOrEmpty(analiseArquivo.Id))
                    {
                        analiseArquivo.Id = Guid.NewGuid().ToString();
                    }
                    
                    analiseArquivo.DataAnalise = DateTime.UtcNow;
                    
                    await _analiseArquivoCollection.InsertOneAsync(analiseArquivo);
                    
                    _logger.LogInformation("✅ Nova análise de arquivo adicionada: {CommitId}/{CaminhoArquivo}", 
                        analiseArquivo.IdCommit, analiseArquivo.CaminhoArquivo);
                        
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao salvar análise de arquivo: {CommitId}/{CaminhoArquivo}", 
                    analiseArquivo.IdCommit, analiseArquivo.CaminhoArquivo);
                throw;
            }
        }
    }
} 