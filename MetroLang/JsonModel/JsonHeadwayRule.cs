using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    // ========================================================================
    // 规则类（发车间隔、站点模式、拥挤度等）
    // ========================================================================

    /// <summary>
    /// 定义特定时段内的列车发车间隔。
    /// </summary>
    public class JsonHeadwayRule
    {
        /// <summary>
        /// 适用的线路 ID。
        /// </summary>
        [JsonPropertyName("line_id")]
        public string LineId { get; set; } = string.Empty;

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
        /// 发车间隔时长（单位：秒）。
        /// </summary>
        [JsonPropertyName("interval_seconds")]
        public int IntervalSeconds { get; set; }

        /// <summary>
        /// 适用的星期集合（如 ["MON", "TUE", "WORKDAY"]）。为空则视为每日有效。
        /// </summary>
        [JsonPropertyName("days")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Days { get; set; }
    }
}