using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 定义两站之间的虚拟换乘规则（出站换乘/虚拟换乘）。
    /// </summary>
    public class JsonVirtualTransferRule
    {
        /// <summary>
        /// 换乘起点站 ID。
        /// </summary>
        [JsonPropertyName("station_a")]
        public string StationA { get; set; } = string.Empty;

        /// <summary>
        /// 换乘终点站 ID。
        /// </summary>
        [JsonPropertyName("station_b")]
        public string StationB { get; set; } = string.Empty;

        /// <summary>
        /// 换乘耗时（单位：分钟）。
        /// </summary>
        [JsonPropertyName("time_minutes")]
        public double TimeMinutes { get; set; }

        /// <summary>
        /// 是否需要重新安检。
        /// </summary>
        [JsonPropertyName("security_check")]
        public bool SecurityCheck { get; set; }

        /// <summary>
        /// 换乘必须经过的方向 Key 列表（用于复杂路径约束）。
        /// </summary>
        [JsonPropertyName("via_directions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? ViaDirections { get; set; }
    }
}