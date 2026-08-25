using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 定义线路的运营时间窗口。
    /// </summary>
    public class JsonOpenTimeRule
    {
        /// <summary>
        /// 适用的线路 ID。若为 null，则应用于全局所有线路。
        /// </summary>
        [JsonPropertyName("line_id")]
        public string? LineId { get; set; }

        /// <summary>
        /// 当日开始运营时间（格式：HH:mm）。
        /// </summary>
        [JsonPropertyName("start_time")]
        public string StartTime { get; set; } = string.Empty;

        /// <summary>
        /// 当日结束运营时间（格式：HH:mm）。
        /// </summary>
        [JsonPropertyName("end_time")]
        public string EndTime { get; set; } = string.Empty;

        /// <summary>
        /// 适用的星期集合。为空则视为每日有效。
        /// </summary>
        [JsonPropertyName("days")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Days { get; set; }
    }
}