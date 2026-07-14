using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // TRANSFER_DEFINE 解析：换乘规则定义
    // 定义两个线路在特定车站的换乘参数
    // 语法示例：
    // TRANSFER_DEFINE "西直门" LINE1 LINE2
    //   TIME 180
    //   STATUS operating
    //   SECURITY_CHECK
    // END_DEFINE
    // ====================================================================
    internal class TransferDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 解析车站名称和两条线路ID
            string station = parser.ParseString();
            string line1 = parser.ParseId();
            string line2 = parser.ParseId();
            var transferRule = new TransferRule
            {
                Station = station,
                L1 = line1,
                L2 = line2,
                Status = "operating"
            };

            // 解析可选子命令
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                string subCommand = parser.ParseId().ToUpperInvariant();
                switch (subCommand)
                {
                    case "TIME":
                        // 换乘耗时（秒）
                        transferRule.Time = parser.ParseDouble();
                        break;
                    case "STATUS":
                        // 换乘状态：operating/pending/out_station
                        string status = parser.ParseId().ToLowerInvariant();
                        if (!new[] { "operating", "pending", "out_station" }.Contains(status))
                            throw new SyntaxException($"无效状态 '{status}'", parser.Peek());
                        transferRule.Status = status;
                        break;
                    case "SECURITY_CHECK":
                        // 标记需要重新安检
                        transferRule.SecurityCheck = true;
                        break;
                    default:
                        throw new SyntaxException($"TRANSFER_DEFINE 内未知子命令 '{subCommand}'", parser.Peek());
                }
            }

            context.Transfers.Add(transferRule);
        }
    }
}
