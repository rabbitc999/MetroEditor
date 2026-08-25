using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // VIRTUAL_TRANSFER_DEFINE 解析：虚拟换乘规则定义
    // 定义两个车站之间的虚拟换乘（出站换乘/虚拟换乘）
    // 语法示例：
    // VIRTUAL_TRANSFER_DEFINE "火车站A" "火车站B"
    //   TIME_MINUTES 15
    //   SECURITY_CHECK
    //   VIA_DIRECTIONS ["LINE1_0", "LINE2_1"]
    // END_DEFINE
    // ====================================================================
    internal class VirtualTransferDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 解析两个车站名称
            string stationA = parser.ParseString();
            string stationB = parser.ParseString();
            var virtualTransferRule = new VirtualTransferRule
            {
                StationA = stationA,
                StationB = stationB
            };

            // 解析可选子命令
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                string subCommand = parser.ParseId().ToUpperInvariant();
                switch (subCommand)
                {
                    case "TIME_MINUTES":
                        // 换乘耗时（分钟）
                        virtualTransferRule.TimeMinutes = parser.ParseDouble();
                        break;
                    case "SECURITY_CHECK":
                        // 标记需要重新安检
                        virtualTransferRule.SecurityCheck = true;
                        break;
                    case "VIA_DIRECTIONS":
                        // 换乘必经方向列表
                        virtualTransferRule.ViaDirections = parser.ParseStringList();
                        break;
                    default:
                        throw new SyntaxException($"VIRTUAL_TRANSFER_DEFINE 内未知子命令 '{subCommand}'", parser.Peek());
                }
            }

            context.VirtualTransfers.Add(virtualTransferRule);
        }
    }
}
