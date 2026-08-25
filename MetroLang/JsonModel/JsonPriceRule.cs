using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 独立的附加票价规则（用于跨城或多套票价体系）。
    /// </summary>
    public class JsonPriceRule
    {
        /// <summary>
        /// 适用城市标识。
        /// </summary>
        [JsonPropertyName("city")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? City { get; set; }

        /// <summary>
        /// 适用线路 ID。
        /// </summary>
        [JsonPropertyName("line_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LineId { get; set; }

        /// <summary>
        /// 具体的票价配置。
        /// </summary>
        [JsonPropertyName("price")]
        public JsonPrice Price { get; set; } = new JsonPrice();
    }
}