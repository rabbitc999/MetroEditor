
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // CROWD_PROFILE 解析：拥挤度系数配置
    // 定义特定方向的拥挤度时间表
    // 语法示例：
    // CROWD_PROFILE LINE1_0
    //   PERIOD 07:00 09:00 1.5
    //   PERIOD 17:00 19:00 1.3
    // END_DEFINE
    // ====================================================================
    internal class CrowdProfile : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string directionKey = parser.ParseId();
            if (!context.Directions.ContainsKey(directionKey))
                throw new SyntaxException($"方向 '{directionKey}' 未定义", parser.Peek());

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

                // 解析拥挤度系数
                double crowdFactor = parser.ParseDouble();

                // 解析日期修饰符或列表
                List<DayOfWeek> applicableDays = new();
                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                    applicableDays = parser.ParseDayOfWeekList();
                else if (parser.Peek().TokenType == TokenType.Id)
                    applicableDays = parser.ParseDayOfWeekModifier();

                // 创建并添加拥挤度规则
                context.CrowdRules.Add(new CrowdRule
                {
                    DirectionKey = Utils.ConvertToExternalDirKey(context, directionKey),
                    StartTime = startTime,
                    EndTime = endTime,
                    Factor = crowdFactor,
                    Days = applicableDays
                });
            }
        }
    }
}
