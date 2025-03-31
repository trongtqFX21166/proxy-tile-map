using Microsoft.Extensions.Caching.Memory;

namespace VietmapLive.TitleMap.Api.Services
{
    public interface ITileProviderService
    {
        Task<(byte[] Content, int StatusCode, string ContentType, int CacheMaxAge)> FetchTileDataAsync(string apiPath, Dictionary<string, string> parameters);
    }

    public class TileProviderService : ITileProviderService
    {
        private readonly IMapboxConfigService _configService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TileProviderService> _logger;
        private readonly IConfiguration _configuration;

        public TileProviderService(
            IMapboxConfigService configService,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<TileProviderService> logger)
        {
            _configService = configService;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<(byte[] Content, int StatusCode, string ContentType, int CacheMaxAge)> FetchTileDataAsync(string apiPath, Dictionary<string, string> parameters)
        {
            try
            {
                // Create a cache key from the API path and parameters
                string cacheKey = CreateCacheKey(apiPath, parameters);
                
                // Try to get from cache first
                if (_cache.TryGetValue(cacheKey, out (byte[] Content, int StatusCode, string ContentType, int CacheMaxAge) cachedResult))
                {
                    return cachedResult;
                }

                // Resolve the endpoint
                var (url, contentType, headers) = await _configService.ResolveEndpointAsync(apiPath, parameters);

                // Create a client with a custom name if needed, otherwise use a general client
                var client = _httpClientFactory.CreateClient();

                // Set up request
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Add any custom headers
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }

                // Make the request
                var response = await client.SendAsync(request);
                
                var content = await response.Content.ReadAsByteArrayAsync();
                var statusCode = (int)response.StatusCode;

                // Get cache max age from configuration (default to 7 days)
                int cacheMaxAge = _configuration.GetValue<int>("Mapbox:DefaultCacheMaxAge", 300);

                // If this is traffic data, use a shorter cache time
                if (apiPath.Contains("/traffic/"))
                {
                    cacheMaxAge = _configuration.GetValue<int>("Vietmap:TrafficCacheMaxAge", 300);
                }

                var result = (content, statusCode, contentType, cacheMaxAge);

                // If the request was successful, cache the result
                if (response.IsSuccessStatusCode)
                {
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromSeconds(cacheMaxAge));
                        
                    _cache.Set(cacheKey, result, cacheOptions);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching tile data for API path: {apiPath}");
                throw;
            }
        }

        private string CreateCacheKey(string apiPath, Dictionary<string, string> parameters)
        {
            var key = $"Tile:{apiPath}";
            
            if (parameters != null && parameters.Count > 0)
            {
                var paramString = string.Join(":", parameters.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));
                key += $":{paramString}";
            }
            
            return key;
        }
    }
}