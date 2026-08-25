using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // TRANSFER_TYPE 解析：换乘类型模板定义
    // 语法示例：
    // TRANSFER_TYPE walk_transfer "步行换乘"
    //   TIME 180
    //   STATUS operating
    //   SECURITY_CHECK
    // END_DEFINE
    // ====================================================================
    internal class TransferType : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string typeId = parser.ParseId();
            string typeName = parser.ParseString();
            var definition = new TransferTypeDefinition { Name = typeName };

            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                string command = parser.ParseId().ToUpperInvariant();
                switch (command)
                {
                    case "TIME":
                        // 换乘耗时（秒）
                        definition.Time = parser.ParseTimeInSeconds();
                        break;
                    case "STATUS":
                        string status = parser.ParseId().ToLowerInvariant();
                        if (!new[] { "operating", "pending", "out_station" }.Contains(status))
                            throw new SyntaxException($"无效状态 '{status}'", parser.Peek());
                        definition.Status = status;
                        break;
                    case "SECURITY_CHECK":
                        // 标记需要重新安检
                        definition.SecurityCheck = true;
                        break;
                    default:
                        throw new SyntaxException($"TRANSFER_TYPE 内未知子命令 '{command}'", parser.Peek());
                }
            }

            // 注册换乘类型模板
            context.TransferTypes[typeId] = definition;
        }
    }
}
