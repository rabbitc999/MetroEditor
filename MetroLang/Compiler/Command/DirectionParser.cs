using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{


    /// <summary>
    /// 方向解析器——统一处理顶层和嵌套的 DIRECTION_DEFINE 语法
    /// 消除 DirectionDefine 和 LineDefine 之间的重复代码
    /// </summary>
    public static class DirectionParser
    {
        // ====================================================================
        // 常量定义（消除硬编码字符串）
        // ====================================================================
        private static class Attr
        {
            public const string Loop = "LOOP";
            public const string Branch = "BRANCH";
            public const string Fast = "FAST";
            public const string Future = "FUTURE";
            public const string Inner = "INNER";
            public const string Outer = "OUTER";
        }

        // ====================================================================
        // 公开入口：解析一个完整的方向定义
        // ====================================================================
        public static DirectionInfo Parse(
            Parser parser,
            Context context,
            string dirId,
            string? explicitLineId = null)  // 嵌套模式传入线路ID，顶层模式从Token流读取
        {
            // ---------- 快速路径：REV 简写 ----------
            if (parser.IsPeekId("REV"))
            {
                parser.ParseId();
                string originalDirId = parser.ParseId();
                return CreateReversedDirection(context, dirId, originalDirId);
            }

            // ---------- 正常路径 ----------
            // 如果未显式传入线路ID，则从Token流读取
            string lineId = explicitLineId ?? parser.ParseId();
            if (!context.Lines.ContainsKey(lineId))
                throw new SyntaxException($"线路 '{lineId}' 未定义", parser.Peek());

            var dirInfo = new DirectionInfo { LineId = lineId };

            // 解析主体内容（所有子命令）
            ParseBody(parser, context, dirInfo, lineId, dirId);

            return dirInfo;
        }

        // ====================================================================
        // 解析方向主体（STATIONS / LOOP / BRANCH / ...）
        // 这是原来重复的核心循环，现在只写一次
        // ====================================================================
        private static void ParseBody(
            Parser parser,
            Context context,
            DirectionInfo dirInfo,
            string lineId,
            string dirId)
        {
            bool hasStations = false;
            bool hasAutoDistance = false;
            bool reversed = false;

            while (true)
            {
                parser.ParseNewlines();

                // 检查结束标记（END_DEFINE）
                if (parser.IsPeekId("END_DEFINE"))
                {
                    parser.ParseId();
                    break;
                }

                if (parser.Peek().TokenType != TokenType.Id)
                    throw new SyntaxException($"期望子命令，但遇到了 '{parser.Peek().Value}'", parser.Peek());

                string command = parser.ParseId().ToUpperInvariant();

                // ---------- 分发给具体的解析方法 ----------
                switch (command)
                {
                    case "REVERSE":
                        ParseReverse(parser, context, dirInfo, dirId, ref reversed);
                        hasStations = true;
                        break;

                    case "STATIONS":
                        dirInfo.Stations = parser.ParseStringList();
                        hasStations = true;
                        break;

                    case "DISTANCE":
                        hasAutoDistance = ParseDistance(parser, context, dirId);
                        break;

                    case "NICK_NAME":
                        dirInfo.Alias = parser.ParseString();
                        break;

                    case "NICK_COLOR":
                        dirInfo.AliasColor = parser.ParseString();
                        break;

                    case "LOOP":
                        ParseLoop(parser, dirInfo);
                        break;

                    case "BRANCH":
                        dirInfo.Attrs.Add(Attr.Branch);
                        break;

                    case "FAST":
                        dirInfo.Attrs.Add(Attr.Fast);
                        break;

                    case "FUTURE":
                        ParseFuture(parser, context, dirInfo, lineId);
                        break;

                    case "SPEED_LIMIT":
                        ParseSpeedLimit(parser, context, dirId);
                        break;

                    case "HEADWAY":
                        ParseHeadway(parser, context, lineId);
                        break;

                    case "CROWD":
                        ParseCrowd(parser, context, dirId);
                        break;

                    case "DWELL_TIME":
                        ParseDwellTime(parser, context, lineId);
                        break;

                    case "OPEN_TIME":
                        ParseOpenTime(parser, context, lineId);
                        break;

                    case "VIA":
                        ParseVia(parser, dirInfo);
                        break;

                    case "DEPOT":
                        ParseDepot(parser, context, lineId, dirId);
                        break;

                    default:
                        throw new SyntaxException($"DIRECTION_DEFINE 内未知子命令 '{command}'", parser.Peek());
                }
            }

            // ---------- 后置校验 ----------
            if (!hasStations)
                throw new SyntaxException($"方向 '{dirId}' 必须定义 STATIONS（或使用 REVERSE）", parser.Peek());

            context.Directions[dirId] = dirInfo;

            // 处理 AUTO 距离
            if (hasAutoDistance)
                Utils.CalcAutoDistances(context, dirId);

            // 警告：未定义 DISTANCE
            if (!context.Distances.ContainsKey(dirId))
                parser.AddWarning($"方向 '{dirId}' 未定义 DISTANCE，将使用默认值");
        }

        // ====================================================================
        // 子命令解析方法（拆分为独立小方法）
        // ====================================================================

        /// <summary>处理 REVERSE 子命令</summary>
        private static void ParseReverse(Parser parser, Context context, DirectionInfo dirInfo, string dirId, ref bool reversed)
        {
            if (reversed)
                throw new SyntaxException($"方向 '{dirId}' 中 REVERSE 只能出现一次", parser.Peek());

            string originalDirId = parser.ParseId();
            if (!context.Directions.TryGetValue(originalDirId, out var originalDir))
                throw new SyntaxException($"原方向 '{originalDirId}' 不存在", parser.Peek());

            // 复制并反转站点
            dirInfo.Stations = new List<string>(originalDir.Stations);
            dirInfo.Stations.Reverse();
            dirInfo.Attrs = new HashSet<string>(originalDir.Attrs);
            dirInfo.Alias = originalDir.Alias;
            dirInfo.AliasColor = originalDir.AliasColor;
            dirInfo.DirectionType = originalDir.Attrs.Contains(Attr.Loop)
                ? originalDir.DirectionType == "inner" ? "outer" : "inner"
                : originalDir.DirectionType;
            dirInfo.IsFuture = originalDir.IsFuture;
            dirInfo.ConnectStation = originalDir.ConnectStation;
            dirInfo.ViaDirections = new List<string>(originalDir.ViaDirections);

            // 反转距离列表
            if (context.Distances.TryGetValue(originalDirId, out var originalDistances))
            {
                context.Distances[dirId] = Utils.ReverseDistanceList(
                    originalDistances,
                    originalDir.Attrs.Contains(Attr.Loop));
            }

            reversed = true;
        }

        /// <summary>处理 DISTANCE 子命令</summary>
        private static bool ParseDistance(Parser parser, Context context, string dirId)
        {
            if (parser.IsPeekId("AUTO"))
            {
                parser.ParseId();
                return true;  // 标记需要自动计算
            }

            var distances = parser.ParseNumberList();
            context.Distances[dirId] = distances;
            return false;
        }

        /// <summary>处理 LOOP 子命令</summary>
        private static void ParseLoop(Parser parser, DirectionInfo dirInfo)
        {
            dirInfo.Attrs.Add(Attr.Loop);
            if (parser.Peek().TokenType == TokenType.Id)
            {
                string subType = parser.Peek().Value.ToUpperInvariant();
                if (subType == Attr.Inner || subType == Attr.Outer)
                {
                    parser.ParseId();
                    dirInfo.DirectionType = subType.ToLowerInvariant();
                }
            }
        }

        /// <summary>处理 FUTURE 子命令</summary>
        private static void ParseFuture(Parser parser, Context context, DirectionInfo dirInfo, string lineId)
        {
            dirInfo.IsFuture = true;
            dirInfo.AliasColor = "#808080";
            if (string.IsNullOrEmpty(dirInfo.Alias) && context.Lines.TryGetValue(lineId, out var line))
                dirInfo.Alias = line.Name;
        }

        /// <summary>处理 SPEED_LIMIT 子命令</summary>
        private static void ParseSpeedLimit(Parser parser, Context context, string dirId)
        {
            string from = parser.ParseString();
            string to = parser.ParseString();
            int speed = parser.ParseInt();

            context.SpeedLimits.Add(new SpeedLimit
            {
                DirectionKey = Utils.ConvertToExternalDirKey(context, dirId),
                FromStation = from,
                ToStation = to,
                SpeedKmh = speed
            });
        }

        /// <summary>处理 HEADWAY 子命令</summary>
        private static void ParseHeadway(Parser parser, Context context, string lineId)
        {
            if (parser.IsPeekId("TIME_RANGE")) parser.ParseId();
            var (start, end) = parser.ParseTimeRange();
            if (parser.IsPeekId("VALUE")) parser.ParseId();
            int interval = (int)parser.ParseTimeInSeconds();

            List<DayOfWeek> days = ParseDays(parser);
            context.HeadwayRules.Add(new HeadwayRule
            {
                LineId = lineId,
                StartTime = start,
                EndTime = end,
                IntervalSeconds = interval,
                Days = days
            });
        }

        /// <summary>处理 CROWD 子命令</summary>
        private static void ParseCrowd(Parser parser, Context context, string dirId)
        {
            if (parser.IsPeekId("TIME_RANGE")) parser.ParseId();
            var (start, end) = parser.ParseTimeRange();
            if (parser.IsPeekId("VALUE")) parser.ParseId();
            double factor = parser.ParseDouble();

            List<DayOfWeek> days = ParseDays(parser);
            context.CrowdRules.Add(new CrowdRule
            {
                DirectionKey = Utils.ConvertToExternalDirKey(context, dirId),
                StartTime = start,
                EndTime = end,
                Factor = factor,
                Days = days
            });
        }

        /// <summary>处理 DWELL_TIME 子命令</summary>
        private static void ParseDwellTime(Parser parser, Context context, string lineId)
        {
            string station = parser.ParseString();
            double seconds = parser.ParseTimeInSeconds();
            context.DwellTimes[(station, lineId)] = seconds;
        }

        /// <summary>处理 OPEN_TIME 子命令</summary>
        private static void ParseOpenTime(Parser parser, Context context, string lineId)
        {
            var (start, end) = parser.ParseTimeRange();
            context.OpenTimes.Add(new OpenTimeRule
            {
                LineId = lineId,
                StartTime = start,
                EndTime = end
            });
        }

        /// <summary>处理 VIA 子命令</summary>
        private static void ParseVia(Parser parser, DirectionInfo dirInfo)
        {
            string viaDir = parser.ParseId();
            dirInfo.ViaDirections.Add(viaDir);
        }

        /// <summary>处理 DEPOT 子命令</summary>
        private static void ParseDepot(Parser parser, Context context, string lineId, string dirId)
        {
            string station = parser.ParseString();
            TimeSpan? departure = null;
            if (parser.Peek().TokenType == TokenType.Number)
                departure = parser.ParseClockTime();

            context.Depots.Add(new DepotInfo
            {
                LineId = lineId,
                Station = station,
                DirectionKey = Utils.ConvertToExternalDirKey(context, dirId),
                DepartureTime = departure
            });
        }

        // ====================================================================
        // 辅助方法
        // ====================================================================

        /// <summary>解析星期修饰符（WORKDAY / WEEKEND / 列表）</summary>
        private static List<DayOfWeek> ParseDays(Parser parser)
        {
            if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                return parser.ParseDayOfWeekList();

            if (parser.Peek().TokenType == TokenType.Id)
                return parser.ParseDayOfWeekModifier();

            return new List<DayOfWeek>();
        }

        /// <summary>创建 REV 反转方向</summary>
        private static DirectionInfo CreateReversedDirection(
            Context context,
            string newDirId,
            string originalDirId)
        {
            if (!context.Directions.TryGetValue(originalDirId, out var original))
                throw new SyntaxException($"原方向 '{originalDirId}' 不存在", new Token(TokenType.Id, originalDirId, 0));

            var reversedStations = new List<string>(original.Stations);
            reversedStations.Reverse();

            var newDir = new DirectionInfo
            {
                LineId = original.LineId,
                Stations = reversedStations,
                Attrs = new HashSet<string>(original.Attrs),
                Alias = original.Alias,
                AliasColor = original.AliasColor,
                DirectionType = original.Attrs.Contains(Attr.Loop)
                    ? original.DirectionType == "inner" ? "outer" : "inner"
                    : original.DirectionType,
                IsFuture = original.IsFuture,
                ConnectStation = original.ConnectStation,
                ViaDirections = new List<string>(original.ViaDirections)
            };

            // 反转距离
            if (context.Distances.TryGetValue(originalDirId, out var distances))
            {
                context.Distances[newDirId] = Utils.ReverseDistanceList(
                    distances,
                    original.Attrs.Contains(Attr.Loop));
            }

            return newDir;
        }
    }
}