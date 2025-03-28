using Platform.Serilog;
using StackExchange.Redis;
using VietmapLive.TitleMap.Api.Config;
using VietmapLive.TitleMap.Api.Providers;
using VietmapLive.TitleMap.Api.Services;
using MongoDB.Driver;

namespace VietmapLive.TitleMap.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Register options
            builder.Services.Configure<VietmapOptions>(builder.Configuration.GetSection("Vietmap"));

            // Register APM for application monitoring
            builder.Services.AddAllElasticApm();

            // Register Redis connection
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var logger = sp.GetRequiredService<ILogger<Program>>();

                // Get Redis cluster configuration
                var hosts = configuration["Redis:Hosts"]?.Split(',');
                var ports = configuration["Redis:Ports"]?.Split(',');
                var password = configuration["Redis:Password"];

                if (hosts == null || ports == null || hosts.Length != ports.Length)
                {
                    logger.LogWarning("Redis cluster configuration is invalid, using default localhost:6379");
                    return ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false");
                }

                // Build connection string for the Redis cluster
                var configOptions = new ConfigurationOptions
                {
                    Password = password,
                    AbortOnConnectFail = false,
                    ConnectRetry = 3,
                    ConnectTimeout = 5000,
                    SyncTimeout = 5000
                };

                // Add all endpoints
                for (int i = 0; i < hosts.Length; i++)
                {
                    if (int.TryParse(ports[i], out int port))
                    {
                        configOptions.EndPoints.Add(hosts[i], port);
                    }
                }

                try
                {
                    logger.LogInformation("Connecting to Redis cluster...");
                    return ConnectionMultiplexer.Connect(configOptions);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to connect to Redis cluster. Using a dummy implementation for development.");
                    // Return a mock implementation for development purposes
                    return ConnectionMultiplexer.Connect("127.0.0.1:6379,abortConnect=false");
                }
            });

            // Register MongoDB client
            builder.Services.AddSingleton<IMongoClient>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var connectionString = configuration["MongoDB:ConnectionString"];

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("MongoDB connection string is not configured");
                }

                return new MongoClient(connectionString);
            });

            // Register memory cache
            builder.Services.AddMemoryCache();

            // Register services
            builder.Services.AddSingleton<ICombinedConfigProvider, CombinedConfigProvider>();
            builder.Services.AddSingleton<IMapboxRouteProvider, MapboxRouteProvider>();
            builder.Services.AddSingleton<IMapboxConfigService, MapboxConfigService>();
            builder.Services.AddSingleton<ITileProviderService, TileProviderService>();
            builder.Services.AddSingleton<ITilemapConfigService, TilemapConfigService>();

            // Register HTTP clients
            builder.Host.ConfigureServices((context, services) =>
            {
                // Add the Mapbox HTTP client
                services.AddHttpClient("Mapbox", httpClient =>
                {
                    httpClient.BaseAddress = new Uri($"{context.Configuration["Mapbox:BaseUrl"]}");
                });

                // Add a general HTTP client
                services.AddHttpClient();
            }).RegisterSerilogConfig();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            // Preload configs when the application starts
            app.Lifetime.ApplicationStarted.Register(async () =>
            {
                try
                {
                    using var scope = app.Services.CreateScope();
                    var configService = scope.ServiceProvider.GetRequiredService<ITilemapConfigService>();
                    await configService.LoadAllConfigsAsync();

                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Successfully preloaded tilemap configurations.");
                }
                catch (Exception ex)
                {
                    // We can't get the logger here, so log to console
                    Console.Error.WriteLine($"Failed to preload configurations: {ex.Message}");
                }
            });

            app.Run();
        }
    }
}