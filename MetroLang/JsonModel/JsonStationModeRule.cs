using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 定义车站在特定时间段内的运营模式（如进站/出站限制）。
    /// </summary>
    public class JsonStationModeRule
    {
        /// <summary>
        /// 目标车站 ID。
        /// </summary>
        [JsonPropertyName("station")]
        public string Station { get; set; } = string.Empty;

        /// <summary>
        /// 运营模式：ENTRY_ONLY（只进）、EXIT_ONLY（只出）、BOTH（正常）。
        /// </summary>
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = string.Empty;

        /// <summary>
        /// 模式生效的开始时间（格式：HH:mm）。若为 null 则表示全天有效。
        /// </summary>
        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        /// <summary>
        /// 模式生效的结束时间（格式：HH:mm）。
        /// </summary>
        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }

        /// <summary>
        /// 适用的星期集合。为空则视为每日有效。
        /// </summary>
        [JsonPropertyName("days")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Days { get; set; }
    }
}