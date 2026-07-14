
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // OPEN_TIME 解析：运营时间定义
    // 定义线路或全局的每日运营时间窗口
    // 语法示例：OPEN_TIME GLOBAL 05:00 23:00 或 OPEN_TIME LINE1 06:00 22:00 WEEKEND
    // ====================================================================
    internal class OpenTime : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 解析作用域：GLOBAL 或具体线路ID
            string lineId = null;
            if (parser.IsPeekId("GLOBAL"))
                parser.ParseId();
            else
                lineId = parser.ParseId();

            // 解析运营时间范围
            var (startTime, endTime) = parser.ParseTimeRange();

            // 解析适用日期集合
            List<DayOfWeek> applicableDays = new();
            if (parser.Peek().TokenType == TokenType.Id || parser.Peek().TokenType == TokenType.Symbol)
            {
                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                    applicableDays = parser.ParseDayOfWeekList();
                else if (parser.Peek().TokenType == TokenType.Id)
                    applicableDays = parser.ParseDayOfWeekModifier();
            }

            // 创建并添加运营时间规则
            context.OpenTimes.Add(new OpenTimeRule
            {
                LineId = lineId,
                StartTime = startTime,
                EndTime = endTime,
                Days = applicableDays
            });
        }
    }
}
