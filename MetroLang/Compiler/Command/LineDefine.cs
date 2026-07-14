
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
            // 1. 读取方向 ID
            string dirId = parser.ParseId();

            // 2. 快速路径：REV 简写
            if (parser.Peek().TokenType == TokenType.Id &&
                parser.Peek().Value.Equals("REV"))
            {
                parser.ParseId(); // 消耗 REV
                string originalDirId = parser.ParseId();
                if (!context.Directions.ContainsKey(originalDirId))
                    throw new SyntaxException($"原方向 '{originalDirId}' 不存在", parser.Peek());

                var originalDir = context.Directions[originalDirId];
                var reversedStations = new List<string>(originalDir.Stations);
                reversedStations.Reverse();

                var newDirection = new DirectionInfo
                {
                    LineId = lineId,
                    Stations = reversedStations,
                    Attrs = new HashSet<string>(originalDir.Attrs),
                    Alias = originalDir.Alias,
                    AliasColor = originalDir.AliasColor,
                    DirectionType = originalDir.Attrs.Contains("LOOP")
                        ? originalDir.DirectionType == "inner" ? "outer" : "inner"
                        : originalDir.DirectionType,
                    IsFuture = originalDir.IsFuture,
                    ConnectStation = originalDir.ConnectStation,
                    ViaDirections = new List<string>(originalDir.ViaDirections)
                };

                context.Directions[dirId] = newDirection;

                // 反转距离
                if (context.Distances.TryGetValue(originalDirId, out var originalDistances))
                {
                    context.Distances[dirId] = Utils.ReverseDistanceList(originalDistances, originalDir.Attrs.Contains("LOOP"));
                }

                return; // 快速路径结束
            }

            // 3. 正常解析路径
            var dirInfo = new DirectionInfo { LineId = lineId };
            bool hasStations = false;
            bool hasAutoDistance = false;
            bool reversed = false; // 标记是否已通过 REVERSE 子命令复制数据

            // 4. 循环读取子命令
            while (true)
            {
                parser.ParseNewlines();

                // 检查是否结束
                if (parser.Peek().TokenType == TokenType.Id &&
                    parser.Peek().Value.Equals("END_DEFINE"))
                {
                    parser.ParseId();
                    break;
                }

                // 读取子命令名称
                if (parser.Peek().TokenType != TokenType.Id)
                    throw new SyntaxException($"期望子命令，但遇到了 '{parser.Peek().Value}'", parser.Peek());

                string command = parser.ParseId().ToUpperInvariant();

                switch (command)
                {
                    // ========== REVERSE 子命令 ==========
                    case "REVERSE":
                        if (reversed)
                            throw new SyntaxException($"方向 '{dirId}' 中 REVERSE 只能出现一次", parser.Peek());

                        string originalDirId = parser.ParseId();
                        if (!context.Directions.TryGetValue(originalDirId, out var originalDir))
                            throw new SyntaxException($"原方向 '{originalDirId}' 不存在", parser.Peek());

                        // 复制并反转站点
                        var reversedStations = new List<string>(originalDir.Stations);
                        reversedStations.Reverse();
                        dirInfo.Stations = reversedStations;
                        dirInfo.Attrs = new HashSet<string>(originalDir.Attrs);
                        dirInfo.Alias = originalDir.Alias;
                        dirInfo.AliasColor = originalDir.AliasColor;
                        dirInfo.DirectionType = originalDir.Attrs.Contains("LOOP")
                            ? originalDir.DirectionType == "inner" ? "outer" : "inner"
                            : originalDir.DirectionType;
                        dirInfo.IsFuture = originalDir.IsFuture;
                        dirInfo.ConnectStation = originalDir.ConnectStation;
                        dirInfo.ViaDirections = new List<string>(originalDir.ViaDirections);
                        hasStations = true;
                        reversed = true;

                        // 复制并反转距离
                        if (context.Distances.TryGetValue(originalDirId, out var originalDistances))
                        {
                            context.Distances[dirId] = Utils.ReverseDistanceList(originalDistances, originalDir.Attrs.Contains("LOOP"));
                        }
                        break;

                    // ========== 标准子命令 ==========
                    case "STATIONS":
                        dirInfo.Stations = parser.ParseStringList();
                        hasStations = true;
                        break;

                    case "DISTANCE":
                        if (parser.Peek().TokenType == TokenType.Id &&
                            parser.Peek().Value.Equals("AUTO"))
                        {
                            parser.ParseId();
                            hasAutoDistance = true;
                        }
                        else
                        {
                            var distances = parser.ParseNumberList();
                            context.Distances[dirId] = distances;
                        }
                        break;

                    case "NICK_NAME":
                        dirInfo.Alias = parser.ParseString();
                        break;

                    case "NICK_COLOR":
                        dirInfo.AliasColor = parser.ParseString();
                        break;

                    case "LOOP":
                        dirInfo.Attrs.Add("LOOP");
                        if (parser.Peek().TokenType == TokenType.Id)
                        {
                            string subType = parser.Peek().Value.ToUpperInvariant();
                            if (subType == "INNER" || subType == "OUTER")
                            {
                                parser.ParseId();
                                dirInfo.DirectionType = subType.ToLowerInvariant();
                            }
                        }
                        break;

                    case "BRANCH":
                        dirInfo.Attrs.Add("BRANCH");
                        break;

                    case "FAST":
                        dirInfo.Attrs.Add("FAST");
                        break;

                    case "FUTURE":
                        dirInfo.IsFuture = true;
                        dirInfo.AliasColor = "#808080";
                        if (string.IsNullOrEmpty(dirInfo.Alias))
                            dirInfo.Alias = context.Lines[lineId].Name;
                        break;

                    case "SPEED_LIMIT":
                        string fromStation = parser.ParseString();
                        string toStation = parser.ParseString();
                        int speed = parser.ParseInt();
                        context.SpeedLimits.Add(new SpeedLimit
                        {
                            DirectionKey = dirId,
                            FromStation = fromStation,
                            ToStation = toStation,
                            SpeedKmh = speed
                        });
                        break;

                    case "HEADWAY":
                        parser.ParseId("TIME_RANGE");
                        var (headwayStart, headwayEnd) = parser.ParseTimeRange();
                        parser.ParseId("VALUE");
                        int interval = (int)parser.ParseTimeInSeconds();

                        List<DayOfWeek> headwayDays = new();
                        if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                            headwayDays = parser.ParseDayOfWeekList();
                        else if (parser.Peek().TokenType == TokenType.Id)
                            headwayDays = parser.ParseDayOfWeekModifier();

                        context.HeadwayRules.Add(new HeadwayRule
                        {
                            LineId = lineId,
                            StartTime = headwayStart,
                            EndTime = headwayEnd,
                            IntervalSeconds = interval,
                            Days = headwayDays
                        });
                        break;

                    case "CROWD":
                        parser.ParseId("TIME_RANGE");
                        var (crowdStart, crowdEnd) = parser.ParseTimeRange();
                        if (parser.Peek().TokenType == TokenType.Id &&
                            parser.Peek().Value.Equals("VALUE"))
                            parser.ParseId();
                        double factor = parser.ParseDouble();

                        List<DayOfWeek> crowdDays = new();
                        if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                            crowdDays = parser.ParseDayOfWeekList();
                        else if (parser.Peek().TokenType == TokenType.Id)
                            crowdDays = parser.ParseDayOfWeekModifier();

                        context.CrowdRules.Add(new CrowdRule
                        {
                            DirectionKey = dirId,
                            StartTime = crowdStart,
                            EndTime = crowdEnd,
                            Factor = factor,
                            Days = crowdDays
                        });
                        break;

                    case "DEPOT":
                        string depotStation = parser.ParseString();
                        TimeSpan? departureTime = null;
                        if (parser.Peek().TokenType == TokenType.Number)
                            departureTime = parser.ParseClockTime();
                        context.Depots.Add(new DepotInfo
                        {
                            LineId = lineId,
                            Station = depotStation,
                            DirectionKey = dirId,
                            DepartureTime = departureTime
                        });
                        break;

                    case "DWELL_TIME":
                        string dwellStation = parser.ParseString();
                        double dwellTime = parser.ParseTimeInSeconds();
                        context.DwellTimes[(dwellStation, lineId)] = dwellTime;
                        break;

                    case "OPEN_TIME":
                        var (openStart, openEnd) = parser.ParseTimeRange();
                        context.OpenTimes.Add(new OpenTimeRule
                        {
                            LineId = lineId,
                            StartTime = openStart,
                            EndTime = openEnd
                        });
                        break;

                    case "VIA":
                        string viaDirection = parser.ParseId();
                        dirInfo.ViaDirections.Add(viaDirection);
                        break;

                    default:
                        throw new SyntaxException($"DIRECTION_DEFINE 内未知子命令 '{command}'", parser.Peek());
                }
            }

            // 5. 保存方向信息
            context.Directions[dirId] = dirInfo;

            // 6. 处理 AUTO 距离
            if (hasAutoDistance)
                Utils.CalcAutoDistances(context, dirId);

            // 7. 最终校验
            if (!hasStations)
                throw new SyntaxException($"方向 '{dirId}' 必须定义 STATIONS（或使用 REVERSE）", parser.Peek());
            if (!context.Distances.ContainsKey(dirId))
                context.Warnings.Add($"方向 '{dirId}' 未定义 DISTANCE，将使用默认值（全部为 NIL）");
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
