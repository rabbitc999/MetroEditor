using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 方向下的交路信息（列车运行交路方案）。
    /// </summary>
    public class JsonRouting
    {
        /// <summary>
        /// 交路名称（如 "全程交路"、"机场北交路"）。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 该交路依次经过的车站 ID 列表。
        /// </summary>
        [JsonPropertyName("stations")]
        public List<string> Stations { get; set; } = new();
    }
}
