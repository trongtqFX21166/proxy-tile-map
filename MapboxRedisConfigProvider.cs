using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Text.Json;
using VietmapLive.TitleMap.Api.Models;

namespace VietmapLive.TitleMap.Api.Providers
{
    public interface IMapboxConfigProvider
    {
        Task<TilemapConfig?> GetConfigAsync(string configId);
        Task<bool> UpdateConfigAsync(TilemapConfig config);
        Task<bool> LoadAllConfigsAsync();
    }

    public class MapboxRedisConfigProvider : IMapboxConfigProvider
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MapboxRedisConfigProvider> _logger;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);
        private const string RedisKeyPrefix = "MapboxConfig:";
        private const string CacheKeyPrefix = "MapboxConfig_";

        public MapboxRedisConfigProvider(
            IConnectionMultiplexer redis,
            IMemoryCache cache,
            ILogger<MapboxRedisConfigProvider> logger)
        {
            _redis = redis;
            _cache = cache;
            _logger = logger;
        }

        public async Task<TilemapConfig?> GetConfigAsync(string configId)
        {
            var cacheKey = $"{CacheKeyPrefix}{configId}";

            if (!_cache.TryGetValue(cacheKey, out TilemapConfig? config))
            {
                var db = _redis.GetDatabase();
                var redisKey = $"{RedisKeyPrefix}{configId}";
                var json = await db.StringGetAsync(redisKey);

                if (!json.IsNullOrEmpty)
                {
                    try
                    {
                        config = JsonSerializer.Deserialize<TilemapConfig>(json.ToString());

                        if (config != null)
                        {
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

                var json = JsonSerializer.Serialize(config);
                var db = _redis.GetDatabase();
                var redisKey = $"{RedisKeyPrefix}{config.Id}";

                await db.StringSetAsync(redisKey, json);

                // Invalidate cache
                var cacheKey = $"{CacheKeyPrefix}{config.Id}";
                _cache.Remove(cacheKey);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating config in Redis: {config.Id}");
                return false;
            }
        }

        public async Task<bool> LoadAllConfigsAsync()
        {
            try
            {
                var db = _redis.GetDatabase();
                var keys = _redis.GetServer(_redis.GetEndPoints().First())
                    .Keys(pattern: $"{RedisKeyPrefix}*").ToArray();

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
                                _cache.Set($"{CacheKeyPrefix}{config.Id}", config, _cacheExpiration);
                            }
                        }
                        catch (JsonException ex)
                        {
                            var configId = key.ToString().Replace(RedisKeyPrefix, "");
                            _logger.LogError(ex, $"Error deserializing config from Redis for ID: {configId}");
                        }
                    }
                }

                _logger.LogInformation($"Loaded {keys.Length} configurations from Redis into memory cache");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all configurations from Redis");
                return false;
            }
        }
    }
}