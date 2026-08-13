using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 路段类型定义（高架/地下/地面）。
    /// </summary>
    public class JsonSegmentType
    {
        /// <summary>起始车站 ID</summary>
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        /// <summary>终点车站 ID</summary>
        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        /// <summary>路段类型：elevated(高架)、underground(地下)、ground(地面)</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }
}
