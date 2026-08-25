using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 方向定义中单个车站的运行属性。
    /// </summary>
    public class JsonDirectionStation
    {
        /// <summary>
        /// 车站 ID。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 到下一站的距离（米）。null 或极大值通常表示终点站。
        /// </summary>
        [JsonPropertyName("next")]
        public double? Next { get; set; }

        /// <summary>
        /// 本站停站时间（秒）。覆盖全局默认值。
        /// </summary>
        [JsonPropertyName("dwell_time")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DwellTime { get; set; }

        /// <summary>
        /// 本站适用的特定优惠策略列表。
        /// </summary>
        [JsonPropertyName("promotions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<JsonPromotionPolicy>? Promotions { get; set; }
    }
}