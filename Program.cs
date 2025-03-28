using Platform.Serilog;

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
