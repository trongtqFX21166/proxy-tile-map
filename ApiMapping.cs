namespace VietmapLive.TitleMap.Api.Services
{
    public static class ApiMapping
    {
        /// <summary>
        /// Maps API path patterns to configuration IDs
        /// </summary>
        public static readonly Dictionary<string, string> PathToConfigMap = new()
        {
            { "/data/{z}/{x}/{y}", "base_tiles" },
            { "/3dbuildings/{z}/{x}/{y}", "buildings3d_tiles" },
            { "/glyphs/{fontstack}/{range}", "glyphs" },
            { "/sprite/{sprite}", "sprite" },
            { "/models/{name}", "models" },
            { "/a.satellite/{z}/{x}/{y}", "satellite_a" },
            { "/b.satellite/{z}/{x}/{y}", "satellite_b" },
            { "/traffic/{z}/{x}/{y}", "traffic" }
        };

        /// <summary>
        /// Default content types for different endpoints
        /// </summary>
        public static readonly Dictionary<string, string> EndpointContentTypes = new()
        {
            { "base_tiles", "application/x-protobuf" },
            { "buildings3d_tiles", "application/x-protobuf" },
            { "glyphs", "application/x-protobuf" },
            { "sprite", "application/json" },  // Will be dynamically changed to image/png for .png requests
            { "models", "application/x-protobuf" },
            { "satellite_a", "image/png" },
            { "satellite_b", "image/png" },
            { "traffic", "image/png" }
        };
    }
}