using MongoDB.Bson.Serialization.Attributes;

namespace VietmapLive.TitleMap.Api.Models
{
    public class TilemapConfig
    {
        [BsonId]
        public string? Id { get; set; }
        public string Api { get; set; } = string.Empty;
        public List<ProviderInfo> Providers { get; set; } = new();
        public string ActivateProvider { get; set; } = string.Empty;
        public long ActivateTime { get; set; }
        public int CacheMaxAge { get; set; } = 604800;
    }
}
