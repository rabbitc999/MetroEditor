using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // TRANSFER 解析：换乘模板实例化
    // 使用预定义的换乘类型模板创建具体换乘规则
    // 语法示例：TRANSFER walk_transfer "西直门" LINE1 LINE2
    // ====================================================================
    internal class Transfer : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string typeId = parser.ParseId();
            if (!context.TransferTypes.TryGetValue(typeId, out var transferDefinition))
                throw new SyntaxException($"换乘类型 '{typeId}' 未定义", parser.Peek());

            string station = parser.ParseString();
            string line1 = parser.ParseId();

            // 检查是否有第二个线路参数
            if (parser.Peek().TokenType == TokenType.Id && !parser.Peek().Value.Equals("END"))
            {
                string line2 = parser.ParseId();
                // 直接生成具体换乘规则
                context.Transfers.Add(new TransferRule
                {
                    Station = station,
                    L1 = line1,
                    L2 = line2,
                    Status = transferDefinition.Status,
                    Time = transferDefinition.Time,
                    SecurityCheck = transferDefinition.SecurityCheck,
                    TransferType = transferDefinition.Name
                });
            }
            else
            {
                // 只有一条线路 → 创建通配模板，后续展开
                context.TransferTemplates.Add(new TransferTemplate
                {
                    TypeId = typeId,
                    Station = station,
                    LineId = line1
                });
            }
        }
    }
}
