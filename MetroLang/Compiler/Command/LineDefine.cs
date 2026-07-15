using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // LINE_DEFINE 解析：线路定义
    // 定义一条线路的基础属性，并可包含多个 DIRECTION_DEFINE 子块
    // ====================================================================
    internal class LineDefine : ICommandHandler
    {
        Parser parser;
        Context context;

        public void Handle(Parser parser, Context context)
        {
            this.parser = parser;
            this.context = context;

            string lineId = parser.ParseId();
            if (context.Lines.ContainsKey(lineId))
                context.Warnings.Add($"线路 '{lineId}' 已存在，将覆盖");

            // 创建线路信息，设置默认值
            var lineInfo = new LineInfo
            {
                Name = lineId,
                Color = "#000000",
                City = context.MainCity,
                OpenYear = 2000
            };
            context.Lines[lineId] = lineInfo;

            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                string command = parser.ParseId().ToUpperInvariant();
                switch (command)
                {
                    case "NAME":
                        lineInfo.Name = parser.ParseString();
                        break;
                    case "COLOR":
                        lineInfo.Color = parser.ParseString();
                        break;
                    case "CITY":
                        string city = parser.ParseString();
                        if (!context.Cities.ContainsKey(city))
                            throw new SyntaxException($"城市 '{city}' 未定义", parser.Peek());
                        lineInfo.City = city;
                        break;
                    case "LINK":
                        context.LineLinks[lineId] = parser.ParseString();
                        break;
                    case "OPEN_YEAR":
                        lineInfo.OpenYear = parser.ParseInt();
                        break;
                    case "FORMATION":
                        lineInfo.Formation = parser.ParseString();
                        break;
                    case "MAXSPEED":
                        lineInfo.Maxspeed = parser.ParseDouble();
                        break;
                    case "POWER":
                        lineInfo.PowerKw = parser.ParseDouble();
                        break;
                    case "BRAKE":
                        lineInfo.Brake = parser.ParseDouble();
                        break;
                    case "DISABLE":
                        lineInfo.Disabled = true;
                        break;
                    case "SPECIAL":
                        context.SpecialLineIds.Add(lineId);
                        break;
                    case "PENDING":
                        // 标记线路下特定站点为待开通状态
                        var pendingStations = ParseStationListOrSingle();
                        foreach (var station in pendingStations)
                        {
                            if (!context.Stations.ContainsKey(station))
                                throw new SyntaxException($"站台 '{station}' 未定义", parser.Peek());
                            context.StationStatus[(station, lineId)] = "pending";
                        }
                        break;
                    case "DEPOT":
                        // 线路级车辆段定义（无特定方向）
                        string depotStation = parser.ParseString();
                        string directionKey = null;
                        if (parser.Peek().TokenType == TokenType.Id)
                            directionKey = parser.ParseId();
                        TimeSpan? departureTime = null;
                        if (parser.Peek().TokenType == TokenType.Number)
                            departureTime = parser.ParseClockTime();
                        context.Depots.Add(new DepotInfo
                        {
                            LineId = lineId,
                            Station = depotStation,
                            DirectionKey = directionKey,
                            DepartureTime = departureTime
                        });
                        break;
                    case "DIRECTION_DEFINE":
                        // 解析嵌套的方向定义（旧语法兼容）
                        ParseDirectionSubDefine(lineId);
                        break;
                    case "SPECIAL_DEFINE":
                        // 特殊线路定义（如机场线）
                        context.SpecialLineIds.Add(lineId);
                        ParseSpecialSubDefine(lineId);
                        break;
                    default:
                        throw new SyntaxException($"LINE_DEFINE 内未知子命令 '{command}'", parser.Peek());
                }
            }

            // 校验必填项
            if (string.IsNullOrEmpty(lineInfo.Color))
                throw new SyntaxException($"线路 '{lineId}' 必须定义 COLOR", parser.Peek());

            context.Lines[lineId] = lineInfo;
        }

        // ====================================================================
        // 车站列表解析：支持单站或数组格式
        // ====================================================================
        private List<string> ParseStationListOrSingle()
        {
            if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                return parser.ParseStringList();
            else
                return new List<string> { parser.ParseString() };
        }

        // ====================================================================
        // 嵌套方向定义解析（旧语法兼容）
        // 用于 LINE_DEFINE 内部的 DIRECTION_DEFINE 子块
        // ====================================================================
        private void ParseDirectionSubDefine(string lineId)
        {
            // 读取方向ID（嵌套语法中，LINE_ID 继承自外层）
            string dirId = parser.ParseId();

            // 委托给统一解析器，显式传入线路ID
            var dirInfo = DirectionParser.Parse(parser, context, dirId, explicitLineId: lineId);

            // 存入上下文
            context.Directions[dirId] = dirInfo;
        }

        // ====================================================================
        // SPECIAL_DEFINE 解析：特殊线路票价定义
        // 用于定义机场线、磁悬浮等特殊票价的线路
        // ====================================================================
        private void ParseSpecialSubDefine(string lineId)
        {
            // 确保线路已标记为特殊线路
            if (!context.SpecialLineIds.Contains(lineId))
                context.SpecialLineIds.Add(lineId);

            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsPeekId("END_DEFINE"))
                {
                    parser.ParseId();
                    break;
                }

                string subCommand = parser.ParseId().ToUpperInvariant();
                if (subCommand == "ALL")
                {
                    // 全线统一票价
                    double price = parser.ParseDouble();
                    Utils.AddSpecialFare(context, lineId, "all", price);
                }
                else if (subCommand == "SEGMENT")
                {
                    // 特定区间附加费
                    string stationA = parser.ParseString();
                    string stationB = parser.ParseString();
                    double price = parser.ParseDouble();
                    Utils.AddSpecialFare(context, lineId, "segment", price, stationA, stationB);
                }
                else
                {
                    throw new SyntaxException($"SPECIAL_DEFINE 内只允许 ALL 或 SEGMENT 子命令，但遇到了 '{subCommand}'", parser.Peek());
                }
            }
        }
    }
}
