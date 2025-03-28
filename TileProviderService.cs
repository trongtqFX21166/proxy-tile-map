namespace VietmapLive.TitleMap.Api.Services
{
    public interface ITileProviderService
    {
        Task<(byte[] Content, int StatusCode, string ContentType, int CacheMaxAge)> FetchTileDataAsync(string apiPath, Dictionary<string, string> parameters);
    }

    public class TileProviderService
    {
    }
}
