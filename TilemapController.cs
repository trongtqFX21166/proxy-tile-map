using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace VietmapLive.TitleMap.Api.Controllers
{
    [Route("")]
    [ApiController]
    public class TilemapController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _client;
        private readonly ILogger<TilemapController> _logger;

        public TilemapController(IConfiguration configuration,
            IHttpClientFactory clientFactory,
            ILogger<TilemapController> logger)
        {
            _configuration = configuration;
            _client = clientFactory.CreateClient("Mapbox");
            _logger = logger;
        }

        [HttpGet("standard.json")]
        public IActionResult Get()
        {
            var second = DateTime.Now.Second;

            return Redirect("https://minio.vietmap.vn/phananh/dev/standard.json");

            //if (second % 2 == 0)
            //{
            //    return Redirect("https://minio.vietmap.vn/phananh/dev/standard.json");
            //}
            //else
            //{

            //    return Redirect("https://api.vietmap.live/production/tilemap/standard.json");
            //}
        }

        [HttpGet("data/{z}/{x}/{y}")]
        public async Task<IActionResult> Data(string z, string x, string y)
        {
            try
            {
                string path = $"/v4/mapbox.mapbox-bathymetry-v2,mapbox.mapbox-streets-v8,mapbox.mapbox-terrain-v2,mapbox.mapbox-models-v1/{z}/{x}/{y}.vector.pbf?access_token={_configuration["Mapbox:AccessToken"]}";

                var response = await _client.GetAsync(path);
                var content = await response.Content.ReadAsByteArrayAsync();

                Response.StatusCode = (int)response.StatusCode;
                Response.ContentType = "application/x-protobuf";
                Response.Headers.CacheControl = $"public, max-age={_configuration["Mapbox:DefaultCacheMaxAge"]}"; // Lấy giá trị max-age từ cấu hình

                await Response.BodyWriter.WriteAsync(content);

                return StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing request: {ex}");
                return StatusCode(500);
            }
        }

        [HttpGet("3dbuildings/{z}/{x}/{y}")]
        public async Task<IActionResult> Buildings3d(string z, string x, string y)
        {
            try
            {
                string path = $"/3dtiles/v1/mapbox.mapbox-3dbuildings-v1/{z}/{x}/{y}.glb?access_token={_configuration["Mapbox:AccessToken"]}";

                var response = await _client.GetAsync(path);
                var content = await response.Content.ReadAsByteArrayAsync();

                Response.StatusCode = (int)response.StatusCode;
                Response.ContentType = "application/x-protobuf";
                Response.Headers.CacheControl = $"public, max-age={_configuration["Mapbox:DefaultCacheMaxAge"]}"; // Lấy giá trị max-age từ cấu hình

                await Response.BodyWriter.WriteAsync(content);

                return StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing request: {ex}");
                return StatusCode(500);
            }
        }

        [HttpGet("glyphs/{fontstack}/{range}")]
        public async Task<IActionResult> Glyphs(string fontstack, string range)
        {
            try
            {
                string path = $"/fonts/v1/mapbox/{fontstack}/{range}.pbf?access_token={_configuration["Mapbox:AccessToken"]}";

                var response = await _client.GetAsync(path);
                var content = await response.Content.ReadAsByteArrayAsync();

                Response.StatusCode = (int)response.StatusCode;
                Response.ContentType = "application/x-protobuf";
                Response.Headers.CacheControl = $"public, max-age={_configuration["Mapbox:DefaultCacheMaxAge"]}"; // Lấy giá trị max-age từ cấu hình

                await Response.BodyWriter.WriteAsync(content);

                return StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing request: {ex}");
                return StatusCode(500);
            }
        }

        [HttpGet("sprite/{sprite}")]
        public async Task<IActionResult> Sprite(string sprite)
        {
            try
            {
                string path = $"/styles/v1/mapbox/standard/efkz8o3ujknnkoa17380x12mp/{sprite}?access_token={_configuration["Mapbox:AccessToken"]}";

                var response = await _client.GetAsync(path);
                var content = await response.Content.ReadAsByteArrayAsync();

                Response.StatusCode = (int)response.StatusCode;
                Response.ContentType = "application/x-protobuf";
                Response.Headers.CacheControl = $"public, max-age={_configuration["Mapbox:DefaultCacheMaxAge"]}"; // Lấy giá trị max-age từ cấu hình

                await Response.BodyWriter.WriteAsync(content);

                return StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing request: {ex}");
                return StatusCode(500);
            }
        }

        [HttpGet("models/{name}")]
        public async Task<IActionResult> Models(string name)
        {
            try
            {
                string path = $"/models/v1/mapbox/{name}?access_token={_configuration["Mapbox:AccessToken"]}";

                var response = await _client.GetAsync(path);
                var content = await response.Content.ReadAsByteArrayAsync();

                Response.StatusCode = (int)response.StatusCode;
                Response.ContentType = "application/x-protobuf";
                Response.Headers.CacheControl = $"public, max-age={_configuration["Mapbox:DefaultCacheMaxAge"]}"; // Lấy giá trị max-age từ cấu hình

                await Response.BodyWriter.WriteAsync(content);

                return StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing request: {ex}");
                return StatusCode(500);
            }
        }

        [HttpGet("a.satellite/{z}/{x}/{y}")]
        public async Task<IActionResult> SatelliteA(string z, string x, string y)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    string path = $"http://mt0.google.com/vt/lyrs=s&x={x}&y={y}&z={z}&scale=2";

                    _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                    var response = await _client.GetAsync(path);
                    var content = await response.Content.ReadAsByteArrayAsync();

                    Response.StatusCode = (int)response.StatusCode;
                    Response.ContentType = "image/png";

                    return File(content, "image/png");
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing request: {ex}");
                return StatusCode(500);
            }
        }

        [HttpGet("b.satellite/{z}/{x}/{y}")]
        public async Task<IActionResult> SatelliteB(string z, string x, string y)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    string path = $"http://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}&scale=2";

                    _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                    var response = await _client.GetAsync(path);
                    var content = await response.Content.ReadAsByteArrayAsync();

                    Response.StatusCode = (int)response.StatusCode;
                    Response.ContentType = "image/png";

                    return File(content, "image/png");
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing request: {ex}");
                return StatusCode(500);
            }
        }

        //[HttpGet("mt3/{z}/{x}/{y}")]
        //public async Task<IActionResult> MT3(string z, string x, string y)
        //{
        //    try
        //    {
        //        using (var httpClient = new HttpClient())
        //        {
        //            string path = $"https://a.tiles.mapbox.com/v4/mapbox.satellite/{z}/{x}/{y}.png?access_token={_configuration["Mapbox:AccessToken"]}";

        //            var response = await _client.GetAsync(path);
        //            var content = await response.Content.ReadAsByteArrayAsync();

        //            Response.StatusCode = (int)response.StatusCode;
        //            Response.ContentType = "image/png";
        //            Response.Headers.CacheControl = $"public, max-age={_configuration["Mapbox:DefaultCacheMaxAge"]}"; // Lấy giá trị max-age từ cấu hình

        //            await Response.BodyWriter.WriteAsync(content);

        //            return StatusCode((int)response.StatusCode);
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError($"Error processing request: {ex}");
        //        return StatusCode(500);
        //    }
        //}
    }
}
