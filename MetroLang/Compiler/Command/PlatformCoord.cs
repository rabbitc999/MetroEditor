using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>
    /// 定义指定车站在线路上的站台坐标。
    /// </summary>
    public class PlatformCoord : ICommandHandler
    {
        /// <summary>
        /// 读取站名、线路 ID 以及 COORD 后的经纬度并写入上下文索引。
        /// </summary>
        public void Handle(Parser parser, Context context)
        {
            string station = parser.ParseString();
            string lineId = parser.ParseId();
            parser.ParseId("COORD");
            double lon = parser.ParseDouble();
            if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == ",")
                parser.ParseSymbol(",");
            double lat = parser.ParseDouble();

            // 校验车站和线路存在，避免创建无法关联到实体的坐标记录。
            if (!context.Stations.ContainsKey(station))
                throw new SyntaxException($"车站 '{station}' 未定义", parser.Peek());
            if (!context.Lines.ContainsKey(lineId))
                throw new SyntaxException($"线路 '{lineId}' 未定义", parser.Peek());

            context.PlatformCoords[(station, lineId)] = (lon, lat);
        }
    }
}
