using MetroEditor.MetroLang.JsonModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MetroEditor.MetroLang.Decompiler
{
    /// <summary>
    /// 将标准 JSON 路网数据反编译为可读性更强的 .metro 源文件。
    /// 核心职责：将结构化的 <see cref="JsonRoot"/> 对象序列化为领域特定语言（DSL）。
    /// </summary>
    public class MetroDecompiler
    {
        private readonly JsonRoot _root;
        private readonly List<string> _outputLines = new();

        // 格式化常量
        private const string Indent = "  ";
        private const int ItemsPerLine = 3;

        // 映射表：用于记录 JSON 中的外部方向 Key 与反编译后内部方向 ID 的对应关系
        // Key: "LineId|StartStation|EndStation"
        // Value: "LineId_Index" (如 "LINE1_0")
        private readonly Dictionary<string, string> _externalToInternalDirectionMap = new();

        /// <summary>
        /// 初始化反编译器实例。
        /// </summary>
        /// <param name="root">路网数据根对象。</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> 为 <c>null</c> 时抛出。</exception>
        public MetroDecompiler(JsonRoot root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>
        /// 执行反编译过程，生成 .metro 源文件内容。
        /// </summary>
        /// <returns>格式化后的 .metro 源文件字符串。</returns>
        public string Decompile()
        {
            _outputLines.Clear();
            WriteFileHeader();
            AddBlankLine();

            // REGION: 程序定义
            AddRegion("程序定义");
            GenerateProgramDefine();
            EndRegion();

            // REGION: 文字标签
            if (_root.Labels.Any())
            {
                AddRegion("文字标签");
                GenerateLabels();
                EndRegion();
            }

            // REGION: 车站列表
            AddRegion("车站列表");
            GenerateStationList();
            EndRegion();

            // REGION: 线路定义（核心部分）
            AddRegion("线路定义");
            GenerateLineDefinitions();
            EndRegion();

            // REGION: 平台坐标（线路级车站坐标）
            if (HasPlatformCoordData())
            {
                AddRegion("平台坐标");
                GeneratePlatformCoords();
                EndRegion();
            }

            // REGION: 运营参数与换乘
            AddRegion("运营参数与换乘");
            GenerateOperationAndTransfers();
            EndRegion();

            // REGION: 停站时间（条件生成）
            if (HasCustomDwellTimeData())
            {
                AddRegion("停站时间");
                GenerateDwellTimeDefine();
                EndRegion();
            }

            // REGION: 站点状态（条件生成）
            if (HasStationStatusData())
            {
                AddRegion("站点状态");
                GenerateStationStatuses();
                EndRegion();
            }

            // REGION: 票价规则
            if (_root.Price != null)
            {
                AddRegion("票价规则");
                GenerateFareRules();
                EndRegion();
            }

            // REGION: 发车间隔
            if (_root.HeadwayRules.Any())
            {
                AddRegion("发车间隔");
                GenerateHeadwayRules();
                EndRegion();
            }

            // REGION: 站点进出模式
            if (_root.StationModes.Any())
            {
                AddRegion("站点进出模式");
                GenerateStationModes();
                EndRegion();
            }

            // REGION: 拥挤度系数
            if (_root.CrowdRules.Any())
            {
                AddRegion("拥挤度系数");
                GenerateCrowdRules();
                EndRegion();
            }

            // REGION: 运营时间
            if (_root.OpenTimes.Any())
            {
                AddRegion("运营时间");
                GenerateOpenTimes();
                EndRegion();
            }

            // REGION: 车辆段
            if (_root.Depots.Any())
            {
                AddRegion("车辆段");
                GenerateDepots();
                EndRegion();
            }

            // REGION: 区间限速
            if (_root.SpeedLimits.Any())
            {
                AddRegion("区间限速");
                GenerateSpeedLimits();
                EndRegion();
            }

            // REGION: 虚拟换乘
            if (_root.VirtualTransfers.Any())
            {
                AddRegion("虚拟换乘");
                GenerateVirtualTransfers();
                EndRegion();
            }

            // REGION: 无障碍设施
            if (_root.Facilities?.Any() == true)
            {
                AddRegion("无障碍设施");
                GenerateFacilityDefine();
                EndRegion();
            }

            // REGION: 票价优惠策略
            if (_root.Promotions?.Any() == true)
            {
                AddRegion("票价优惠策略");
                GeneratePromotionDefine();
                EndRegion();
            }

            // REGION: 运营公司
            if (_root.OperatorCompanies?.Any() == true)
            {
                AddRegion("运营公司");
                GenerateOperatorCompanies();
                EndRegion();
            }

            WriteFileFooter();
            return string.Join("\n", _outputLines);
        }

        #region 基础辅助方法
        private void WriteLine(string text) => _outputLines.Add(text);
        private void AddBlankLine() => _outputLines.Add("");

        private void WriteFileHeader()
        {
            WriteLine("# ==========================================");
            WriteLine("# Metro 路网配置文件");
            WriteLine("# 由 MetroEditor 反编译器生成");
            WriteLine($"# 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteLine("# ==========================================");
        }

        private void WriteFileFooter()
        {
            AddBlankLine();
            WriteLine("# ==========================================");
            WriteLine("# 文件结束");
            WriteLine("# ==========================================");
        }

        private void AddRegion(string regionName) => WriteLine($"# REGION {regionName}");
        private void EndRegion() => WriteLine("# END_REGION");

        /// <summary>
        /// 检查是否存在自定义的停站时间配置。
        /// </summary>
        private bool HasCustomDwellTimeData()
        {
            return _root.Lines.Values.SelectMany(l => l.Direction)
                       .SelectMany(d => d.Station)
                       .Any(s => s.DwellTime.HasValue);
        }

        /// <summary>
        /// 检查是否存在自定义的站点状态配置。
        /// </summary>
        private bool HasStationStatusData()
        {
            return _root.Stations.Values
                       .Where(s => s.Lines != null)
                       .SelectMany(s => s.Lines)
                       .Any(sl => !string.IsNullOrEmpty(sl.Status));
        }

        /// <summary>
        /// 检查是否存在平台坐标数据。
        /// </summary>
        private bool HasPlatformCoordData()
        {
            return _root.Stations.Values
                .Where(s => s.Lines != null)
                .SelectMany(s => s.Lines)
                .Any(l => l.PlatformCoord != null);
        }
        #endregion

        #region 头部与元数据生成
        private void GenerateProgramDefine()
        {
            WriteLine("# 程序定义（主城市/版本/日期）");
            WriteLine("PROGRAM_DEFINE");

            WriteLine($"{Indent}KEY_CITY '{_root.KeyCity}'");

            // 收集除主城市外的其他城市
            var otherCities = _root.Lines.Values
                .Select(l => l.City)
                .Where(c => !string.IsNullOrEmpty(c) && c != _root.KeyCity)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            if (otherCities.Any())
            {
                var cityList = string.Join(", ", otherCities.Select(c => $"'{c}'"));
                WriteLine($"{Indent}OTHER_CITY [{cityList}]");
            }

            if (_root.LanguageOrder != null && _root.LanguageOrder.Any())
            {
                var langList = string.Join(", ", _root.LanguageOrder.Select(lang => $"'{lang}'"));
                WriteLine($"{Indent}LANGUAGE_ORDER [{langList}]");
            }

            WriteLine($"{Indent}VERSION '{_root.Version}'");
            WriteLine($"{Indent}UPDATE_DATE '{_root.UpdateDate}'");

            if (!string.IsNullOrEmpty(_root.Author))
                WriteLine($"{Indent}AUTHOR '{_root.Author}'");
            if (!string.IsNullOrEmpty(_root.CreateDate))
                WriteLine($"{Indent}CREATE_DATE '{_root.CreateDate}'");

            WriteLine("END_DEFINE");
            AddBlankLine();
        }

        private void GenerateLabels()
        {
            WriteLine("# 文字标签");
            WriteLine("LABEL_DEFINE");
            foreach (var label in _root.Labels)
            {
                // :R 格式说明符确保浮点数使用小数点而非逗号（国际化兼容）
                WriteLine($"{Indent}NODE '{label.Text}' COORD {label.Lon:R}, {label.Lat:R}");
            }
            WriteLine("END_DEFINE");
            AddBlankLine();
        }
        #endregion

        #region 车站与线路生成
        private void GenerateStationList()
        {
            WriteLine("# 车站列表");
            WriteLine("STATION_DEFINE");

            var sortedStationNames = _root.Stations.Keys.OrderBy(k => k).ToList();
            foreach (var stationName in sortedStationNames)
            {
                var station = _root.Stations[stationName];
                var lineBuilder = new StringBuilder();
                lineBuilder.Append($"{Indent}NODE '{stationName}' COORD {station.Lon:R}, {station.Lat:R}");

                if (!string.IsNullOrEmpty(station.City))
                    lineBuilder.Append($" CITY '{station.City}'");

                if (station.OtherNames != null && station.OtherNames.Any())
                {
                    if (station.OtherNames.Count == 1)
                        lineBuilder.Append($" OTHER_NAME '{station.OtherNames[0]}'");
                    else
                    {
                        var names = string.Join(", ", station.OtherNames.Select(n => $"'{n}'"));
                        lineBuilder.Append($" OTHER_NAME [{names}]");
                    }
                }

                WriteLine(lineBuilder.ToString());
            }
            WriteLine("END_DEFINE");
            AddBlankLine();
        }

        private void GenerateLineDefinitions()
        {
            WriteLine("# 线路定义");

            foreach (var lineEntry in _root.Lines)
            {
                string lineId = lineEntry.Key;
                JsonLine line = lineEntry.Value;

                AddRegion($"线路 {lineId}: {line.Name}");
                WriteLine($"LINE_DEFINE {lineId}");

                // --- 基础属性 ---
                WriteLine($"{Indent}NAME '{line.Name}'");
                WriteLine($"{Indent}COLOR '{line.Color}'");

                if (!string.IsNullOrEmpty(line.City) && line.City != _root.KeyCity)
                    WriteLine($"{Indent}CITY '{line.City}'");
                if (!string.IsNullOrEmpty(line.Formation))
                    WriteLine($"{Indent}FORMATION '{line.Formation}'");
                if (line.Maxspeed > 0)
                    WriteLine($"{Indent}MAXSPEED {line.Maxspeed:F2}");
                if (line.OpenYear > 0)
                    WriteLine($"{Indent}OPEN_YEAR {line.OpenYear}");
                if (line.PowerKw.HasValue)
                    WriteLine($"{Indent}POWER {line.PowerKw.Value:F2}");
                if (line.Brake.HasValue)
                    WriteLine($"{Indent}BRAKE {line.Brake.Value:F2}");
                if (line.Disabled == true)
                    WriteLine($"{Indent}DISABLE");
                if (!string.IsNullOrEmpty(line.Link))
                    WriteLine($"{Indent}LINK '{line.Link}'");
                if (line.IsSpecial)
                    WriteLine($"{Indent}SPECIAL");

                // --- 方向定义（核心逻辑）---
                GenerateDirectionsForLine(lineId, line);

                WriteLine("END_DEFINE");
                EndRegion();
                AddBlankLine();
            }
        }

        /// <summary>
        /// 处理单条线路下的所有方向定义，自动识别反向方向以使用 REV 简写。
        /// </summary>
        private void GenerateDirectionsForLine(string lineId, JsonLine line)
        {
            // 为每个方向分配一个内部索引 ID (e.g., LINE1_0, LINE1_1)
            var directionsWithIndex = line.Direction.Select((dir, idx) => (Direction: dir, Index: idx)).ToList();

            // 构建反向方向映射表
            var reversedDirectionMap = BuildReverseDirectionMap(lineId, directionsWithIndex);

            // 第一遍：输出所有非反向的完整方向定义
            foreach (var (direction, index) in directionsWithIndex)
            {
                string internalDirectionId = $"{lineId}_{index}";
                if (reversedDirectionMap.ContainsKey(internalDirectionId))
                    continue; // 如果是反向方向，跳过，稍后使用 REV 简写

                WriteLine($"{Indent}DIRECTION_DEFINE {internalDirectionId}");
                GenerateSingleDirectionContent(direction, direction.IsLoop);

                // 记录映射关系，供后续模块（如 SpeedLimit, CrowdRule）引用
                string externalKey = $"{lineId}|{direction.Start}|{direction.End}";
                if (!_externalToInternalDirectionMap.ContainsKey(externalKey))
                    _externalToInternalDirectionMap[externalKey] = internalDirectionId;
            }

            // 第二遍：输出所有 REV 简写定义
            foreach (var mapping in reversedDirectionMap)
            {
                string originalDirectionId = mapping.Value;
                string revDirectionId = mapping.Key;
                WriteLine($"{Indent}DIRECTION_DEFINE {revDirectionId} REV {originalDirectionId}");

                // 记录反向方向的映射关系
                var revDirection = directionsWithIndex.First(d => $"{lineId}_{d.Index}" == revDirectionId).Direction;
                string revExternalKey = $"{lineId}|{revDirection.Start}|{revDirection.End}";
                if (!_externalToInternalDirectionMap.ContainsKey(revExternalKey))
                    _externalToInternalDirectionMap[revExternalKey] = revDirectionId;
            }
        }

        /// <summary>
        /// 构建方向的反向映射表。
        /// 通过比较站点列表的顺序来判断两个方向是否互为反向。
        /// </summary>
        private Dictionary<string, string> BuildReverseDirectionMap(string lineId, List<(JsonDirection Direction, int Index)> directionsWithIndex)
        {
            var usedAsRev = new HashSet<string>();
            var pairMap = new Dictionary<string, string>(); // RevId -> OriginalId

            for (int i = 0; i < directionsWithIndex.Count; i++)
            {
                var (dir1, idx1) = directionsWithIndex[i];
                string id1 = $"{lineId}_{idx1}";
                if (usedAsRev.Contains(id1)) continue;

                for (int j = i + 1; j < directionsWithIndex.Count; j++)
                {
                    var (dir2, idx2) = directionsWithIndex[j];
                    string id2 = $"{lineId}_{idx2}";
                    if (usedAsRev.Contains(id2)) continue;

                    var stations1 = dir1.Station.Select(s => s.Name).ToList();
                    var stations2 = dir2.Station.Select(s => s.Name).ToList();

                    if (stations1.Count == stations2.Count && stations1.SequenceEqual(stations2.AsEnumerable().Reverse()))
                    {
                        // 按字典序决定谁是 Original，谁是 Rev，保证输出稳定
                        if (string.Compare(stations1.FirstOrDefault(), stations2.FirstOrDefault(), StringComparison.Ordinal) <= 0)
                        {
                            pairMap[id2] = id1; // id2 是 id1 的反向
                            usedAsRev.Add(id2);
                        }
                        else
                        {
                            pairMap[id1] = id2; // id1 是 id2 的反向
                            usedAsRev.Add(id1);
                        }
                        break;
                    }
                }
            }
            return pairMap;
        }

        /// <summary>
        /// 生成单个方向定义块的内部内容（不包含 DIRECTION_DEFINE 头尾）。
        /// </summary>
        private void GenerateSingleDirectionContent(JsonDirection direction, bool isLineLoop)
        {
            // Stations
            var stationNames = direction.Station.Select(s => $"'{s.Name}'").ToList();
            WriteLine($"{Indent}{Indent}STATIONS [{string.Join(", ", stationNames)}]");

            // Distances
            var distances = direction.Station.Select(s => s.Next.HasValue ? s.Next.Value.ToString("F6") : "NIL").ToList();
            if (!isLineLoop && distances.Any() && distances.Last() != "NIL")
                distances[distances.Count - 1] = "NIL"; // 非环线终点距离设为 NIL
            WriteLine($"{Indent}{Indent}DISTANCE [{string.Join(", ", distances)}]");

            if (direction.IsLoop)
            {
                var loopLine = "LOOP";
                if (!string.IsNullOrEmpty(direction.DirectionType))
                    loopLine += $" {direction.DirectionType.ToUpperInvariant()}";
                WriteLine($"{Indent}{Indent}{loopLine}");
            }

            if (direction.IsFast) WriteLine($"{Indent}{Indent}FAST");
            if (direction.IsFuture) WriteLine($"{Indent}{Indent}FUTURE");

            if (direction.ViaDirections?.Any() == true)
                WriteLine($"{Indent}{Indent}VIA {string.Join(" ", direction.ViaDirections)}");

            WriteLine($"{Indent}END_DEFINE");
        }

        /// <summary>
        /// 生成平台坐标命令（线路级车站坐标）。
        /// </summary>
        private void GeneratePlatformCoords()
        {
            WriteLine("# 线路级车站坐标（平台坐标）");
            foreach (var stationEntry in _root.Stations.OrderBy(kv => kv.Key))
            {
                string station = stationEntry.Key;
                var lines = stationEntry.Value.Lines;
                if (lines == null) continue;

                foreach (var line in lines.OrderBy(l => l.Id))
                {
                    if (line.PlatformCoord != null)
                    {
                        var coord = line.PlatformCoord;
                        WriteLine($"PLATFORM_COORD '{station}' {line.Id} COORD {coord.Lon:R}, {coord.Lat:R}");
                    }
                }
            }
            AddBlankLine();
        }
        #endregion

        #region 运营规则生成
        private void GenerateOperationAndTransfers()
        {
            var opParams = _root.OperationParams;
            if (opParams != null)
            {
                WriteLine("# 运营参数");
                WriteLine("OPERATION_DEFINE");
                WriteLine($"{Indent}TRANSFER_TIME {opParams.InnerTransferTime}");
                WriteLine($"{Indent}OUT_STATION_TRANSFER_TIME {opParams.OutStationTransferTime}");
                WriteLine($"{Indent}DWELL_TIME {opParams.DwellTime}");
                if (opParams.EntryTime > 0)
                    WriteLine($"{Indent}ENTRY_TIME {opParams.EntryTime}");
                WriteLine("END_DEFINE");
                AddBlankLine();
            }

            GenerateCustomTransfers();
        }

        private void GenerateCustomTransfers()
        {
            var customTransfers = new List<(string Station, string Line1, string Line2, string Status, double TimeSec)>();
            int defaultInnerTransfer = _root.OperationParams?.InnerTransferTime ?? 180;
            int defaultOutTransfer = _root.OperationParams?.OutStationTransferTime ?? 300;

            foreach (var stationEntry in _root.Stations)
            {
                string stationName = stationEntry.Key;
                var station = stationEntry.Value;
                if (station.Transfers == null) continue;

                foreach (var transferEntry in station.Transfers)
                {
                    string key = transferEntry.Key;
                    var parts = key.Split('|');
                    if (parts.Length != 2) continue;

                    string line1 = parts[0], line2 = parts[1];
                    string status = "operating";
                    double timeSec = 0;

                    ParseTransferValue(transferEntry.Value, out status, out timeSec, defaultInnerTransfer, defaultOutTransfer);

                    customTransfers.Add((stationName, line1, line2, status, timeSec));
                }
            }

            if (!customTransfers.Any()) return;

            WriteLine("# 自定义换乘");
            var distinctTransfers = customTransfers
                .DistinctBy(t => new { t.Station, L1 = Min(t.Line1, t.Line2), L2 = Max(t.Line1, t.Line2) })
                .OrderBy(t => t.Line1).ThenBy(t => t.Line2).ThenBy(t => t.Station);

            foreach (var (station, line1, line2, status, timeSec) in distinctTransfers)
            {
                WriteLine($"TRANSFER_DEFINE '{station}' {line1} {line2}");
                WriteLine($"{Indent}STATUS {status.ToUpperInvariant()}");
                WriteLine($"{Indent}TIME {(int)timeSec}");
                WriteLine("END_DEFINE");
            }
            AddBlankLine();
        }

        private void ParseTransferValue(object value, out string status, out double time, int defaultInner, int defaultOut)
        {
            status = "operating";
            time = defaultInner;

            switch (value)
            {
                case JsonElement jsonElement:
                    if (jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        if (jsonElement.TryGetProperty("status", out var statusProp))
                            status = statusProp.GetString() ?? "operating";
                        if (jsonElement.TryGetProperty("time", out var timeProp))
                            time = timeProp.GetDouble();
                        else
                            time = GetDefaultTimeForStatus(status, defaultInner, defaultOut);
                    }
                    break;
                case Dictionary<string, object> dict:
                    if (dict.TryGetValue("status", out var statusObj))
                        status = statusObj?.ToString() ?? "operating";
                    if (dict.TryGetValue("time", out var timeObj) && timeObj is double d)
                        time = d;
                    else
                        time = GetDefaultTimeForStatus(status, defaultInner, defaultOut);
                    break;
            }
        }

        private static string Min(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a : b;
        private static string Max(string a, string b) => string.CompareOrdinal(a, b) > 0 ? a : b;
        private int GetDefaultTimeForStatus(string status, int innerSec, int outerSec) =>
            status.Equals("out_station", StringComparison.OrdinalIgnoreCase) ? outerSec : innerSec;
        #endregion

        #region 其余模块生成（停站时间、状态、票价等）
        // 以下方法主要进行了变量重命名、注释补充和微小结构调整，
        // 逻辑与原代码保持一致。

        private void GenerateDwellTimeDefine()
        {
            var dwellList = new List<(string Station, string Line, double Seconds)>();
            foreach (var lineEntry in _root.Lines)
            {
                string lineId = lineEntry.Key;
                foreach (var direction in lineEntry.Value.Direction)
                {
                    foreach (var station in direction.Station)
                    {
                        if (station.DwellTime.HasValue)
                            dwellList.Add((station.Name, lineId, station.DwellTime.Value));
                    }
                }
            }

            if (!dwellList.Any()) return;

            var distinctDwells = dwellList
                .GroupBy(x => new { x.Station, x.Line })
                .Select(g => g.Last())
                .OrderBy(x => x.Line).ThenBy(x => x.Station);

            WriteLine("# 停站时间（Dwell Time）");
            WriteLine("DWELL_TIME_DEFINE");
            foreach (var item in distinctDwells)
                WriteLine($"{Indent}NODE '{item.Station}' {item.Line} {item.Seconds:F2}");
            WriteLine("END_DEFINE");
            AddBlankLine();
        }

        private void GenerateStationStatuses()
        {
            var statuses = new List<(string Station, string Line, string Status)>();
            foreach (var stationEntry in _root.Stations)
            {
                string stationName = stationEntry.Key;
                if (stationEntry.Value.Lines == null) continue;

                foreach (var stationLine in stationEntry.Value.Lines)
                {
                    if (!string.IsNullOrEmpty(stationLine.Status))
                        statuses.Add((stationName, stationLine.Id, stationLine.Status));
                }
            }

            if (!statuses.Any()) return;

            WriteLine("# 站点线路状态");
            var distinctStatuses = statuses
                .DistinctBy(t => new { t.Station, t.Line })
                .OrderBy(t => t.Line).ThenBy(t => t.Station);

            foreach (var (station, line, status) in distinctStatuses)
                WriteLine($"STATION_STATUS '{station}' {line} {status.ToUpperInvariant()}");

            AddBlankLine();
        }

        private void GenerateFareRules()
        {
            var price = _root.Price;
            if (price == null) return;

            WriteLine("# 票价规则");
            WriteLine("PRICE_DEFINE");

            if (price.DefaultFare > 0)
                WriteLine($"{Indent}DEFAULT_FARE {price.DefaultFare:F2}");

            if (price.Segments != null)
                foreach (var segment in price.Segments)
                    WriteLine($"{Indent}SEGMENT {segment.RangeStart:F2} {segment.RangeEnd:F2} {segment.Fare:F2}");

            if (price.LinearExtension?.RatePerKm > 0)
            {
                var ext = price.LinearExtension;
                WriteLine($"{Indent}LINEAR_EXTENSION THRESHOLD {ext.Threshold:F2} BASE_FARE {ext.BaseFare:F2} RATE_PER_KM {ext.RatePerKm:F6}");
            }

            if (price.SpecialLines != null)
            {
                foreach (var specialLine in price.SpecialLines)
                {
                    if (specialLine.All.HasValue)
                        WriteLine($"{Indent}SPECIAL_LINE {specialLine.LineId} ALL {specialLine.All.Value:F2}");

                    if (specialLine.Surcharge != null)
                        foreach (var surcharge in specialLine.Surcharge)
                            WriteLine($"{Indent}SPECIAL_LINE {specialLine.LineId} SEGMENT '{surcharge.NodeA}' '{surcharge.NodeB}' {surcharge.Price:F2}");
                }
            }

            if (price.TransferDiscount?.Enabled == true)
                WriteLine($"{Indent}TRANSFER_DISCOUNT ENABLED TRUE DISCOUNT_AMOUNT {price.TransferDiscount.DiscountAmount:F2} TIME_LIMIT {price.TransferDiscount.TimeLimitMinutes}");

            if (price.FareCap > 0)
                WriteLine($"{Indent}FARE_CAP {price.FareCap:F2}");

            WriteLine("END_DEFINE");
            AddBlankLine();
        }

        private void GenerateHeadwayRules()
        {
            if (!_root.HeadwayRules.Any()) return;

            var groupedRules = _root.HeadwayRules.GroupBy(r => r.LineId).OrderBy(g => g.Key);
            WriteLine("# 发车间隔");

            foreach (var group in groupedRules)
            {
                string lineId = group.Key;
                if (string.IsNullOrEmpty(lineId)) continue;

                WriteLine($"HEADWAY_PROFILE {lineId}");
                foreach (var rule in group)
                {
                    var lineBuilder = new StringBuilder();
                    lineBuilder.Append($"{Indent}PERIOD {rule.StartTime} {rule.EndTime} {rule.IntervalSeconds}");
                    AppendDaysCondition(lineBuilder, rule.Days);
                    WriteLine(lineBuilder.ToString());
                }
                WriteLine("END_DEFINE");
            }
            AddBlankLine();
        }

        private void GenerateStationModes()
        {
            if (!_root.StationModes.Any()) return;

            WriteLine("# 站点进出模式");
            foreach (var rule in _root.StationModes.OrderBy(r => r.Station))
            {
                var lineBuilder = new StringBuilder();
                lineBuilder.Append($"STATION_MODE '{rule.Station}' {rule.Mode}");

                if (!string.IsNullOrEmpty(rule.StartTime) && !string.IsNullOrEmpty(rule.EndTime))
                    lineBuilder.Append($" TIME_RANGE {rule.StartTime} {rule.EndTime}");

                AppendDaysCondition(lineBuilder, rule.Days);
                WriteLine(lineBuilder.ToString());
            }
            AddBlankLine();
        }

        private void GenerateCrowdRules()
        {
            if (!_root.CrowdRules.Any()) return;

            var groupedRules = _root.CrowdRules.GroupBy(r => r.DirectionKey).OrderBy(g => g.Key);
            WriteLine("# 拥挤度系数");

            foreach (var group in groupedRules)
            {
                string externalDirectionKey = group.Key;
                if (string.IsNullOrEmpty(externalDirectionKey)) continue;

                string internalDirectionId = _externalToInternalDirectionMap.TryGetValue(externalDirectionKey, out var id) ? id : externalDirectionKey;
                WriteLine($"CROWD_PROFILE {internalDirectionId}");

                foreach (var rule in group)
                {
                    var lineBuilder = new StringBuilder();
                    lineBuilder.Append($"{Indent}PERIOD {rule.StartTime} {rule.EndTime} {rule.Factor:F2}");
                    AppendDaysCondition(lineBuilder, rule.Days);
                    WriteLine(lineBuilder.ToString());
                }
                WriteLine("END_DEFINE");
            }
            AddBlankLine();
        }

        private void GenerateOpenTimes()
        {
            if (!_root.OpenTimes.Any()) return;

            WriteLine("# 运营时间");
            foreach (var rule in _root.OpenTimes.OrderBy(r => r.LineId))
            {
                string prefix = string.IsNullOrEmpty(rule.LineId) ? "GLOBAL" : rule.LineId;
                var lineBuilder = new StringBuilder();
                lineBuilder.Append($"OPEN_TIME {prefix} {rule.StartTime} {rule.EndTime}");
                AppendDaysCondition(lineBuilder, rule.Days);
                WriteLine(lineBuilder.ToString());
            }
            AddBlankLine();
        }

        private void GenerateDepots()
        {
            if (!_root.Depots.Any()) return;

            WriteLine("# 车辆段");
            foreach (var rule in _root.Depots.OrderBy(r => r.LineId))
            {
                var lineBuilder = new StringBuilder();
                lineBuilder.Append($"DEPOT {rule.LineId} '{rule.Station}'");

                if (!string.IsNullOrEmpty(rule.DirectionKey))
                {
                    string internalKey = _externalToInternalDirectionMap.TryGetValue(rule.DirectionKey, out var id) ? id : rule.DirectionKey;
                    lineBuilder.Append($" {internalKey}");
                }

                if (!string.IsNullOrEmpty(rule.DepartureTime))
                    lineBuilder.Append($" {rule.DepartureTime}");

                WriteLine(lineBuilder.ToString());
            }
            AddBlankLine();
        }

        private void GenerateSpeedLimits()
        {
            if (!_root.SpeedLimits.Any()) return;

            var groupedRules = _root.SpeedLimits.GroupBy(r => r.DirectionKey).OrderBy(g => g.Key);
            WriteLine("# 区间限速");

            foreach (var group in groupedRules)
            {
                string externalDirectionKey = group.Key;
                if (string.IsNullOrEmpty(externalDirectionKey)) continue;

                string internalDirectionId = _externalToInternalDirectionMap.TryGetValue(externalDirectionKey, out var id) ? id : externalDirectionKey;
                WriteLine($"SPEED_LIMIT_DEFINE {internalDirectionId}");

                foreach (var rule in group.OrderBy(r => r.FromStation))
                    WriteLine($"{Indent}NODE '{rule.FromStation}' '{rule.ToStation}' {rule.SpeedKmh}");

                WriteLine("END_DEFINE");
            }
            AddBlankLine();
        }

        private void GenerateVirtualTransfers()
        {
            if (!_root.VirtualTransfers.Any()) return;

            WriteLine("# 虚拟换乘");
            foreach (var rule in _root.VirtualTransfers.OrderBy(r => r.StationA))
            {
                WriteLine($"VIRTUAL_TRANSFER_DEFINE '{rule.StationA}' '{rule.StationB}'");
                WriteLine($"{Indent}TIME_MINUTES {rule.TimeMinutes:F2}");
                if (rule.SecurityCheck) WriteLine($"{Indent}SECURITY_CHECK");
                if (rule.ViaDirections?.Any() == true)
                    WriteLine($"{Indent}VIA_DIRECTIONS [{string.Join(", ", rule.ViaDirections.Select(d => $"'{d}'"))}]");
                WriteLine("END_DEFINE");
            }
            AddBlankLine();
        }

        private void GenerateFacilityDefine()
        {
            if (_root.Facilities == null || !_root.Facilities.Any()) return;

            WriteLine("# 无障碍设施与卫生间信息");
            WriteLine("FACILITY_DEFINE");

            foreach (var facilityEntry in _root.Facilities.OrderBy(x => x.Key))
            {
                string station = facilityEntry.Key;
                var facilities = facilityEntry.Value;

                if (facilities.Toilet != null)
                    WriteLine(BuildToiletLine(station, "TOILET", facilities.Toilet));

                if (facilities.AccessibleToilet != null)
                    WriteLine(BuildToiletLine(station, "ACCESSIBLE_TOILET", facilities.AccessibleToilet));

                if (facilities.Elevators != null)
                    foreach (var elevator in facilities.Elevators)
                        WriteLine($"{Indent}ELEVATOR '{station}' LOCATION '{elevator.Location}'{(elevator.Accessible ? " ACCESSIBLE" : "")}");

                if (facilities.WideGate != null)
                    WriteLine($"{Indent}WIDE_GATE '{station}' QUANTITY {facilities.WideGate.Quantity}");

                if (facilities.Escalators != null)
                    foreach (var escalator in facilities.Escalators)
                        WriteLine($"{Indent}ESCALATOR '{station}' QUANTITY {escalator.Quantity}{(escalator.Direction != "both" ? $" DIRECTION {escalator.Direction.ToUpperInvariant()}" : "")}");

                if (facilities.TactilePaving != null)
                    WriteLine($"{Indent}TACTILE_PAVING '{station}' STATUS {facilities.TactilePaving.Status.ToUpperInvariant()}");
            }

            WriteLine("END_DEFINE");
            AddBlankLine();
        }

        private string BuildToiletLine(string station, string command, JsonToiletInfo toilet)
        {
            var lineBuilder = new StringBuilder();
            lineBuilder.Append($"{Indent}{command} '{station}' POSITION {toilet.Position.ToUpperInvariant()}");
            if (toilet.Accessible) lineBuilder.Append(" ACCESSIBLE");
            if (toilet.Gender != "mixed") lineBuilder.Append($" GENDER {toilet.Gender.ToUpperInvariant()}");
            return lineBuilder.ToString();
        }

        private void GeneratePromotionDefine()
        {
            if (_root.Promotions == null || !_root.Promotions.Any()) return;

            WriteLine("# 票价优惠策略");
            WriteLine("PROMOTION_DEFINE");

            foreach (var policy in _root.Promotions.OrderBy(p => p.Id))
            {
                WriteLine($"{Indent}POLICY {policy.Id}");
                WriteLine($"{Indent}{Indent}NAME '{policy.Name}'");
                if (!string.IsNullOrEmpty(policy.Description))
                    WriteLine($"{Indent}{Indent}DESCRIPTION '{policy.Description}'");
                WriteLine($"{Indent}{Indent}TYPE {policy.Type.ToUpperInvariant()}");
                WriteLine($"{Indent}{Indent}VALUE {policy.Value:F2}");

                if (policy.ApplicableLines?.Any() == true)
                    WriteLine($"{Indent}{Indent}APPLICABLE_LINES [{string.Join(", ", policy.ApplicableLines)}]");

                if (!string.IsNullOrEmpty(policy.StartTime) && !string.IsNullOrEmpty(policy.EndTime))
                    WriteLine($"{Indent}{Indent}TIME_RANGE {policy.StartTime} {policy.EndTime}");

                AppendDaysCondition(new StringBuilder($"{Indent}{Indent}"), policy.Days, true);
                WriteLine($"{Indent}END_POLICY");
            }

            WriteLine("END_DEFINE");
            AddBlankLine();
        }

        /// <summary>
        /// 生成运营公司定义（OPERATOR_DEFINE 块）。
        /// </summary>
        private void GenerateOperatorCompanies()
        {
            if (_root.OperatorCompanies == null || !_root.OperatorCompanies.Any()) return;

            foreach (var company in _root.OperatorCompanies.OrderBy(c => c.Id))
            {
                WriteLine($"OPERATOR_DEFINE {company.Id}");
                WriteLine($"{Indent}NAME '{company.Name}'");
                if (!string.IsNullOrEmpty(company.ShortName))
                    WriteLine($"{Indent}SHORT_NAME '{company.ShortName}'");
                if (!string.IsNullOrEmpty(company.EnName))
                    WriteLine($"{Indent}EN_NAME '{company.EnName}'");
                if (!string.IsNullOrEmpty(company.Country))
                    WriteLine($"{Indent}COUNTRY '{company.Country}'");
                if (!string.IsNullOrEmpty(company.City))
                    WriteLine($"{Indent}CITY '{company.City}'");
                if (company.Founded.HasValue)
                    WriteLine($"{Indent}FOUNDED {company.Founded.Value}");
                if (!string.IsNullOrEmpty(company.Website))
                    WriteLine($"{Indent}WEBSITE '{company.Website}'");
                if (!string.IsNullOrEmpty(company.Hotline))
                    WriteLine($"{Indent}HOTLINE '{company.Hotline}'");
                if (!string.IsNullOrEmpty(company.Description))
                    WriteLine($"{Indent}DESCRIPTION '{company.Description}'");
                if (company.Lines?.Any() == true)
                    WriteLine($"{Indent}LINES [{string.Join(", ", company.Lines)}]");
                WriteLine("END_DEFINE");
                AddBlankLine();
            }
        }

        /// <summary>
        /// 辅助方法：将星期集合转换为 DSL 字符串表示。
        /// </summary>
        private void AppendDaysCondition(StringBuilder builder, List<string>? days, bool indentTwice = false)
        {
            if (days == null || !days.Any()) return;

            string indentation = indentTwice ? $"{Indent}{Indent}" : Indent;
            builder.Append(indentation);

            var weekdays = new[] { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY" };
            var upperDays = days.Select(d => d.ToUpperInvariant()).ToList();

            if (upperDays.Count == 5 && weekdays.All(wd => upperDays.Contains(wd)))
                builder.Append(" WORKDAY");
            else if (upperDays.Count == 2 && upperDays.Contains("SATURDAY") && upperDays.Contains("SUNDAY"))
                builder.Append(" WEEKEND");
            else if (upperDays.Count == 1)
                builder.Append($" {upperDays[0]}");
            else
                builder.Append($" [{string.Join(", ", upperDays)}]");
        }
        #endregion
    }
}