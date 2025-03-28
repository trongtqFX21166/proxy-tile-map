using Microsoft.AspNetCore.Mvc;
using VietmapLive.TitleMap.Api.Services;

namespace VietmapLive.TitleMap.Api.Controllers
{
    [Route("")]
    [ApiController]
    public class TilemapController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ITileProviderService _tileProvider;
        private readonly ILogger<TilemapController> _logger;

        public TilemapController(
            IConfiguration configuration,
            ITileProviderService tileProvider,
            ILogger<TilemapController> logger)
        {
            _configuration = configuration;
            _tileProvider = tileProvider;
            _logger = logger;
        }

        [HttpGet("standard.json")]
        public IActionResult Get()
        {
            return Redirect("https://minio.vietmap.vn/phananh/dev/standard.json");
        }

        [HttpGet("data/{z}/{x}/{y}")]
        public async Task<IActionResult> Data(string z, string x, string y)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "z", z },
                    { "x", x },
                    { "y", y }
                };

                var (content, statusCode, contentType, cacheMaxAge) = await _tileProvider.FetchTileDataAsync("/data/{z}/{x}/{y}", parameters);

                Response.StatusCode = statusCode;
                Response.ContentType = contentType;
                Response.Headers.CacheControl = $"public, max-age={cacheMaxAge}";

                await Response.BodyWriter.WriteAsync(content);

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing data request for z={z}, x={x}, y={y}");
                return StatusCode(500);
            }
        }

        [HttpGet("3dbuildings/{z}/{x}/{y}")]
        public async Task<IActionResult> Buildings3d(string z, string x, string y)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "z", z },
                    { "x", x },
                    { "y", y }
                };

                var (content, statusCode, contentType, cacheMaxAge) = await _tileProvider.FetchTileDataAsync("/3dbuildings/{z}/{x}/{y}", parameters);

                Response.StatusCode = statusCode;
                Response.ContentType = contentType;
                Response.Headers.CacheControl = $"public, max-age={cacheMaxAge}";

                await Response.BodyWriter.WriteAsync(content);

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing 3D buildings request for z={z}, x={x}, y={y}");
                return StatusCode(500);
            }
        }

        [HttpGet("glyphs/{fontstack}/{range}")]
        public async Task<IActionResult> Glyphs(string fontstack, string range)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "fontstack", fontstack },
                    { "range", range }
                };

                var (content, statusCode, contentType, cacheMaxAge) = await _tileProvider.FetchTileDataAsync("/glyphs/{fontstack}/{range}", parameters);

                Response.StatusCode = statusCode;
                Response.ContentType = contentType;
                Response.Headers.CacheControl = $"public, max-age={cacheMaxAge}";

                await Response.BodyWriter.WriteAsync(content);

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing glyphs request for fontstack={fontstack}, range={range}");
                return StatusCode(500);
            }
        }

        [HttpGet("sprite/{sprite}")]
        public async Task<IActionResult> Sprite(string sprite)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "sprite", sprite }
                };

                var (content, statusCode, contentType, cacheMaxAge) = await _tileProvider.FetchTileDataAsync("/sprite/{sprite}", parameters);

                // Determine the correct content type based on the extension
                string effectiveContentType = sprite.EndsWith(".png") ? "image/png" : contentType;

                Response.StatusCode = statusCode;
                Response.ContentType = effectiveContentType;
                Response.Headers.CacheControl = $"public, max-age={cacheMaxAge}";

                await Response.BodyWriter.WriteAsync(content);

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing sprite request for sprite={sprite}");
                return StatusCode(500);
            }
        }

        [HttpGet("models/{name}")]
        public async Task<IActionResult> Models(string name)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "name", name }
                };

                var (content, statusCode, contentType, cacheMaxAge) = await _tileProvider.FetchTileDataAsync("/models/{name}", parameters);

                Response.StatusCode = statusCode;
                Response.ContentType = contentType;
                Response.Headers.CacheControl = $"public, max-age={cacheMaxAge}";

                await Response.BodyWriter.WriteAsync(content);

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing models request for name={name}");
                return StatusCode(500);
            }
        }

        [HttpGet("a.satellite/{z}/{x}/{y}")]
        public async Task<IActionResult> SatelliteA(string z, string x, string y)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "z", z },
                    { "x", x },
                    { "y", y }
                };

                var (content, statusCode, contentType, cacheMaxAge) = await _tileProvider.FetchTileDataAsync("/a.satellite/{z}/{x}/{y}", parameters);

                Response.StatusCode = statusCode;
                Response.ContentType = contentType;
                Response.Headers.CacheControl = $"public, max-age={cacheMaxAge}";

                return File(content, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing satellite A request for z={z}, x={x}, y={y}");
                return StatusCode(500);
            }
        }

        [HttpGet("b.satellite/{z}/{x}/{y}")]
        public async Task<IActionResult> SatelliteB(string z, string x, string y)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "z", z },
                    { "x", x },
                    { "y", y }
                };

                var (content, statusCode, contentType, cacheMaxAge) = await _tileProvider.FetchTileDataAsync("/b.satellite/{z}/{x}/{y}", parameters);

                Response.StatusCode = statusCode;
                Response.ContentType = contentType;
                Response.Headers.CacheControl = $"public, max-age={cacheMaxAge}";

                return File(content, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing satellite B request for z={z}, x={x}, y={y}");
                return StatusCode(500);
            }
        }

        [HttpGet("traffic/{z}/{x}/{y}")]
        public async Task<IActionResult> Traffic(string z, string x, string y)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "z", z },
                    { "x", x },
                    { "y", y }
                };

                var (content, statusCode, contentType, cacheMaxAge) = await _tileProvider.FetchTileDataAsync("/traffic/{z}/{x}/{y}", parameters);

                Response.StatusCode = statusCode;
                Response.ContentType = contentType;
                Response.Headers.CacheControl = $"public, max-age={cacheMaxAge}";

                return File(content, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing traffic request for z={z}, x={x}, y={y}");
                return StatusCode(500);
            }
        }
    }
}