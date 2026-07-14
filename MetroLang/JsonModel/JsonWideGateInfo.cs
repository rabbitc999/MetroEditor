using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 宽通道闸机详情。
    /// </summary>
    public class JsonWideGateInfo
    {
        /// <summary>
        /// 闸机数量。
        /// </summary>
        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
    }
}