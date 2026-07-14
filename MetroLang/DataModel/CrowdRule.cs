namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 拥挤度系数规则。
    /// </summary>
    public class CrowdRule
    {
        /// <summary>目标方向 Key</summary>
        public string DirectionKey { get; set; } = string.Empty;

        /// <summary>规则生效开始时间</summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>规则生效结束时间</summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>拥挤度倍率（>1拥挤，<1通畅）</summary>
        public double Factor { get; set; }

        /// <summary>适用星期集合</summary>
        public List<DayOfWeek> Days { get; set; } = new();
    }
}
