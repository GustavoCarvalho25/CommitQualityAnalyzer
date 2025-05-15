using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RefactorScore.Core.Interfaces;
using StackExchange.Redis;

namespace RefactorScore.Infrastructure.RedisCache
{
    public class RedisCacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly RedisCacheOptions _options;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisCacheService(
            IConnectionMultiplexer redis,
            IOptions<RedisCacheOptions> options,
            ILogger<RedisCacheService> logger)
        {
            _redis = redis;
            _options = options.Value;
            _logger = logger;
            _database = _redis.GetDatabase(_options.DatabaseId);
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        /// <inheritdoc />
        public async Task<T> GetAsync<T>(string key)
        {
            try
            {
                var redisKey = FormatKey(key);
                var value = await _database.StringGetAsync(redisKey);
                
                if (value.IsNullOrEmpty)
                {
                    _logger.LogDebug("Chave não encontrada no cache: {Key}", key);
                    return default;
                }
                
                _logger.LogDebug("Item recuperado do cache: {Key}", key);
                return JsonSerializer.Deserialize<T>(value, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter item do cache: {Key}", key);
                return default;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                var redisKey = FormatKey(key);
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                
                var expiryTime = expiry ?? TimeSpan.FromHours(_options.DefaultExpiryHours);
                
                var result = await _database.StringSetAsync(redisKey, serializedValue, expiryTime);
                
                _logger.LogDebug("Item armazenado no cache: {Key}, Expiração: {Expiry}", key, expiryTime);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao armazenar item no cache: {Key}", key);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(string key)
        {
            try
            {
                var redisKey = FormatKey(key);
                var result = await _database.KeyDeleteAsync(redisKey);
                
                _logger.LogDebug("Item removido do cache: {Key}, Sucesso: {Success}", key, result);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover item do cache: {Key}", key);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var redisKey = FormatKey(key);
                var result = await _database.KeyExistsAsync(redisKey);
                
                _logger.LogDebug("Verificação de existência de chave: {Key}, Existe: {Exists}", key, result);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar existência de chave: {Key}", key);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<string[]> GetKeysByPatternAsync(string pattern)
        {
            try
            {
                var fullPattern = $"{_options.KeyPrefix}:{pattern}";
                var keys = new List<string>();
                
                var endPoints = _redis.GetEndPoints();
                
                foreach (var endpoint in endPoints)
                {
                    var server = _redis.GetServer(endpoint);
                    var redisKeys = server.Keys(pattern: fullPattern);
                    
                    foreach (var redisKey in redisKeys)
                    {
                        // Remove o prefixo para retornar apenas a chave original
                        var originalKey = redisKey.ToString();
                        if (!string.IsNullOrEmpty(_options.KeyPrefix))
                        {
                            originalKey = originalKey.Substring(_options.KeyPrefix.Length + 1); // +1 para o separador ":"
                        }
                        
                        keys.Add(originalKey);
                    }
                }
                
                _logger.LogDebug("Encontradas {KeyCount} chaves com o padrão: {Pattern}", keys.Count, pattern);
                
                return keys.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar chaves com o padrão: {Pattern}", pattern);
                return Array.Empty<string>();
            }
        }

        /// <inheritdoc />
        public async Task<long> IncrementAsync(string key, long value = 1)
        {
            try
            {
                var redisKey = FormatKey(key);
                var result = await _database.StringIncrementAsync(redisKey, value);
                
                _logger.LogDebug("Contador incrementado: {Key}, Incremento: {Value}, Novo valor: {NewValue}", key, value, result);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao incrementar contador: {Key}, Incremento: {Value}", key, value);
                return -1;
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                _logger.LogInformation("[REDIS_CHECK] Checking Redis availability...");
                var startTime = DateTime.UtcNow;
                
                // Simply ping the server to check availability
                var isAvailable = await _database.PingAsync() != null;
                
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("[REDIS_CHECK] Redis availability: {IsAvailable} (response time: {Duration}ms)",
                    isAvailable, duration.TotalMilliseconds);
                
                return isAvailable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[REDIS_CHECK] Error checking Redis availability");
                return false;
            }
        }

        private string FormatKey(string key)
        {
            return string.IsNullOrEmpty(_options.KeyPrefix) ? key : $"{_options.KeyPrefix}:{key}";
        }
    }

    public class RedisCacheOptions
    {
        public string ConnectionString { get; set; } = "localhost:6379";
        public string KeyPrefix { get; set; } = "refactorscore";
        public int DatabaseId { get; set; } = 0;
        public int DefaultExpiryHours { get; set; } = 24;
    }
} 