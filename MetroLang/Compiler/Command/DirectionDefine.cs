
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // DIRECTION_DEFINE 解析：方向定义（核心复杂逻辑）
    // 定义线路的一个运行方向，包含站点序列、距离、运营参数等
    // 支持两种定义方式：
    // 1. 完整定义：DIRECTION_DEFINE DIR_ID LINE_ID ... END_DEFINE
    // 2. 反向简写：DIRECTION_DEFINE NEW_DIR REV EXISTING_DIR
    // ====================================================================
    internal class DirectionDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string dirId = parser.ParseId();

            // ---------- 快速路径：REV 简写（直接复制并反转现有方向） ----------
            if (parser.IsPeekId("REV"))
            {
                parser.ParseId();
                string originalDirId = parser.ParseId();
                if (!context.Directions.ContainsKey(originalDirId))
                    throw new SyntaxException($"原方向 '{originalDirId}' 不存在", parser.Peek());

                var originalDir = context.Directions[originalDirId];

                // 创建新的方向信息，复制原方向的属性并反转站点列表
                var reversedStations = new List<string>(originalDir.Stations);
                reversedStations.Reverse();

                var newDirection = new DirectionInfo
                {
                    LineId = originalDir.LineId,
                    Stations = reversedStations,
                    Attrs = new HashSet<string>(originalDir.Attrs),
                    Alias = originalDir.Alias,
                    AliasColor = originalDir.AliasColor,
                    // 环线需要交换内外环标识
                    DirectionType = originalDir.Attrs.Contains("LOOP")
                        ? originalDir.DirectionType == "inner" ? "outer" : "inner"
                        : originalDir.DirectionType,
                    IsFuture = originalDir.IsFuture,
                    ConnectStation = originalDir.ConnectStation,
                    ViaDirections = new List<string>(originalDir.ViaDirections)
                };

                context.Directions[dirId] = newDirection;

                // 复制并反转距离列表
                if (context.Distances.TryGetValue(originalDirId, out var originalDistances))
                {
                    context.Distances[dirId] = Utils.ReverseDistanceList(originalDistances, originalDir.Attrs.Contains("LOOP"));
                }
                return; // REV 快捷方式不处理后续子命令
            }

            // ---------- 正常解析路径 ----------
            string lineId = parser.ParseId();
            if (!context.Lines.ContainsKey(lineId))
                throw new SyntaxException($"线路 '{lineId}' 未定义", parser.Peek());

            var dirInfo = new DirectionInfo { LineId = lineId };
            bool hasStations = false;
            bool hasAutoDistance = false;
            bool reversed = false;   // 标记是否已执行 REVERSE 子命令

            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                if (parser.Peek().TokenType != TokenType.Id)
                    throw new SyntaxException($"期望子命令，但遇到了 '{parser.Peek().Value}'", parser.Peek());

                string command = parser.ParseId().ToUpperInvariant();

                // ---------- 新增：REVERSE 子命令（在完整定义内部反转） ----------
                if (command == "REVERSE")
                {
                    if (reversed)
                        throw new SyntaxException($"方向 '{dirId}' 中 REVERSE 只能出现一次", parser.Peek());

                    string originalDirId = parser.ParseId();
                    if (!context.Directions.TryGetValue(originalDirId, out var originalDir))
                        throw new SyntaxException($"原方向 '{originalDirId}' 不存在", parser.Peek());

                    // 复制站点并反转
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
                    continue; // 继续解析后续子命令
                }

                // ---------- 其余子命令处理 ----------
                switch (command)
                {
                    case "STATIONS":
                        // 定义站点序列
                        dirInfo.Stations = parser.ParseStringList();
                        hasStations = true;
                        break;

                    case "DISTANCE":
                        // 定义站间距，支持手动指定或 AUTO 自动计算
                        if (parser.IsPeekId("AUTO"))
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
                        // 方向别名（如"往市区方向"）
                        dirInfo.Alias = parser.ParseString();
                        break;

                    case "NICK_COLOR":
                        // 别名显示颜色
                        dirInfo.AliasColor = parser.ParseString();
                        break;

                    case "LOOP":
                        // 标记为环线，可选指定 inner/outer
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

                    case "CROWD":
                        // 拥挤度规则：时间段 + 倍率
                        parser.ParseId("TIME_RANGE");
                        var (crowdStart, crowdEnd) = parser.ParseTimeRange();
                        if (parser.IsPeekId("VALUE"))
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

                    case "BRANCH":
                        // 标记为支线
                        dirInfo.Attrs.Add("BRANCH");
                        break;

                    case "FAST":
                        // 标记为快线
                        dirInfo.Attrs.Add("FAST");
                        break;

                    case "FUTURE":
                        // 标记为远期规划，默认灰色
                        dirInfo.IsFuture = true;
                        dirInfo.AliasColor = "#808080";
                        if (string.IsNullOrEmpty(dirInfo.Alias))
                            dirInfo.Alias = context.Lines[lineId].Name;
                        break;

                    case "SPEED_LIMIT":
                        // 区间限速
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
                        // 发车间隔规则
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

                    case "DWELL_TIME":
                        // 方向内特定站点停站时间
                        string dwellStation = parser.ParseString();
                        double dwellTime = parser.ParseTimeInSeconds();
                        context.DwellTimes[(dwellStation, lineId)] = dwellTime;
                        break;

                    case "OPEN_TIME":
                        // 方向运营时间
                        var (openStart, openEnd) = parser.ParseTimeRange();
                        context.OpenTimes.Add(new OpenTimeRule
                        {
                            LineId = lineId,
                            StartTime = openStart,
                            EndTime = openEnd
                        });
                        break;

                    case "VIA":
                        // 经由其他方向
                        string viaDirection = parser.ParseId();
                        dirInfo.ViaDirections.Add(viaDirection);
                        break;

                    case "DEPOT":
                        // 车辆段配置
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

                    default:
                        throw new SyntaxException($"DIRECTION_DEFINE 内未知子命令 '{command}'", parser.Peek());
                }
            }

            // 保存方向信息
            context.Directions[dirId] = dirInfo;

            // 处理AUTO距离计算
            if (hasAutoDistance)
                Utils.CalcAutoDistances(context, dirId);

            // 最终校验
            if (!hasStations)
                throw new SyntaxException($"方向 '{dirId}' 必须定义 STATIONS（或使用 REVERSE）", parser.Peek());
            if (!context.Distances.ContainsKey(dirId))
                parser.AddWarning($"方向 '{dirId}' 未定义 DISTANCE，将使用默认值（全部为 NIL）");
        }
    }
}
