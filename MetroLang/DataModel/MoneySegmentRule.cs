namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 分段票价规则（阶梯票价）。
    /// </summary>
    public class MoneySegmentRule : MoneyRule
    {
        /// <summary>里程区间起点（公里）</summary>
        public double Start { get; set; }

        /// <summary>里程区间终点（公里）</summary>
        public double End { get; set; }

        /// <summary>该区间内票价</summary>
        public double Price { get; set; }

        public new MoneySegmentRule Clone()
        {
            return new MoneySegmentRule
            {
                Start = this.Start,
                End = this.End,
                Price = this.Price
            };
        }
    }
}
