
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // HEADWAY_PROFILE 解析：发车间隔配置
    // 定义特定线路的发车间隔时间表
    // 语法示例：
    // HEADWAY_PROFILE LINE1
    //   PERIOD 06:00 09:00 120
    //   PERIOD 09:00 17:00 180
    //   PERIOD 17:00 20:00 120
    // END_DEFINE
    // ====================================================================
    internal class HeadwayProfile : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string lineId = parser.ParseId();

            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                parser.ParseId("PERIOD");

                // 可选 TIME_RANGE 关键字
                if (parser.IsPeekId("TIME_RANGE"))
                    parser.ParseId();

                // 解析时间范围
                var (startTime, endTime) = parser.ParseTimeRange();

                // 可选 VALUE 关键字
                if (parser.IsPeekId("VALUE"))
                    parser.ParseId();

                // 解析发车间隔（秒）
                int intervalSeconds = (int)parser.ParseTimeInSeconds();

                // 解析日期修饰符或列表
                List<DayOfWeek> applicableDays = new();
                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                    applicableDays = parser.ParseDayOfWeekList();
                else if (parser.Peek().TokenType == TokenType.Id)
                    applicableDays = parser.ParseDayOfWeekModifier();

                // 创建并添加发车间隔规则
                context.HeadwayRules.Add(new HeadwayRule
                {
                    LineId = lineId,
                    StartTime = startTime,
                    EndTime = endTime,
                    IntervalSeconds = intervalSeconds,
                    Days = applicableDays
                });
            }
        }
    }
}
