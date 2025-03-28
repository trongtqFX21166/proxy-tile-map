using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using System.Text.RegularExpressions;
using VietmapLive.TitleMap.Api.Models;

namespace VietmapLive.TitleMap.Api.Services
{
    public interface ITilemapConfigService
    {
        Task<TilemapConfig?> GetConfigAsync(string configId);
        Task<IEnumerable<TilemapConfig>> GetAllConfigsAsync();
        Task<bool> UpdateConfigAsync(TilemapConfig config);
        Task<bool> SwitchProviderAsync(string configId, string providerName);
        Task<(string Url, string ContentType, Dictionary<string, string>? Headers)> ResolveEndpointAsync(string apiPath, Dictionary<string, string> parameters);
    }

    public class TilemapConfigService : ITilemapConfigService
    {
        private readonly IMongoCollection<TilemapConfig> _configCollection;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TilemapConfigService> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);
        private const string CacheKeyPrefix = "TilemapConfig_";

        public TilemapConfigService(
            IMongoClient mongoClient,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<TilemapConfigService> logger)
        {
            var database = mongoClient.GetDatabase(configuration["MongoDB:DatabaseName"]);
            _configCollection = database.GetCollection<TilemapConfig>(configuration["MongoDB:ConfigCollectionName"]);
            _cache = cache;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<TilemapConfig?> GetConfigAsync(string configId)
        {
            var cacheKey = $"{CacheKeyPrefix}{configId}";

            if (!_cache.TryGetValue(cacheKey, out TilemapConfig? config))
            {
                var filter = Builders<TilemapConfig>.Filter.Eq("_id", configId);
                config = await _configCollection.Find(filter).FirstOrDefaultAsync();

                if (config != null)
                {
                    _cache.Set(cacheKey, config, _cacheExpiration);
                }
                else
                {
                    _logger.LogWarning($"No configuration found for ID: {configId}");
                }
            }

            return config;
        }

        public async Task<IEnumerable<TilemapConfig>> GetAllConfigsAsync()
        {
            const string cacheKey = $"{CacheKeyPrefix}ALL";

            if (!_cache.TryGetValue(cacheKey, out List<TilemapConfig>? configs))
            {
                configs = await _configCollection.Find(_ => true).ToListAsync();
                _cache.Set(cacheKey, configs, _cacheExpiration);
            }

            return configs ?? new List<TilemapConfig>();
        }

        public async Task<bool> LoadAllConfigsAsync()
        {
            try
            {
                _logger.LogInformation("Loading all configurations into memory cache");
                var configs = await _configCollection.Find(_ => true).ToListAsync();

                // Store all configs in cache
                _cache.Set($"{CacheKeyPrefix}ALL", configs, _cacheExpiration);

                // Store individual configs in cache
                foreach (var config in configs)
                {
                    if (!string.IsNullOrEmpty(config.Id))
                    {
                        _cache.Set($"{CacheKeyPrefix}{config.Id}", config, _cacheExpiration);
                    }
                }

                _logger.LogInformation($"Loaded {configs.Count} configurations into memory cache");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all configurations");
                return false;
            }
        }

        public async Task<bool> UpdateConfigAsync(TilemapConfig config)
        {
            try
            {
                // Only update ActivateTime if it's a new config or the ActivateProvider has changed
                if (string.IsNullOrEmpty(config.Id))
                {
                    // New config, set the activation time
                    config.ActivateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    await _configCollection.InsertOneAsync(config);
                }
                else
                {
                    // Check if provider has changed
                    var existingConfig = await GetConfigAsync(config.Id);

                    if (existingConfig != null && existingConfig.ActivateProvider != config.ActivateProvider)
                    {
                        // Provider has changed, update activation time
                        config.ActivateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    }
                    else if (existingConfig != null)
                    {
                        // Provider hasn't changed, keep the original activation time
                        config.ActivateTime = existingConfig.ActivateTime;
                    }

                    var filter = Builders<TilemapConfig>.Filter.Eq("_id", config.Id);
                    await _configCollection.ReplaceOneAsync(filter, config, new ReplaceOptions { IsUpsert = true });
                }

                // Invalidate cache
                var cacheKey = $"{CacheKeyPrefix}{config.Id}";
                _cache.Remove(cacheKey);
                _cache.Remove($"{CacheKeyPrefix}ALL");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating config: {config.Id}");
                return false;
            }
        }

        public async Task<bool> UpdateConfigsAsync(IEnumerable<TilemapConfig> configs)
        {
            try
            {
                var bulkOperations = new List<WriteModel<TilemapConfig>>();
                var existingConfigs = await _configCollection.Find(_ => true).ToListAsync();
                var existingConfigsDict = existingConfigs.ToDictionary(c => c.Id);

                foreach (var config in configs)
                {
                    if (string.IsNullOrEmpty(config.Id))
                    {
                        // New config
                        config.ActivateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        bulkOperations.Add(new InsertOneModel<TilemapConfig>(config));
                    }
                    else if (existingConfigsDict.TryGetValue(config.Id, out var existingConfig))
                    {
                        // Only update ActivateTime if provider has changed
                        if (existingConfig.ActivateProvider != config.ActivateProvider)
                        {
                            config.ActivateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        }
                        else
                        {
                            config.ActivateTime = existingConfig.ActivateTime;
                        }

                        var filter = Builders<TilemapConfig>.Filter.Eq("_id", config.Id);
                        bulkOperations.Add(new ReplaceOneModel<TilemapConfig>(filter, config) { IsUpsert = true });
                    }
                    else
                    {
                        // Config with ID doesn't exist, but we'll upsert it
                        config.ActivateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var filter = Builders<TilemapConfig>.Filter.Eq("_id", config.Id);
                        bulkOperations.Add(new ReplaceOneModel<TilemapConfig>(filter, config) { IsUpsert = true });
                    }
                }

                // Execute all operations in a single batch
                if (bulkOperations.Any())
                {
                    var bulkResult = await _configCollection.BulkWriteAsync(bulkOperations);
                    _logger.LogInformation($"Bulk update completed: {bulkResult.ModifiedCount} modified, {bulkResult.UpsertedCount} upserted");
                }

                // Clear all cache
                _cache.Remove($"{CacheKeyPrefix}ALL");
                foreach (var config in configs)
                {
                    if (!string.IsNullOrEmpty(config.Id))
                    {
                        _cache.Remove($"{CacheKeyPrefix}{config.Id}");
                    }
                }

                // Load updated configs into cache
                await LoadAllConfigsAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating multiple configurations");
                return false;
            }
        }

        public async Task<bool> SwitchProviderAsync(string configId, string providerName)
        {
            try
            {
                var config = await GetConfigAsync(configId);
                if (config == null)
                {
                    _logger.LogWarning($"No configuration found for ID: {configId}");
                    return false;
                }

                // Verify the provider exists
                if (!config.Providers.Any(p => p.Name == providerName))
                {
                    _logger.LogWarning($"Provider '{providerName}' not found in config: {configId}");
                    return false;
                }

                config.ActivateProvider = providerName;
                config.ActivateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                return await UpdateConfigAsync(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error switching provider for config: {configId}");
                return false;
            }
        }

        public async Task<(string Url, string ContentType, Dictionary<string, string>? Headers)> ResolveEndpointAsync(string apiPath, Dictionary<string, string> parameters)
        {
            try
            {
                // Find the appropriate config ID from the API path
                string? configId = null;

                foreach (var mapping in ApiMapping.PathToConfigMap)
                {
                    if (IsPathMatch(apiPath, mapping.Key))
                    {
                        configId = mapping.Value;
                        break;
                    }
                }

                if (configId == null)
                {
                    throw new InvalidOperationException($"No config mapping found for API path: {apiPath}");
                }

                var config = await GetConfigAsync(configId);
                if (config == null)
                {
                    throw new InvalidOperationException($"No configuration found for ID: {configId}");
                }

                // Get the active provider
                var provider = config.Providers.FirstOrDefault(p => p.Name == config.ActivateProvider);
                if (provider == null)
                {
                    throw new InvalidOperationException($"Active provider '{config.ActivateProvider}' not found in config");
                }

                // Process the URL with parameters
                string url = provider.Url;

                // Handle Mapbox access token if needed
                if (provider.Name == "mapbox" && url.Contains("{accessToken}"))
                {
                    string accessToken = _configuration["Mapbox:AccessToken"] ?? "";
                    url = url.Replace("{accessToken}", accessToken);
                }

                // Add base URL for relative URLs
                if (!url.StartsWith("http"))
                {
                    if (provider.Name == "mapbox")
                    {
                        string baseUrl = _configuration["Mapbox:BaseUrl"] ?? "https://api.mapbox.com";
                        url = $"{baseUrl.TrimEnd('/')}{url}";
                    }
                    else if (provider.Name == "vietmap")
                    {
                        string baseUrl = _configuration["Vietmap:BaseUrl"] ?? "https://tile.vietmap.live";
                        url = $"{baseUrl.TrimEnd('/')}{url}";
                    }
                }

                // Replace parameters in the URL
                foreach (var param in parameters)
                {
                    url = url.Replace($"{{{param.Key}}}", param.Value);
                }

                // Determine content type
                string contentType = provider.ContentType ??
                                    ApiMapping.EndpointContentTypes.GetValueOrDefault(configId, "application/octet-stream");

                // For sprite requests, determine if it's JSON or PNG
                if (configId == "sprite" && apiPath.Contains(".png"))
                {
                    contentType = "image/png";
                }

                return (url, contentType, provider.AdditionalHeaders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resolving endpoint for API path: {apiPath}");
                throw;
            }
        }

        private bool IsPathMatch(string actualPath, string patternPath)
        {
            // Convert the pattern path to a regex pattern
            string regexPattern = "^" + Regex.Escape(patternPath)
                .Replace("\\{z\\}", "([^/]+)")
                .Replace("\\{x\\}", "([^/]+)")
                .Replace("\\{y\\}", "([^/]+)")
                .Replace("\\{fontstack\\}", "([^/]+)")
                .Replace("\\{range\\}", "([^/]+)")
                .Replace("\\{sprite\\}", "([^/]+)")
                .Replace("\\{name\\}", "([^/]+)") + "$";

            return Regex.IsMatch(actualPath, regexPattern);
        }
    }
}