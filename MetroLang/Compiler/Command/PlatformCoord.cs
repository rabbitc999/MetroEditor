using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler.Command
{
    public class PlatformCoord : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string station = parser.ParseString();
            string lineId = parser.ParseId();
            parser.ParseId("COORD");
            double lon = parser.ParseDouble();
            if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == ",")
                parser.ParseSymbol(",");
            double lat = parser.ParseDouble();

            // 校验车站和线路存在
            if (!context.Stations.ContainsKey(station))
                throw new SyntaxException($"车站 '{station}' 未定义", parser.Peek());
            if (!context.Lines.ContainsKey(lineId))
                throw new SyntaxException($"线路 '{lineId}' 未定义", parser.Peek());

            context.PlatformCoords[(station, lineId)] = (lon, lat);
        }
    }
}
