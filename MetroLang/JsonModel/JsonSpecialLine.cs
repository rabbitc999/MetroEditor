using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 特定线路的特殊票价配置（如机场线、磁悬浮）。
    /// </summary>
    public class JsonSpecialLine
    {
        /// <summary>
        /// 目标线路 ID。"_" 通常表示全局默认或占位符。
        /// </summary>
        [JsonPropertyName("lineId")]
        public string LineId { get; set; } = "_";

        /// <summary>
        /// 全线统一票价（固定值）。
        /// </summary>
        [JsonPropertyName("all")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? All { get; set; }

        /// <summary>
        /// 按区间计算的附加费列表。
        /// </summary>
        [JsonPropertyName("surcharge")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<JsonSurchargeItem> Surcharge { get; set; }

        /// <summary>
        /// 引用的其他线路 ID 列表（用于联合计价）。
        /// </summary>
        [JsonPropertyName("use_lines")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> UseLines { get; set; }

        /// <summary>
        /// 该线路的默认起步票价。
        /// </summary>
        [JsonPropertyName("default_fare")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? DefaultFare { get; set; }

        /// <summary>
        /// 该线路的分段计价配置。
        /// </summary>
        [JsonPropertyName("segments")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<JsonSegment> Segments { get; set; }

        /// <summary>
        /// 该线路的线性续价配置。
        /// </summary>
        [JsonPropertyName("linear_extension")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonLinearExtension? LinearExtension { get; set; }
    }
}