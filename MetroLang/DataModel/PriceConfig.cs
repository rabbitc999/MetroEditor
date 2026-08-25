using MetroEditor.MetroLang.JsonModel;

namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 票价配置容器（支持按城市/线路定制）。
    /// </summary>
    public class PriceConfig
    {
        /// <summary>适用城市（空表示全局）</summary>
        public string? City { get; set; }

        /// <summary>适用线路 ID（空表示全路网）</summary>
        public string? LineId { get; set; }

        /// <summary>货币代码</summary>
        public string Currency { get; set; } = "CNY";

        /// <summary>默认起步票价</summary>
        public double DefaultFare { get; set; } = 2.0;

        /// <summary>分段计价配置列表</summary>
        public List<Segment> Segments { get; set; } = new();

        /// <summary>线性续价配置</summary>
        public LinearExtension? LinearExtension { get; set; }

        /// <summary>换乘优惠配置</summary>
        public TransferDiscount? TransferDiscount { get; set; }

        /// <summary>票价封顶上限（0表示无封顶）</summary>
        public double FareCap { get; set; } = 0;

        /// <summary>特殊线路票价规则列表</summary>
        public List<SpecialLine> SpecialLines { get; set; } = new();

        /// <summary>
        /// 深拷贝当前票价配置，返回一个独立的副本。
        /// </summary>
        public PriceConfig Clone()
        {
            return new PriceConfig
            {
                City = this.City,
                LineId = this.LineId,
                Currency = this.Currency,
                DefaultFare = this.DefaultFare,
                Segments = this.Segments.Select(s => s.Clone()).ToList(),
                LinearExtension = this.LinearExtension?.Clone(),
                TransferDiscount = this.TransferDiscount?.Clone(),
                FareCap = this.FareCap,
                SpecialLines = this.SpecialLines.Select(sl => sl.Clone()).ToList()
            };
        }
    }
}
