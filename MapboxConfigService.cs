using VietmapLive.TitleMap.Api.Models;
using VietmapLive.TitleMap.Api.Providers;

namespace VietmapLive.TitleMap.Api.Services
{
    public interface IMapboxConfigService
    {
        Task<TilemapConfig?> GetConfigAsync(string configId);
        Task<bool> UpdateConfigAsync(TilemapConfig config);
        Task<(string Url, string ContentType, Dictionary<string, string>? Headers)> ResolveEndpointAsync(string apiPath, Dictionary<string, string> parameters);
    }

    public class MapboxConfigService : IMapboxConfigService
    {
        private readonly ICombinedConfigProvider _configProvider;
        private readonly IMapboxRouteProvider _routeProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MapboxConfigService> _logger;

        public MapboxConfigService(
            ICombinedConfigProvider configProvider,
            IMapboxRouteProvider routeProvider,
            IConfiguration configuration,
            ILogger<MapboxConfigService> logger)
        {
            _configProvider = configProvider;
            _routeProvider = routeProvider;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<TilemapConfig?> GetConfigAsync(string configId)
        {
            return await _configProvider.GetConfigAsync(configId);
        }

        public async Task<bool> UpdateConfigAsync(TilemapConfig config)
        {
            if (string.IsNullOrEmpty(config.Id))
            {
                _logger.LogError("Cannot update config with null or empty ID");
                return false;
            }

            // Ensure ActivateTime is set
            if (config.ActivateTime == 0)
            {
                config.ActivateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            // Update the config in Redis and clear the memory cache
            return await _configProvider.UpdateConfigAsync(config);
        }

        public async Task<(string Url, string ContentType, Dictionary<string, string>? Headers)> ResolveEndpointAsync(string apiPath, Dictionary<string, string> parameters)
        {
            try
            {
                // Parse the API path and get the corresponding config ID and parameters
                var routeInfo = _routeProvider.ParseApiPath(apiPath);
                if (routeInfo == null)
                {
                    throw new InvalidOperationException($"No route mapping found for API path: {apiPath}");
                }

                var (configId, _parameters) = routeInfo.Value;

                // Get the configuration
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
                if (url.Contains("{accessToken}"))
                {
                    string accessToken = _configuration["Mapbox:AccessToken"] ?? "";
                    url = url.Replace("{accessToken}", accessToken);
                }

                // Add base URL for relative URLs if necessary
                if (!url.StartsWith("http"))
                {
                    if (provider.Name == "mapbox")
                    {
                        string baseUrl = _configuration["Mapbox:BaseUrl"] ?? "https://api.mapbox.com";
                        url = $"{baseUrl.TrimEnd('/')}{url}";
                    }
                    else if (provider.Name.StartsWith("vietmap"))
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
                string contentType = provider.ContentType ?? "application/octet-stream";

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
    }
}