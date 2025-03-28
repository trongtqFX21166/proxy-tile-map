namespace VietmapLive.TitleMap.Api.Config
{
    public class VietmapOptions
    {
        public string BaseUrl { get; set; } = "https://tile.vietmap.live";
        public string FastmapBaseUrl { get; set; } = "https://maps.fastmap.vn";
        public int DefaultCacheMaxAge { get; set; } = 604800;
    }
}
