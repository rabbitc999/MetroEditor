namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 特殊票价规则定义。
    /// </summary>
    public class SpecialFareRule
    {
        /// <summary>适用线路 ID</summary>
        public string LineId { get; set; } = string.Empty;

        /// <summary>票价类型标识</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>票价金额</summary>
        public double Price { get; set; }

        /// <summary>区间起点站</summary>
        public string StartStation { get; set; } = string.Empty;

        /// <summary>区间终点站</summary>
        public string EndStation { get; set; } = string.Empty;
    }
}
