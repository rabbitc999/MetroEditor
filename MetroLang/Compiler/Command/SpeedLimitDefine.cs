using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // SPEED_LIMIT_DEFINE 解析：区间限速定义
    // 语法示例：
    // SPEED_LIMIT_DEFINE LINE1_0
    //   NODE '苹果园' '古城' 60
    //   NODE '古城' '八角游乐园' 80
    // END_DEFINE
    // ====================================================================
    internal class SpeedLimitDefine : ICommandHandler
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

                string subCommand = parser.ParseId().ToUpperInvariant();
                if (subCommand != "NODE")
                    throw new SyntaxException($"SPEED_LIMIT_DEFINE 内只能使用 NODE 子命令，但遇到了 '{subCommand}'", parser.Peek());

                string fromStation = parser.ParseString();
                string toStation = parser.ParseString();
                int speedLimit = parser.ParseInt();

                context.SpeedLimits.Add(new SpeedLimit
                {
                    DirectionKey = Utils.ConvertToExternalDirKey(context, directionKey),
                    FromStation = fromStation,
                    ToStation = toStation,
                    SpeedKmh = speedLimit
                });
            }
        }
    }
}
