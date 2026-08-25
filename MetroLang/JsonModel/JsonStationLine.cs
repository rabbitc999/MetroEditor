using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 描述车站在某一条线路上的属性。
    /// </summary>
    public class JsonStationLine
    {
        /// <summary>
        /// 线路 ID。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 该线路在该站的起始方向标识。
        /// </summary>
        [JsonPropertyName("start")]
        public string Start { get; set; } = string.Empty;

        /// <summary>
        /// 该线路在该站的终止方向标识。
        /// </summary>
        [JsonPropertyName("end")]
        public string End { get; set; } = string.Empty;

        /// <summary>
        /// 车站状态（如 "OPEN", "CLOSED", "FUTURE"）。
        /// </summary>
        [JsonPropertyName("status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Status { get; set; }

        /// <summary>
        /// 该线路在该车站的平台坐标（若与全局坐标不同）。
        /// </summary>
        [JsonPropertyName("platform_coord")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonCoord? PlatformCoord { get; set; }
    }
}