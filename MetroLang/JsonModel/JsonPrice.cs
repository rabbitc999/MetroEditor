using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 票价计算的核心配置模型。
    /// </summary>
    public class JsonPrice
    {
        /// <summary>
        /// 货币代码（如 CNY, USD）。
        /// </summary>
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "CNY";

        /// <summary>
        /// 计价方式：distance（按里程）或 zone（按分区）。
        /// </summary>
        [JsonPropertyName("pricingType")]
        public string PricingType { get; set; } = "distance";

        /// <summary>
        /// 分段计价配置列表（阶梯票价）。
        /// </summary>
        [JsonPropertyName("segments")]
        public List<JsonSegment> Segments { get; set; } = new();

        /// <summary>
        /// 超过阈值后的线性续价规则。
        /// </summary>
        [JsonPropertyName("linearExtension")]
        public JsonLinearExtension LinearExtension { get; set; } = new();

        /// <summary>
        /// 换乘优惠政策配置。
        /// </summary>
        [JsonPropertyName("transferDiscount")]
        public JsonTransferDiscount TransferDiscount { get; set; } = new();

        /// <summary>
        /// 特定线路的附加费或特殊票价规则。
        /// </summary>
        [JsonPropertyName("specialLines")]
        public List<JsonSpecialLine> SpecialLines { get; set; } = new();

        /// <summary>
        /// 票价封顶上限（0 表示无封顶）。
        /// </summary>
        [JsonPropertyName("fare_cap")]
        public double FareCap { get; set; } = 0;

        /// <summary>
        /// 默认起步票价。
        /// </summary>
        [JsonPropertyName("default_fare")]
        public double DefaultFare { get; set; } = 2.0;
    }
}