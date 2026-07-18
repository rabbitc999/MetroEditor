namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 线路运营时间规则。
    /// </summary>
    public class OpenTimeRule
    {
        /// <summary>适用线路 ID（空表示全局）</summary>
        public string LineId { get; set; } = string.Empty;

        /// <summary>当日开始运营时间</summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>当日结束运营时间</summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>适用星期集合</summary>
        public List<DayOfWeek> Days { get; set; } = new();

        public OpenTimeRule Clone()
        {
            return new OpenTimeRule
            {
                LineId = this.LineId,
                StartTime = this.StartTime,
                EndTime = this.EndTime,
                Days = new List<DayOfWeek>(this.Days)
            };
        }
    }
}
