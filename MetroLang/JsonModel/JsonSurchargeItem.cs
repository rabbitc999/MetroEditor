using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 定义两站之间的固定附加费。
    /// </summary>
    public class JsonSurchargeItem
    {
        /// <summary>
        /// 起始节点（通常是车站 ID）。
        /// </summary>
        [JsonPropertyName("node_a")]
        public string NodeA { get; set; } = string.Empty;

        /// <summary>
        /// 到达节点。
        /// </summary>
        [JsonPropertyName("node_b")]
        public string NodeB { get; set; } = string.Empty;

        /// <summary>
        /// 附加费用金额。
        /// </summary>
        [JsonPropertyName("price")]
        public double Price { get; set; }
    }
}