using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 定义特定区间内的列车最高运行速度限制。
    /// </summary>
    public class JsonSpeedLimit
    {
        /// <summary>
        /// 目标方向 Key。
        /// </summary>
        [JsonPropertyName("direction_key")]
        public string DirectionKey { get; set; } = string.Empty;

        /// <summary>
        /// 限速区间的起始站 ID。
        /// </summary>
        [JsonPropertyName("from_station")]
        public string FromStation { get; set; } = string.Empty;

        /// <summary>
        /// 限速区间的终点站 ID。
        /// </summary>
        [JsonPropertyName("to_station")]
        public string ToStation { get; set; } = string.Empty;

        /// <summary>
        /// 最高允许速度（单位：km/h）。
        /// </summary>
        [JsonPropertyName("speed_kmh")]
        public int SpeedKmh { get; set; }
    }
}