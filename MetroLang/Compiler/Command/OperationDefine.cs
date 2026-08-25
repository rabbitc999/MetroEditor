using MetroEditor.MetroLang.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // OPERATION_DEFINE 解析：运营参数定义
    // 定义全局默认时间参数
    // 语法示例：
    // OPERATION_DEFINE
    //   TRANSFER_TIME 180
    //   DWELL_TIME 30
    //   OUT_STATION_TRANSFER_TIME 300
    //   ENTRY_TIME 5
    //   AVG_BOARD_TIME 3
    // END_DEFINE
    // ====================================================================
    internal class OperationDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                string command = parser.ParseId().ToUpperInvariant();
                double timeValue = parser.ParseTimeInSeconds();

                switch (command)
                {
                    case "TRANSFER_TIME":
                        context.DefaultTimes["in_station"] = timeValue;
                        break;
                    case "DWELL_TIME":
                        context.DefaultTimes["dwell"] = timeValue;
                        break;
                    case "OUT_STATION_TRANSFER_TIME":
                        context.DefaultTimes["out_station"] = timeValue;
                        break;
                    case "ENTRY_TIME":
                        context.DefaultTimes["entry"] = timeValue;
                        break;
                    case "AVG_BOARD_TIME":
                        context.DefaultTimes["board"] = timeValue;
                        break;
                    default:
                        throw new SyntaxException($"OPERATION_DEFINE 内未知子命令 '{command}'", parser.Peek());
                }
            }
        }
    }
}
