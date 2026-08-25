namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 特定线路的特殊票价配置（如机场线、磁悬浮）。
    /// </summary>
    public class SpecialLine
    {
        /// <summary>目标线路 ID</summary>
        public string LineId { get; set; } = "_";

        /// <summary>全线统一票价（固定值）</summary>
        public double? All { get; set; }

        /// <summary>按区间计算的附加费列表</summary>
        public List<SurchargeItem> Surcharge { get; set; } = new();

        /// <summary>引用的其他线路 ID 列表（用于联合计价）</summary>
        public List<string> UseLines { get; set; } = new();

        /// <summary>该线路的默认起步票价</summary>
        public double? DefaultFare { get; set; }

        /// <summary>该线路的分段计价配置</summary>
        public List<Segment> Segments { get; set; } = new();

        /// <summary>该线路的线性续价配置</summary>
        public LinearExtension? LinearExtension { get; set; }

        public SpecialLine Clone()
        {
            return new SpecialLine
            {
                LineId = this.LineId,
                All = this.All,
                Surcharge = this.Surcharge.Select(s => s.Clone()).ToList(),
                UseLines = new List<string>(this.UseLines),
                DefaultFare = this.DefaultFare,
                Segments = this.Segments.Select(s => s.Clone()).ToList(),
                LinearExtension = this.LinearExtension?.Clone()
            };
        }
    }
}