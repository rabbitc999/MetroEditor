using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 定义特定方向在特定时段内的拥挤度系数。
    /// </summary>
    public class JsonCrowdRule
    {
        /// <summary>
        /// 目标方向 Key（由线路和方向定义生成）。
        /// </summary>
        [JsonPropertyName("direction_key")]
        public string DirectionKey { get; set; } = string.Empty;

        /// <summary>
        /// 规则生效的开始时间（格式：HH:mm）。
        /// </summary>
        [JsonPropertyName("start_time")]
        public string StartTime { get; set; } = string.Empty;

        /// <summary>
        /// 规则生效的结束时间（格式：HH:mm）。
        /// </summary>
        [JsonPropertyName("end_time")]
        public string EndTime { get; set; } = string.Empty;

        /// <summary>
        /// 拥挤度倍率（>1 表示拥挤，<1 表示通畅）。
        /// </summary>
        [JsonPropertyName("factor")]
        public double Factor { get; set; }

        /// <summary>
        /// 适用的星期集合。为空则视为每日有效。
        /// </summary>
        [JsonPropertyName("days")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Days { get; set; }
    }
}