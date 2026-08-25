using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // STATION_MODE 解析：车站运营模式定义
    // 定义车站在特定时间段内的进出站限制
    // 语法示例：STATION_MODE "西直门" ENTRY_ONLY TIME_RANGE 07:00 09:00 WORKDAY
    // ====================================================================
    internal class StationMode : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 解析目标车站名称
            string station = parser.ParseString();
            string mode = parser.ParseId().ToUpperInvariant();

            // 可选时间范围参数
            TimeSpan? startTime = null, endTime = null;
            if (parser.IsPeekId("TIME_RANGE"))
            {
                parser.ParseId();
                var (start, end) = parser.ParseTimeRange();
                startTime = start;
                endTime = end;
            }

            // 解析适用日期集合
            List<DayOfWeek> applicableDays = new();
            if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                applicableDays = parser.ParseDayOfWeekList();
            else if (parser.Peek().TokenType == TokenType.Id)
                applicableDays = parser.ParseDayOfWeekModifier();

            // 创建并添加车站运营模式规则
            context.StationModes.Add(new StationModeRule
            {
                Station = station,
                Mode = mode,
                StartTime = startTime,
                EndTime = endTime,
                Days = applicableDays
            });
        }
    }
}
