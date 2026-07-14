namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 线性续价规则（超过阈值后按公里计费）。
    /// </summary>
    public class MoneyCalcRule : MoneyRule
    {
        /// <summary>计费阈值（公里）</summary>
        public double DistSegment { get; set; }

        /// <summary>每公里加价金额</summary>
        public double AddPrice { get; set; }
    }
}
