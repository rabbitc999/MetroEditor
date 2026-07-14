using MetroEditor.MetroLang.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // DWELL_TIME_DEFINE 解析：自定义停站时间配置
    // 支持三种粒度：单站(NODE)、整条线路(LINE)、特定方向(DIRECTION)
    // 语法示例：
    // DWELL_TIME_DEFINE
    //   NODE "王府井" LINE1 45      # 特定站点特定线路停站45秒
    //   LINE LINE2 30               # LINE2所有站点停站30秒
    //   DIRECTION LINE1_0 35        # LINE1_0方向所有站点停站35秒
    // END_DEFINE
    // ====================================================================
    internal class DwellTimeDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                string subCommand = parser.ParseId().ToUpperInvariant();

                switch (subCommand)
                {
                    case "NODE":
                        // NODE "站名" 线路ID 停站时间（秒）
                        string station = parser.ParseString();
                        string lineId = parser.ParseId();
                        double timeSec = parser.ParseTimeInSeconds();

                        // 校验车站和线路是否存在
                        if (!context.Stations.ContainsKey(station))
                            throw new SyntaxException($"站台 '{station}' 未定义", parser.Peek());
                        if (!context.Lines.ContainsKey(lineId))
                            throw new SyntaxException($"线路 '{lineId}' 未定义", parser.Peek());

                        // 记录自定义停站时间
                        context.DwellTimes[(station, lineId)] = timeSec;
                        break;

                    case "LINE":
                        // LINE 线路ID 停站时间（秒）
                        string lineId2 = parser.ParseId();
                        double timeSec2 = parser.ParseTimeInSeconds();

                        if (!context.Lines.ContainsKey(lineId2))
                            throw new SyntaxException($"线路 '{lineId2}' 未定义", parser.Peek());

                        // 遍历该线路所有方向，批量设置停站时间
                        var directions = context.Directions.Values.Where(d => d.LineId == lineId2);
                        foreach (var dir in directions)
                        {
                            foreach (var st in dir.Stations)
                            {
                                context.DwellTimes[(st, lineId2)] = timeSec2;
                            }
                        }
                        break;

                    case "DIRECTION":
                        // DIRECTION 方向ID 停站时间（秒）
                        string dirId = parser.ParseId();
                        double timeSec3 = parser.ParseTimeInSeconds();

                        if (!context.Directions.TryGetValue(dirId, out var dirInfo))
                            throw new SyntaxException($"方向 '{dirId}' 未定义", parser.Peek());

                        // 仅针对该方向的站点设置停站时间
                        foreach (var st in dirInfo.Stations)
                        {
                            context.DwellTimes[(st, dirInfo.LineId)] = timeSec3;
                        }
                        break;

                    default:
                        throw new SyntaxException($"DWELL_TIME_DEFINE 内未知子命令 '{subCommand}'，允许的子命令：NODE、LINE、DIRECTION", parser.Peek());
                }
            }
        }
    }
}
