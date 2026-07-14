namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 发车间隔规则。
    /// </summary>
    public class HeadwayRule
    {
        /// <summary>适用线路 ID</summary>
        public string LineId { get; set; } = string.Empty;

        /// <summary>规则生效开始时间</summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>规则生效结束时间</summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>发车间隔时长（秒）</summary>
        public int IntervalSeconds { get; set; }

        /// <summary>适用星期集合</summary>
        public List<DayOfWeek> Days { get; set; } = new();
    }
}
