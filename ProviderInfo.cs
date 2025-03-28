namespace VietmapLive.TitleMap.Api.Models
{
    public class ProviderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public Dictionary<string, string>? AdditionalHeaders { get; set; }
        public string? ContentType { get; set; }
    }
}
