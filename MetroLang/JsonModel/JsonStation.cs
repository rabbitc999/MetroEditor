using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 车站的基础信息模型。
    /// </summary>
    public class JsonStation
    {
        /// <summary>
        /// 地理经度（WGS-84）。
        /// </summary>
        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        /// <summary>
        /// 地理纬度（WGS-84）。
        /// </summary>
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        /// <summary>
        /// 该车站所属的线路及方向信息列表。
        /// </summary>
        [JsonPropertyName("lines")]
        public List<JsonStationLine> Lines { get; set; } = new();

        /// <summary>
        /// 换乘关系映射表（Key 为线路 ID，Value 为换乘细节对象）。
        /// </summary>
        [JsonPropertyName("transfers")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Transfers { get; set; }

        /// <summary>
        /// 所属城市标识（用于跨城数据）。
        /// </summary>
        [JsonPropertyName("city")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? City { get; set; }

        /// <summary>
        /// 所属城市标识（用于跨城数据）。
        /// </summary>
        [JsonPropertyName("other_names")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? OtherNames { get; set; }
    }
}