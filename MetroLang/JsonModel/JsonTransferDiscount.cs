using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 换乘优惠的具体参数。
    /// </summary>
    public class JsonTransferDiscount
    {
        /// <summary>
        /// 是否启用换乘优惠。
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        /// <summary>
        /// 优惠金额（或折扣力度，取决于计价逻辑）。
        /// </summary>
        [JsonPropertyName("discount_amount")]
        public double DiscountAmount { get; set; }

        /// <summary>
        /// 优惠有效时间窗口（单位：分钟）。
        /// </summary>
        [JsonPropertyName("time_limit_minutes")]
        public int TimeLimitMinutes { get; set; }
    }
}