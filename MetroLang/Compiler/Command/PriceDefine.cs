using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // PRICE_DEFINE 解析：票价规则定义
    // 支持三种模式：
    // 1. 全局票价（无修饰符）
    // 2. 城市级票价（CITY 修饰符）
    // 3. 线路组票价（LINE 修饰符）
    // ====================================================================
    internal class PriceDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // ---------- 检测限定符（城市或线路组） ----------
            string? targetCity = null;
            List<string> targetLines = new List<string>();

            if (parser.IsPeekId("CITY"))
            {
                parser.ParseId();
                targetCity = parser.ParseString();
                if (!context.Cities.ContainsKey(targetCity))
                    throw new SyntaxException($"城市 '{targetCity}' 未定义", parser.Peek());
                // 获取该城市所有线路
                targetLines = context.Lines.Where(kv => kv.Value.City == targetCity).Select(kv => kv.Key).ToList();
            }
            else if (parser.IsPeekId("LINE"))
            {
                parser.ParseId();
                // 支持单个线路或列表
                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                    targetLines = parser.ParseLineIdList();
                else
                    targetLines.Add(parser.ParseId());

                // 校验线路是否存在
                foreach (var lineId in targetLines)
                    if (!context.Lines.ContainsKey(lineId))
                        throw new SyntaxException($"线路 '{lineId}' 未定义", parser.Peek());
            }

            // ---------- 如果有线路组，解析完整票价配置（特殊票价模式） ----------
            if (targetLines.Count > 0)
            {
                var specialLine = new SpecialLine
                {
                    UseLines = targetLines,
                    Segments = new List<Segment>(),
                    LinearExtension = null
                };

                while (true)
                {
                    parser.ParseNewlines();
                    if (parser.IsEndDefine()) { break; }

                    string command = parser.ParseId().ToUpperInvariant();
                    switch (command)
                    {
                        case "DEFAULT_FARE":
                            specialLine.DefaultFare = parser.ParseDouble();
                            break;
                        case "SEGMENT":
                            double start = parser.ParseDouble();
                            double end = parser.ParseDouble();
                            double fare = parser.ParseDouble();
                            specialLine.Segments.Add(new Segment
                            {
                                RangeStart = start,
                                RangeEnd = end,
                                Fare = fare
                            });
                            break;
                        case "LINEAR_EXTENSION":
                            parser.ParseId("THRESHOLD");
                            double threshold = parser.ParseDouble();
                            parser.ParseId("BASE_FARE");
                            double baseFare = parser.ParseDouble();
                            parser.ParseId("RATE_PER_KM");
                            double rate = parser.ParseDouble();
                            specialLine.LinearExtension = new LinearExtension
                            {
                                Threshold = threshold,
                                BaseFare = baseFare,
                                RatePerKm = rate
                            };
                            break;
                        default:
                            throw new SyntaxException($"特殊票价块内未知子命令 '{command}'", parser.Peek());
                    }
                }

                // 将特殊票价添加到配置
                context.PriceConfig.SpecialLines.Add(specialLine);
                // 标记这些线路为特殊线路
                foreach (var lineId in targetLines)
                    context.SpecialLineIds.Add(lineId);

                return;
            }

            // ---------- 普通票价模式（无限定符，全局配置） ----------

            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsPeekId("END_DEFINE"))
                {
                    parser.ParseId();
                    break;
                }

                string command = parser.ParseId().ToUpperInvariant();
                switch (command)
                {
                    case "CURRENCY":
                        context.PriceConfig.Currency = parser.ParseString();
                        break;
                    case "DEFAULT_FARE":
                        context.PriceConfig.DefaultFare = parser.ParseDouble();
                        break;
                    case "SEGMENT":
                        double start = parser.ParseDouble();
                        double end = parser.ParseDouble();
                        double fare = parser.ParseDouble();
                        context.PriceConfig.Segments.Add(new Segment
                        {
                            RangeStart = start,
                            RangeEnd = end,
                            Fare = fare
                        });
                        break;
                    case "LINEAR_EXTENSION":
                        parser.ParseId("THRESHOLD");
                        double threshold = parser.ParseDouble();
                        parser.ParseId("BASE_FARE");
                        double baseFare = parser.ParseDouble();
                        parser.ParseId("RATE_PER_KM");
                        double rate = parser.ParseDouble();
                        context.PriceConfig.LinearExtension = new LinearExtension
                        {
                            Threshold = threshold,
                            BaseFare = baseFare,
                            RatePerKm = rate
                        };
                        break;
                    case "TRANSFER_DISCOUNT":
                        bool enabled = true;
                        if (parser.IsPeekId("ENABLED"))
                        {
                            parser.ParseId();
                            if (parser.Peek().TokenType == TokenType.Id &&
                               (parser.Peek().Value.Equals("TRUE") ||
                                parser.Peek().Value.Equals("FALSE")))
                                enabled = bool.Parse(parser.ParseId().ToLowerInvariant());
                        }
                        parser.ParseId("DISCOUNT_AMOUNT");
                        double discount = parser.ParseDouble();
                        parser.ParseId("TIME_LIMIT");
                        int timeLimit = parser.ParseInt();
                        context.PriceConfig.TransferDiscount = new TransferDiscount
                        {
                            Enabled = enabled,
                            DiscountAmount = discount,
                            TimeLimitMinutes = timeLimit
                        };
                        break;
                    case "FARE_CAP":
                        context.PriceConfig.FareCap = parser.ParseDouble();
                        break;
                    case "SPECIAL_LINE":
                        // 普通票价块内也可以定义特殊票价（针对单条线路）
                        string lineId = parser.ParseId();
                        if (!context.Lines.ContainsKey(lineId))
                            throw new SyntaxException($"线路 '{lineId}' 未定义", parser.Peek());
                        string type = parser.ParseId().ToUpperInvariant();
                        if (type == "ALL")
                        {
                            double allPrice = parser.ParseDouble();
                            context.SpecialLineIds.Add(lineId);
                            Utils.AddSpecialFare(context, lineId, "all", allPrice);
                        }
                        else if (type == "SEGMENT")
                        {
                            string stationA = parser.ParseString();
                            string stationB = parser.ParseString();
                            double segmentPrice = parser.ParseDouble();
                            context.SpecialLineIds.Add(lineId);
                            Utils.AddSpecialFare(context, lineId, "segment", segmentPrice, stationA, stationB);
                        }
                        else
                            throw new SyntaxException($"SPECIAL_LINE 类型只能是 ALL 或 SEGMENT", parser.Peek());
                        break;
                    default:
                        throw new SyntaxException($"PRICE_DEFINE 内未知子命令 '{command}'", parser.Peek());
                }
            }
        }
    }
}
