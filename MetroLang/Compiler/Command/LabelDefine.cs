using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // LABEL_DEFINE 解析：地图标注定义
    // 语法示例：
    // LABEL_DEFINE
    //   NODE '市中心' COORD 116.397, 39.908
    //   NODE '火车站' COORD 116.429, 39.902 CITY '北京'
    // END_DEFINE
    // ====================================================================
    internal class LabelDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                string subCommand = parser.ParseId().ToUpperInvariant();
                if (subCommand != "NODE")
                    throw new SyntaxException($"LABEL_DEFINE 内只能使用 NODE 子命令，但遇到了 '{subCommand}'", parser.Peek());

                string text = parser.ParseString();
                parser.ParseId("COORD");
                double longitude = parser.ParseDouble();

                // 可选逗号分隔符
                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == ",")
                    parser.ParseSymbol(",");

                double latitude = parser.ParseDouble();

                // 可选 CITY 属性（当前版本忽略，仅记录警告）
                if (parser.IsPeekId("CITY"))
                {
                    parser.ParseId();
                    string city = parser.ParseString();
                    parser.AddWarning($"LABEL_DEFINE 中 CITY 关键字被忽略（标签 '{text}' 位于 {city}）");
                }

                context.Labels.Add(new Label { Text = text, Lon = longitude, Lat = latitude });
            }
        }
    }
}
