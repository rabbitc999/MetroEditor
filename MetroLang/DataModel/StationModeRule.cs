namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 车站运营模式规则（如只进不出）。
    /// </summary>
    public class StationModeRule
    {
        /// <summary>目标车站 ID</summary>
        public string Station { get; set; } = string.Empty;

        /// <summary>运营模式（ENTRY_ONLY/EXIT_ONLY/BOTH）</summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>模式生效开始时间（可选）</summary>
        public TimeSpan? StartTime { get; set; }

        /// <summary>模式生效结束时间（可选）</summary>
        public TimeSpan? EndTime { get; set; }

        /// <summary>适用星期集合</summary>
        public List<DayOfWeek> Days { get; set; } = new();
    }
}
