using Platform.Serilog;
using StackExchange.Redis;
using VietmapLive.TitleMap.Api.Providers;
using VietmapLive.TitleMap.Api.Services;

namespace VietmapLive.TitleMap.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddAllElasticApm();

            // Register Redis connection
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var redisConnectionString = configuration["Redis:ConnectionString"];

                if (string.IsNullOrEmpty(redisConnectionString))
                {
                    throw new InvalidOperationException("Redis connection string is not configured");
                }

                return ConnectionMultiplexer.Connect(redisConnectionString);
            });

            // Register memory cache
            builder.Services.AddMemoryCache();

            // Register Mapbox configuration and route provider services
            builder.Services.AddSingleton<IMapboxConfigProvider, MapboxRedisConfigProvider>();
            builder.Services.AddSingleton<IMapboxConfigService, MapboxConfigService>();
            builder.Services.AddSingleton<IMapboxRouteProvider, MapboxRouteProvider>();


            builder.Host.ConfigureServices((context, services) =>
            {
                services.AddHttpClient("Mapbox", httpClient =>
                {
                    httpClient.BaseAddress = new Uri($"{context.Configuration["Mapbox:BaseUrl"]}");
                });
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

            app.Run();
        }
    }
}
