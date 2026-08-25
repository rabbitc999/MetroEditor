using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{

    /// <summary>
    /// 自动扶梯详情。
    /// </summary>
    public class JsonEscalatorInfo
    {
        /// <summary>
        /// 扶梯数量。
        /// </summary>
        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        /// <summary>
        /// 运行方向：上行 (up)、下行 (down)、双向 (both)。
        /// </summary>
        [JsonPropertyName("direction")]
        public string Direction { get; set; } = "both";
    }
}