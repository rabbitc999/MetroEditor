using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // PROMOTION_DEFINE 解析：票价优惠策略配置
    // 语法示例：
    // PROMOTION_DEFINE
    //   POLICY student_card
    //     NAME '学生优惠'
    //     DESCRIPTION '全日制在校学生凭学生卡享受5折优惠'
    //     TYPE rate
    //     VALUE 0.5
    //     APPLICABLE_LINES [LINE1, LINE2]
    //     TIME_RANGE 06:00 09:00
    //     DAYS WORKDAY
    //   END_POLICY
    // END_DEFINE
    // ====================================================================
    internal class PromotionDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                // 解析 POLICY 定义
                parser.ParseId("POLICY");
                string policyId = parser.ParseId();
                var policy = new PromotionPolicy { Id = policyId };

                // 解析 POLICY 内部属性
                while (true)
                {
                    parser.ParseNewlines();

                    // 遇到 END_POLICY 结束当前策略
                    if (parser.IsPeekId("END_POLICY"))
                    {
                        parser.ParseId();
                        break;
                    }

                    string subCommand = parser.ParseId().ToUpperInvariant();
                    switch (subCommand)
                    {
                        case "NAME":
                            policy.Name = parser.ParseString();
                            break;
                        case "DESCRIPTION":
                            policy.Description = parser.ParseString();
                            break;
                        case "TYPE":
                            string type = parser.ParseId().ToLowerInvariant();
                            if (type != "rate" && type != "fixed" && type != "fixed_discount")
                                throw new SyntaxException($"TYPE 只能是 'rate'、'fixed' 或 'fixed_discount'，但得到 '{type}'", parser.Peek());
                            policy.Type = type;
                            break;
                        case "VALUE":
                            policy.Value = parser.ParseDouble();
                            break;
                        case "APPLICABLE_LINES":
                            // 解析适用线路列表（支持数组或单个值）
                            policy.ApplicableLines = parser.ParseLineIdList();
                            break;
                        case "TIME_RANGE":
                            // 解析时间范围，转换为 HH:mm 格式字符串
                            var (startTime, endTime) = parser.ParseTimeRange();
                            policy.StartTime = startTime.ToString(@"hh\:mm");
                            policy.EndTime = endTime.ToString(@"hh\:mm");
                            break;
                        case "DAYS":
                            // 解析星期集合（支持数组或单个值）
                            if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                                policy.Days = parser.ParseDayOfWeekList().Select(d => d.ToString()).ToList();
                            else
                                policy.Days = new List<string> { parser.ParseId().ToUpperInvariant() };
                            break;
                        default:
                            throw new SyntaxException($"POLICY 内未知子命令 '{subCommand}'", parser.Peek());
                    }
                }

                // 将解析完成的策略添加到全局列表
                context.Promotions.Add(policy);
            }
        }
    }
}
