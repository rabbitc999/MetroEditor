namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 换乘优惠的具体参数。
    /// </summary>
    public class TransferDiscount
    {
        /// <summary>是否启用换乘优惠</summary>
        public bool Enabled { get; set; }

        /// <summary>优惠金额（或折扣力度）</summary>
        public double DiscountAmount { get; set; }

        /// <summary>优惠有效时间窗口（单位：分钟）</summary>
        public int TimeLimitMinutes { get; set; }

        public TransferDiscount Clone()
        {
            return new TransferDiscount
            {
                Enabled = this.Enabled,
                DiscountAmount = this.DiscountAmount,
                TimeLimitMinutes = this.TimeLimitMinutes
            };
        }
    }
}