using Microsoft.AspNetCore.Mvc;
using VietmapLive.TitleMap.Api.Models;
using VietmapLive.TitleMap.Api.Services;

namespace VietmapLive.TitleMap.Api.Controllers
{
    [ApiController]
    [Route("admin/tilemap-config")]
    public class TilemapConfigController : ControllerBase
    {
        private readonly ITilemapConfigService _configService;
        private readonly ILogger<TilemapConfigController> _logger;
        private readonly IConfiguration _configuration;

        public TilemapConfigController(
            ITilemapConfigService configService,
            IConfiguration configuration,
            ILogger<TilemapConfigController> logger)
        {
            _configService = configService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllConfigs()
        {
            try
            {
                var configs = await _configService.GetAllConfigsAsync();
                return Ok(configs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all configurations");
                return StatusCode(500, "An error occurred while retrieving configurations");
            }
        }

        [HttpGet("{configId}")]
        public async Task<IActionResult> GetConfig(string configId)
        {
            try
            {
                var config = await _configService.GetConfigAsync(configId);
                if (config == null)
                {
                    return NotFound($"No configuration found for ID: {configId}");
                }

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving configuration for ID: {configId}");
                return StatusCode(500, $"An error occurred while retrieving the configuration");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrUpdateConfig([FromBody] TilemapConfig config)
        {
            try
            {
                if (string.IsNullOrEmpty(config.Api))
                {
                    return BadRequest("API path is required");
                }

                if (config.Providers.Count == 0)
                {
                    return BadRequest("At least one provider is required");
                }

                if (string.IsNullOrEmpty(config.ActivateProvider) ||
                    !config.Providers.Any(p => p.Name == config.ActivateProvider))
                {
                    return BadRequest("Active provider must be one of the available providers");
                }

                bool success = await _configService.UpdateConfigAsync(config);
                if (!success)
                {
                    return StatusCode(500, "Failed to update configuration");
                }

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration");
                return StatusCode(500, "An error occurred while updating the configuration");
            }
        }

        [HttpPut("{configId}/switch-provider/{providerName}")]
        public async Task<IActionResult> SwitchProvider(string configId, string providerName)
        {
            try
            {
                bool success = await _configService.SwitchProviderAsync(configId, providerName);
                if (!success)
                {
                    return BadRequest($"Failed to switch to provider '{providerName}' for config: {configId}");
                }

                var updatedConfig = await _configService.GetConfigAsync(configId);
                return Ok(updatedConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error switching provider for config: {configId}");
                return StatusCode(500, "An error occurred while switching the provider");
            }
        }

        [HttpPost("batch")]
        public async Task<IActionResult> CreateOrUpdateBatch([FromBody] List<TilemapConfig> configs)
        {
            try
            {
                // Validate all configs first
                foreach (var config in configs)
                {
                    if (string.IsNullOrEmpty(config.Api))
                    {
                        return BadRequest($"API path is required for config: {config.Id}");
                    }

                    if (config.Providers.Count == 0)
                    {
                        return BadRequest($"At least one provider is required for config: {config.Id}");
                    }

                    if (string.IsNullOrEmpty(config.ActivateProvider) ||
                        !config.Providers.Any(p => p.Name == config.ActivateProvider))
                    {
                        return BadRequest($"Active provider must be one of the available providers for config: {config.Id}");
                    }
                }

                // If validation passes, update all configs at once
                bool success = await _configService.UpdateConfigsAsync(configs);

                if (!success)
                {
                    return StatusCode(500, "Failed to update configurations in batch");
                }

                // Return the updated configs
                var updatedConfigs = await _configService.GetAllConfigsAsync();
                return Ok(updatedConfigs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configurations in batch");
                return StatusCode(500, "An error occurred while updating configurations");
            }
        }

        [HttpGet("reload")]
        public async Task<IActionResult> ReloadConfigurations()
        {
            try
            {
                bool success = await _configService.LoadAllConfigsAsync();

                if (!success)
                {
                    return StatusCode(500, "Failed to reload configurations");
                }

                var configs = await _configService.GetAllConfigsAsync();
                return Ok(new
                {
                    Message = $"Successfully reloaded {configs.Count()} configurations",
                    Configurations = configs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading configurations");
                return StatusCode(500, "An error occurred while reloading configurations");
            }
        }

        [HttpPost("initialize-defaults")]
        public async Task<IActionResult> InitializeDefaultConfigurations()
        {
            try
            {
                var existingConfigs = await _configService.GetAllConfigsAsync();
                if (existingConfigs.Any())
                {
                    return BadRequest("Configurations already exist. Use the reload endpoint instead or delete existing configurations first.");
                }

                var configs = CreateDefaultConfigurations();
                bool success = await _configService.UpdateConfigsAsync(configs);

                if (!success)
                {
                    return StatusCode(500, "Failed to initialize default configurations");
                }

                // Reload configurations
                await _configService.LoadAllConfigsAsync();

                return Ok(new
                {
                    Message = $"Successfully created {configs.Count} default configurations",
                    Configurations = configs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing default configurations");
                return StatusCode(500, "An error occurred while initializing default configurations");
            }
        }

        private List<TilemapConfig> CreateDefaultConfigurations()
        {
            var configs = new List<TilemapConfig>();
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string mapboxAccessToken = _configuration["Mapbox:AccessToken"] ?? "";

            // 1. Base tiles configuration
            configs.Add(new TilemapConfig
            {
                Id = "base_tiles",
                Api = "/base/{z}/{x}/{y}",
                Providers = new List<ProviderInfo>
                {
                    new ProviderInfo
                    {
                        Name = "vietmap",
                        Url = "https://maps.fastmap.vn/mt/tile/data-20250317/{z}/{x}/{y}",
                        ContentType = "application/x-protobuf"
                    },
                    new ProviderInfo
                    {
                        Name = "mapbox",
                        Url = "https://api.mapbox.com/v4/mapbox.mapbox-bathymetry-v2,mapbox.mapbox-streets-v8,mapbox.mapbox-terrain-v2,mapbox.mapbox-models-v1/{z}/{x}/{y}.vector.pbf?access_token=" + mapboxAccessToken,
                        ContentType = "application/x-protobuf"
                    }
                },
                ActivateProvider = "vietmap",
                ActivateTime = currentTime,
                CacheMaxAge = 604800 // 7 days
            });

            // 2. 3D Buildings configuration
            configs.Add(new TilemapConfig
            {
                Id = "buildings3d_tiles",
                Api = "/3dbuildings/{z}/{x}/{y}",
                Providers = new List<ProviderInfo>
                {
                    new ProviderInfo
                    {
                        Name = "mapbox",
                        Url = "https://api.mapbox.com/3dtiles/v1/mapbox.mapbox-3dbuildings-v1/{z}/{x}/{y}.glb?access_token=" + mapboxAccessToken,
                        ContentType = "application/x-protobuf"
                    }
                },
                ActivateProvider = "mapbox",
                ActivateTime = currentTime,
                CacheMaxAge = 604800
            });

            // 3. Glyphs configuration
            configs.Add(new TilemapConfig
            {
                Id = "glyphs",
                Api = "/glyphs/{fontstack}/{range}",
                Providers = new List<ProviderInfo>
                {
                    new ProviderInfo
                    {
                        Name = "vietmap",
                        Url = "https://tile.vietmap.live/production/v3/fonts/{fontstack}/{range}.pbf",
                        ContentType = "application/x-protobuf"
                    },
                    new ProviderInfo
                    {
                        Name = "vietmap_fastmap",
                        Url = "https://maps.fastmap.vn/mt/fonts/{fontstack}/{range}.pbf",
                        ContentType = "application/x-protobuf"
                    },
                    new ProviderInfo
                    {
                        Name = "mapbox",
                        Url = "https://api.mapbox.com/fonts/v1/mapbox/{fontstack}/{range}.pbf?access_token=" + mapboxAccessToken,
                        ContentType = "application/x-protobuf"
                    }
                },
                ActivateProvider = "vietmap",
                ActivateTime = currentTime,
                CacheMaxAge = 604800
            });

            // 4. Sprite configuration
            configs.Add(new TilemapConfig
            {
                Id = "sprite",
                Api = "/sprite/{sprite}",
                Providers = new List<ProviderInfo>
                {
                    new ProviderInfo
                    {
                        Name = "vietmap",
                        Url = "https://tile.vietmap.live/production/v3/sprite{sprite}",
                        ContentType = "application/json" // Content type will be dynamically set based on .png suffix
                    },
                    new ProviderInfo
                    {
                        Name = "vietmap_fastmap",
                        Url = "https://maps.fastmap.vn/mt/styles/neutral033/sprite{sprite}",
                        ContentType = "application/json"
                    },
                    new ProviderInfo
                    {
                        Name = "mapbox",
                        Url = "https://api.mapbox.com/styles/v1/mapbox/standard/efkz8o3ujknnkoa17380x12mp/{sprite}?access_token=" + mapboxAccessToken,
                        ContentType = "application/json"
                    }
                },
                ActivateProvider = "vietmap",
                ActivateTime = currentTime,
                CacheMaxAge = 604800
            });

            // 5. Models configuration
            configs.Add(new TilemapConfig
            {
                Id = "models",
                Api = "/models/{name}",
                Providers = new List<ProviderInfo>
                {
                    new ProviderInfo
                    {
                        Name = "mapbox",
                        Url = "https://api.mapbox.com/models/v1/mapbox/{name}?access_token=" + mapboxAccessToken,
                        ContentType = "application/x-protobuf"
                    }
                },
                ActivateProvider = "mapbox",
                ActivateTime = currentTime,
                CacheMaxAge = 604800
            });

            // 6. Satellite A configuration
            configs.Add(new TilemapConfig
            {
                Id = "satellite_a",
                Api = "/a.satellite/{z}/{x}/{y}",
                Providers = new List<ProviderInfo>
                {
                    new ProviderInfo
                    {
                        Name = "vietmap",
                        Url = "https://tile.vietmap.live/production/v3/a.satellite/{z}/{x}/{y}",
                        ContentType = "image/png"
                    },
                    new ProviderInfo
                    {
                        Name = "google",
                        Url = "http://mt0.google.com/vt/lyrs=s&x={x}&y={y}&z={z}&scale=2",
                        ContentType = "image/png",
                        AdditionalHeaders = new Dictionary<string, string>
                        {
                            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36" }
                        }
                    }
                },
                ActivateProvider = "vietmap",
                ActivateTime = currentTime,
                CacheMaxAge = 604800
            });

            // 7. Satellite B configuration
            configs.Add(new TilemapConfig
            {
                Id = "satellite_b",
                Api = "/b.satellite/{z}/{x}/{y}",
                Providers = new List<ProviderInfo>
                {
                    new ProviderInfo
                    {
                        Name = "vietmap",
                        Url = "https://tile.vietmap.live/production/v3/b.satellite/{z}/{x}/{y}",
                        ContentType = "image/png"
                    },
                    new ProviderInfo
                    {
                        Name = "google",
                        Url = "http://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}&scale=2",
                        ContentType = "image/png",
                        AdditionalHeaders = new Dictionary<string, string>
                        {
                            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36" }
                        }
                    }
                },
                ActivateProvider = "vietmap",
                ActivateTime = currentTime,
                CacheMaxAge = 604800
            });

            // 8. Traffic configuration
            configs.Add(new TilemapConfig
            {
                Id = "traffic",
                Api = "/traffic/{z}/{x}/{y}",
                Providers = new List<ProviderInfo>
                {
                    new ProviderInfo
                    {
                        Name = "vietmap",
                        Url = "https://tile.vietmap.live/production/v3/traffic/{z}/{x}/{y}",
                        ContentType = "image/png"
                    },
                    new ProviderInfo
                    {
                        Name = "vietmap_fastmap",
                        Url = "https://maps.fastmap.vn/mt/tf/{z}/{x}/{y}",
                        ContentType = "image/png"
                    }
                },
                ActivateProvider = "vietmap",
                ActivateTime = currentTime,
                CacheMaxAge = 60 // Short cache for traffic data
            });

            return configs;
        }
    }
}