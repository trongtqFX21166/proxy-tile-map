using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Text.Json;
using VietmapLive.TitleMap.Api.Models;

namespace VietmapLive.TitleMap.Api.Providers
{
    public interface ICombinedConfigProvider
    {
        Task<TilemapConfig?> GetConfigAsync(string configId);
        Task<bool> UpdateConfigAsync(TilemapConfig config);
        Task<bool> LoadAllConfigsAsync();
        Task<bool> ClearCacheAsync(string configId);
        Task<bool> ClearAllCacheAsync();
    }

    public class CombinedConfigProvider : ICombinedConfigProvider
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CombinedConfigProvider> _logger;
        private readonly TimeSpan _cacheExpiration;
        private readonly string _redisKeyPrefix;
        private const string CacheKeyPrefix = "MapboxConfig_";

        public CombinedConfigProvider(
            IConnectionMultiplexer redis,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<CombinedConfigProvider> logger)
        {
            _redis = redis;
            _cache = cache;
            _logger = logger;

            // Get cache expiration from configuration, default to 10 minutes
            int cacheExpirationMinutes = configuration.GetValue<int>("Redis:CacheExpirationMinutes", 10);
            _cacheExpiration = TimeSpan.FromMinutes(cacheExpirationMinutes);

            // Get Redis key prefix from configuration
            var prefixKey = configuration["Redis:PrefixKey"];
            var instanceName = configuration.GetValue<string>("Redis:InstanceName", "VietmapLive.Tilemap:");
            _redisKeyPrefix = string.IsNullOrEmpty(prefixKey)
                ? instanceName + "MapboxConfig:"
                : prefixKey + ":" + instanceName + "MapboxConfig:";
        }

        public async Task<TilemapConfig?> GetConfigAsync(string configId)
        {
            var cacheKey = $"{CacheKeyPrefix}{configId}";

            // Try to get from memory cache first
            if (_cache.TryGetValue(cacheKey, out TilemapConfig? config))
            {
                return config;
            }

            // If not in memory cache, try to get from Redis
            try
            {
                var db = _redis.GetDatabase();
                var redisKey = $"{_redisKeyPrefix}{configId}";
                var json = await db.StringGetAsync(redisKey);

                if (!json.IsNullOrEmpty)
                {
                    try
                    {
                        config = JsonSerializer.Deserialize<TilemapConfig>(json.ToString());

                        if (config != null)
                        {
                            // Store in memory cache for faster access next time
                            _cache.Set(cacheKey, config, _cacheExpiration);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, $"Error deserializing config from Redis for ID: {configId}");
                    }
                }
                else
                {
                    _logger.LogWarning($"No configuration found in Redis for ID: {configId}");
                }
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection failed when retrieving config. Falling back to memory cache only.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error accessing Redis for config ID: {configId}");
            }

            return config;
        }

        public async Task<bool> UpdateConfigAsync(TilemapConfig config)
        {
            try
            {
                if (string.IsNullOrEmpty(config.Id))
                {
                    _logger.LogError("Cannot update config with null or empty ID");
                    return false;
                }

                // Serialize to JSON
                var json = JsonSerializer.Serialize(config);

                try
                {
                    // Update in Redis
                    var db = _redis.GetDatabase();
                    var redisKey = $"{_redisKeyPrefix}{config.Id}";
                    await db.StringSetAsync(redisKey, json);
                }
                catch (RedisConnectionException ex)
                {
                    _logger.LogError(ex, $"Redis connection failed when updating config: {config.Id}. Config will be available in memory cache only.");
                    // Still update memory cache even if Redis fails
                    _cache.Set($"{CacheKeyPrefix}{config.Id}", config, _cacheExpiration);
                    return true;
                }

                // Clear memory cache
                await ClearCacheAsync(config.Id);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating config: {config.Id}");
                return false;
            }
        }

        public async Task<bool> ClearCacheAsync(string configId)
        {
            try
            {
                // Clear from memory cache
                var cacheKey = $"{CacheKeyPrefix}{configId}";
                _cache.Remove(cacheKey);

                // No need to clear from Redis as we've just updated it
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error clearing cache for config: {configId}");
                return false;
            }
        }

        public async Task<bool> ClearAllCacheAsync()
        {
            try
            {
                // Clear the ALL cache key
                _cache.Remove($"{CacheKeyPrefix}ALL");

                // Get all Redis keys and clear corresponding memory cache
                var db = _redis.GetDatabase();
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var keys = server.Keys(pattern: $"{_redisKeyPrefix}*").ToArray();

                foreach (var key in keys)
                {
                    var configId = key.ToString().Replace(_redisKeyPrefix, "");
                    _cache.Remove($"{CacheKeyPrefix}{configId}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all cache");
                return false;
            }
        }

        public async Task<bool> LoadAllConfigsAsync()
        {
            try
            {
                var configs = new List<TilemapConfig>();

                try
                {
                    var db = _redis.GetDatabase();
                    var server = _redis.GetServer(_redis.GetEndPoints().First());
                    var keys = server.Keys(pattern: $"{_redisKeyPrefix}*").ToArray();

                    foreach (var key in keys)
                    {
                        var json = await db.StringGetAsync(key);
                        if (!json.IsNullOrEmpty)
                        {
                            try
                            {
                                var config = JsonSerializer.Deserialize<TilemapConfig>(json.ToString());
                                if (config != null && !string.IsNullOrEmpty(config.Id))
                                {
                                    configs.Add(config);
                                    _cache.Set($"{CacheKeyPrefix}{config.Id}", config, _cacheExpiration);
                                }
                            }
                            catch (JsonException ex)
                            {
                                var configId = key.ToString().Replace(_redisKeyPrefix, "");
                                _logger.LogError(ex, $"Error deserializing config from Redis for ID: {configId}");
                            }
                        }
                    }

                    _logger.LogInformation($"Loaded {keys.Length} configurations from Redis into memory cache");
                }
                catch (RedisConnectionException ex)
                {
                    _logger.LogError(ex, "Redis connection failed when loading configurations. Will depend on MongoDB loading.");
                    return false;
                }

                // Store all configs in cache if we have any
                if (configs.Any())
                {
                    _cache.Set($"{CacheKeyPrefix}ALL", configs, _cacheExpiration);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all configurations from Redis");
                return false;
            }
        }
    }
}