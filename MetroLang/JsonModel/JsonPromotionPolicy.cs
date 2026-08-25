using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 票务优惠政策详情。
    /// </summary>
    public class JsonPromotionPolicy
    {
        /// <summary>
        /// 政策唯一标识符（如 "student_card"）。
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 政策显示名称（如 "学生优惠"）。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 政策详细描述。
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 优惠类型：rate（打折）、fixed（定额减免）、fixed_discount（固定票价）。
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "rate";

        /// <summary>
        /// 优惠数值（如 0.5 表示 5 折，10 表示减 10 元）。
        /// </summary>
        [JsonPropertyName("value")]
        public double Value { get; set; }

        /// <summary>
        /// 适用的线路 ID 列表。为空表示适用于全网。
        /// </summary>
        [JsonPropertyName("applicable_lines")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? ApplicableLines { get; set; }

        /// <summary>
        /// 每日生效开始时间（格式：HH:mm）。
        /// </summary>
        [JsonPropertyName("start_time")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StartTime { get; set; }

        /// <summary>
        /// 每日生效结束时间（格式：HH:mm）。
        /// </summary>
        [JsonPropertyName("end_time")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EndTime { get; set; }

        /// <summary>
        /// 适用的星期集合（如 ["WORKDAY"]）。
        /// </summary>
        [JsonPropertyName("days")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Days { get; set; }
    }
}