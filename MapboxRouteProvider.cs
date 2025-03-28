using System.Collections.Concurrent;

namespace VietmapLive.TitleMap.Api.Providers
{
    public interface IMapboxRouteProvider
    {
        (string ConfigId, Dictionary<string, string> Parameters)? ParseApiPath(string apiPath);
    }

    public class MapboxRouteProvider : IMapboxRouteProvider
    {
        private static readonly ConcurrentDictionary<string, string> _routePatterns = new();
        private readonly ILogger<MapboxRouteProvider> _logger;

        public MapboxRouteProvider(ILogger<MapboxRouteProvider> logger)
        {
            _logger = logger;
            InitializeRoutePatterns();
        }

        private void InitializeRoutePatterns()
        {
            // Define mappings for API paths to config IDs
            _routePatterns.TryAdd("/data/{z}/{x}/{y}", "base_tiles");
            _routePatterns.TryAdd("/3dbuildings/{z}/{x}/{y}", "buildings3d_tiles");
            _routePatterns.TryAdd("/glyphs/{fontstack}/{range}", "glyphs");
            _routePatterns.TryAdd("/sprite/{sprite}", "sprite");
            _routePatterns.TryAdd("/models/{name}", "models");
            _routePatterns.TryAdd("/a.satellite/{z}/{x}/{y}", "satellite_a");
            _routePatterns.TryAdd("/b.satellite/{z}/{x}/{y}", "satellite_b");
            _routePatterns.TryAdd("/traffic/{z}/{x}/{y}", "traffic");
        }

        public (string ConfigId, Dictionary<string, string> Parameters)? ParseApiPath(string apiPath)
        {
            try
            {
                foreach (var pattern in _routePatterns)
                {
                    var templateParts = pattern.Key.Split('/');
                    var apiParts = apiPath.Split('/');

                    if (templateParts.Length != apiParts.Length)
                    {
                        continue;
                    }

                    var parameters = new Dictionary<string, string>();
                    bool isMatch = true;

                    for (int i = 0; i < templateParts.Length; i++)
                    {
                        if (templateParts[i].StartsWith("{") && templateParts[i].EndsWith("}"))
                        {
                            // This is a parameter
                            var paramName = templateParts[i].Substring(1, templateParts[i].Length - 2);
                            parameters.Add(paramName, apiParts[i]);
                        }
                        else if (templateParts[i] != apiParts[i])
                        {
                            // Not a parameter and doesn't match
                            isMatch = false;
                            break;
                        }
                    }

                    if (isMatch)
                    {
                        return (pattern.Value, parameters);
                    }
                }

                _logger.LogWarning($"No route pattern matched for API path: {apiPath}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing API path: {apiPath}");
                return null;
            }
        }
    }
}