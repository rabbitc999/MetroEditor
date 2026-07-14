using MetroEditor.MetroLang.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // STATION_DEFINE 解析：车站坐标定义
    // 语法示例：
    // STATION_DEFINE
    //   NODE "北京南站" COORD 116.378, 39.865 CITY "北京"
    //   NODE "上海虹桥" COORD 121.320, 31.197
    // END_DEFINE
    // ====================================================================
    internal class StationDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                string subCommand = parser.ParseId().ToUpperInvariant();
                if (subCommand != "NODE")
                    throw new SyntaxException($"STATION_DEFINE 内只能使用 NODE 子命令，但遇到了 '{subCommand}'", parser.Peek());

                // 解析车站名称和坐标
                string stationName = parser.ParseString();
                parser.ParseId("COORD");
                double longitude = parser.ParseDouble();
                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == ",")
                    parser.ParseSymbol(",");
                double latitude = parser.ParseDouble();

                // 可选的 CITY 属性
                string city = null;
                if (parser.IsPeekId("CITY"))
                {
                    parser.ParseId();
                    city = parser.ParseString();
                }

                // 记录车站坐标，重复定义时发出警告
                if (context.Stations.ContainsKey(stationName))
                    parser.AddWarning($"站台 '{stationName}' 已存在，将覆盖");
                context.Stations[stationName] = (longitude, latitude);

                // 记录车站所属城市
                if (!string.IsNullOrEmpty(city))
                    context.StationCity[stationName] = city;
            }
        }
    }
}
