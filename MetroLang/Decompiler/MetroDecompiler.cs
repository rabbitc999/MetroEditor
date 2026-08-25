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
    /// </summary>
    /// <remarks>
    /// 本类只负责<b>数据组织与条件判断</b>，DSL 语法全部由 Templates 目录下的
    /// .template 模板文件声明（占位符 {{Key}}），可通过编辑模板自定义输出格式。
    /// </remarks>
    public class MetroDecompiler
    {
        private readonly JsonRoot _root;
        private readonly List<string> _outputLines = new();
        private readonly TemplateEngine _engine;

        // 映射表：JSON 外部方向 Key 与反编译后内部方向 ID 的对应关系
        // Key: "LineId|StartStation|EndStation"  Value: "LineId_Index"
        private readonly Dictionary<string, string> _externalToInternalDirectionMap = new();

        /// <summary>
        /// 初始化反编译器实例（使用默认模板目录）。
        /// </summary>
        public MetroDecompiler(JsonRoot root) : this(root, null) { }

        /// <summary>
        /// 初始化反编译器实例（可指定自定义模板目录）。
        /// </summary>
        /// <param name="root">路网数据根对象。</param>
        /// <param name="templateDirectory">模板目录；为 null 时使用默认目录。</param>
        public MetroDecompiler(JsonRoot root, string? templateDirectory)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _engine = new TemplateEngine(templateDirectory);
        }

        /// <summary>
        /// 执行反编译过程，生成 .metro 源文件内容。
        /// </summary>
        public string Decompile()
        {
            _outputLines.Clear();
            Append(_engine.Render("FileHeader", new Dictionary<string, string?>
            {
                ["GenerationTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }));
            AddBlankLine();

            AddRegion("程序定义");
            GenerateProgramDefine();
            EndRegion();

            if (_root.Labels.Any())
            {
                AddRegion("文字标签");
                GenerateLabels();
                EndRegion();
            }

            AddRegion("车站列表");
            GenerateStationList();
            EndRegion();

            AddRegion("线路定义");
            GenerateLineDefinitions();
            EndRegion();

            if (HasPlatformCoordData())
            {
                AddRegion("平台坐标");
                GeneratePlatformCoords();
                EndRegion();
            }

            AddRegion("运营参数与换乘");
            GenerateOperationAndTransfers();
            EndRegion();

            if (HasCustomDwellTimeData())
            {
                AddRegion("停站时间");
                GenerateDwellTimeDefine();
                EndRegion();
            }

            if (HasStationStatusData())
            {
                AddRegion("站点状态");
                GenerateStationStatuses();
                EndRegion();
            }

            if (_root.Price != null)
            {
                AddRegion("票价规则");
                GenerateFareRules();
                EndRegion();
            }

            if (_root.HeadwayRules.Any())
            {
                AddRegion("发车间隔");
                GenerateHeadwayRules();
                EndRegion();
            }

            if (_root.StationModes.Any())
            {
                AddRegion("站点进出模式");
                GenerateStationModes();
                EndRegion();
            }

            if (_root.CrowdRules.Any())
            {
                AddRegion("拥挤度系数");
                GenerateCrowdRules();
                EndRegion();
            }

            if (_root.OpenTimes.Any())
            {
                AddRegion("运营时间");
                GenerateOpenTimes();
                EndRegion();
            }

            if (_root.Depots.Any())
            {
                AddRegion("车辆段");
                GenerateDepots();
                EndRegion();
            }

            if (_root.SpeedLimits.Any())
            {
                AddRegion("区间限速");
                GenerateSpeedLimits();
                EndRegion();
            }

            if (_root.VirtualTransfers.Any())
            {
                AddRegion("虚拟换乘");
                GenerateVirtualTransfers();
                EndRegion();
            }

            if (_root.Facilities?.Any() == true)
            {
                AddRegion("无障碍设施");
                GenerateFacilityDefine();
                EndRegion();
            }

            if (_root.Promotions?.Any() == true)
            {
                AddRegion("票价优惠策略");
                GeneratePromotionDefine();
                EndRegion();
            }

            if (_root.OperatorCompanies?.Any() == true)
            {
                AddRegion("运营公司");
                GenerateOperatorCompanies();
                EndRegion();
            }

            if (_root.Backgrounds?.Any() == true)
            {
                AddRegion("背景图片");
                GenerateBackgrounds();
                EndRegion();
            }

            AddBlankLine();
            Append(_engine.Render("FileFooter", new Dictionary<string, string?>()));
            return string.Join("\n", _outputLines);
        }

        #region 基础辅助方法
        private void Append(string text)
        {
            foreach (var line in text.Split('\n'))
                _outputLines.Add(line.TrimEnd('\r'));
        }
        private void AddBlankLine() => _outputLines.Add("");

        private void AddRegion(string regionName) =>
            Append(_engine.Render("Region", new Dictionary<string, string?> { ["Name"] = regionName }));
        private void EndRegion() =>
            Append(_engine.Render("EndRegion", new Dictionary<string, string?>()));

        private bool HasCustomDwellTimeData() =>
            _root.Lines.Values.SelectMany(l => l.Direction)
                       .SelectMany(d => d.Station)
                       .Any(s => s.DwellTime.HasValue);

        private bool HasStationStatusData() =>
            _root.Stations.Values
                       .Where(s => s.Lines != null)
                       .SelectMany(s => s.Lines)
                       .Any(sl => !string.IsNullOrEmpty(sl.Status));

        private bool HasPlatformCoordData() =>
            _root.Stations.Values
                .Where(s => s.Lines != null)
                .SelectMany(s => s.Lines)
                .Any(l => l.PlatformCoord != null);

        /// <summary>转义字符串内容：\ → \\，' → \'（用于 .metro 字符串字面量）。</summary>
        private static string Esc(string? value) =>
            string.IsNullOrEmpty(value) ? "" : value.Replace("\\", "\\\\").Replace("'", "\\'");

        /// <summary>转义并用单引号包裹：返回 'escaped'。</summary>
        private static string Q(string? value) => $"'{Esc(value)}'";
        #endregion

        #region 头部与元数据生成
        private void GenerateProgramDefine()
        {
            var otherCities = _root.Lines.Values
                .Select(l => l.City)
                .Where(c => !string.IsNullOrEmpty(c) && c != _root.KeyCity)
                .Distinct()
                .OrderBy(c => c)
                .Select(Q)
                .ToList();

            var languages = _root.LanguageOrder?.Select(Q).ToList() ?? new List<string>();

            Append(_engine.Render("ProgramDefine", new Dictionary<string, string?>
            {
                ["KeyCity"] = Esc(_root.KeyCity),
                ["OtherCities"] = otherCities.Any() ? string.Join(", ", otherCities) : "",
                ["Languages"] = languages.Any() ? string.Join(", ", languages) : "",
                ["Version"] = Esc(_root.Version),
                ["UpdateDate"] = Esc(_root.UpdateDate),
                ["Author"] = Esc(_root.Author),
                ["CreateDate"] = Esc(_root.CreateDate)
            }));
            AddBlankLine();
        }

        private void GenerateLabels()
        {
            var items = _root.Labels.Select(label => new Dictionary<string, string?>
            {
                ["Text"] = Esc(label.Text),
                ["Lon"] = label.Lon.ToString("R"),
                ["Lat"] = label.Lat.ToString("R")
            }).ToList();

            Append(_engine.Render("LabelDefine", new Dictionary<string, string?>
            {
                ["Items"] = _engine.RenderMany("LabelNode", items)
            }));
            AddBlankLine();
        }
        #endregion

        #region 车站与线路生成
        private void GenerateStationList()
        {
            var items = new List<IReadOnlyDictionary<string, string?>>();

            foreach (var stationName in _root.Stations.Keys.OrderBy(k => k))
            {
                var station = _root.Stations[stationName];

                // 可选后缀：CITY / OTHER_NAME
                var suffix = new StringBuilder();
                if (!string.IsNullOrEmpty(station.City))
                    suffix.Append($" CITY {Q(station.City)}");
                if (station.OtherNames != null && station.OtherNames.Any())
                {
                    if (station.OtherNames.Count == 1)
                        suffix.Append($" OTHER_NAME {Q(station.OtherNames[0])}");
                    else
                        suffix.Append($" OTHER_NAME [{string.Join(", ", station.OtherNames.Select(Q))}]");
                }

                items.Add(new Dictionary<string, string?>
                {
                    ["Name"] = Esc(stationName),
                    ["Lon"] = station.Lon.ToString("R"),
                    ["Lat"] = station.Lat.ToString("R"),
                    ["Suffix"] = suffix.ToString()
                });
            }

            Append(_engine.Render("StationDefine", new Dictionary<string, string?>
            {
                ["Items"] = _engine.RenderMany("StationNode", items)
            }));
            AddBlankLine();
        }

        private void GenerateLineDefinitions()
        {
            Append("# 线路定义");
            foreach (var lineEntry in _root.Lines)
            {
                string lineId = lineEntry.Key;
                JsonLine line = lineEntry.Value;

                AddRegion($"线路 {lineId}: {line.Name}");
                Append(_engine.Render("LineDefine", new Dictionary<string, string?>
                {
                    ["LineId"] = lineId,
                    ["Name"] = Esc(line.Name),
                    ["Color"] = Esc(line.Color),
                    ["City"] = !string.IsNullOrEmpty(line.City) && line.City != _root.KeyCity ? Esc(line.City) : "",
                    ["Formation"] = Esc(line.Formation),
                    ["Maxspeed"] = line.Maxspeed > 0 ? line.Maxspeed.ToString("F2") : "",
                    ["OpenYear"] = line.OpenYear > 0 ? line.OpenYear.ToString() : "",
                    ["Power"] = line.PowerKw.HasValue ? line.PowerKw.Value.ToString("F2") : "",
                    ["Brake"] = line.Brake.HasValue ? line.Brake.Value.ToString("F2") : "",
                    ["Disable"] = line.Disabled == true ? "DISABLE" : "",
                    ["Link"] = Esc(line.Link),
                    ["Special"] = line.IsSpecial ? "SPECIAL" : "",
                    ["DirectionBlocks"] = GenerateDirectionsForLine(lineId, line)
                }));
                EndRegion();
                AddBlankLine();
            }
        }

        /// <summary>
        /// 处理单条线路下的所有方向定义，自动识别反向方向以使用 REV 简写。
        /// </summary>
        private string GenerateDirectionsForLine(string lineId, JsonLine line)
        {
            var blocks = new List<string>();
            var directionsWithIndex = line.Direction.Select((dir, idx) => (Direction: dir, Index: idx)).ToList();
            var reversedDirectionMap = BuildReverseDirectionMap(lineId, directionsWithIndex);

            // 第一遍：输出所有非反向的完整方向定义
            foreach (var (direction, index) in directionsWithIndex)
            {
                string internalDirectionId = $"{lineId}_{index}";
                if (reversedDirectionMap.ContainsKey(internalDirectionId))
                    continue;

                blocks.Add(_engine.Render("DirectionBody", new Dictionary<string, string?>
                {
                    ["DirectionId"] = internalDirectionId,
                    ["Stations"] = string.Join(", ", direction.Station.Select(s => Q(s.Name))),
                    ["Distances"] = BuildDistanceList(direction),
                    ["Loop"] = BuildLoopToken(direction),
                    ["Future"] = direction.IsFuture ? "FUTURE" : "",
                    ["ExpressStops"] = direction.ExpressStops?.Any() == true
                        ? string.Join(", ", direction.ExpressStops.Select(Q)) : "",
                    ["RoutingLines"] = direction.Routings?.Any() == true
                        ? _engine.RenderMany("Routing", direction.Routings.Select(r => new Dictionary<string, string?>
                        {
                            ["Name"] = Esc(r.Name),
                            ["Stations"] = string.Join(", ", r.Stations.Select(Q))
                        })) : "",
                    ["ViaLine"] = direction.ViaDirections?.Any() == true
                        ? _engine.Render("Via", new Dictionary<string, string?>
                        {
                            ["Directions"] = string.Join(" ", direction.ViaDirections)
                        }) : "",
                    ["SegmentTypeLines"] = direction.SegmentTypes?.Any() == true
                        ? _engine.RenderMany("SegmentType", direction.SegmentTypes.Select(seg => new Dictionary<string, string?>
                        {
                            ["From"] = Esc(seg.From),
                            ["To"] = Esc(seg.To),
                            ["Type"] = seg.Type.ToUpperInvariant()
                        })) : ""
                }));

                string externalKey = $"{lineId}|{direction.Start}|{direction.End}";
                if (!_externalToInternalDirectionMap.ContainsKey(externalKey))
                    _externalToInternalDirectionMap[externalKey] = internalDirectionId;
            }

            // 第二遍：输出所有 REV 简写定义
            foreach (var mapping in reversedDirectionMap)
            {
                string revDirectionId = mapping.Key;
                blocks.Add(_engine.Render("DirectionRev", new Dictionary<string, string?>
                {
                    ["RevId"] = revDirectionId,
                    ["OriginalId"] = mapping.Value
                }));

                var revDirection = directionsWithIndex.First(d => $"{lineId}_{d.Index}" == revDirectionId).Direction;
                string revExternalKey = $"{lineId}|{revDirection.Start}|{revDirection.End}";
                if (!_externalToInternalDirectionMap.ContainsKey(revExternalKey))
                    _externalToInternalDirectionMap[revExternalKey] = revDirectionId;
            }

            return string.Join("\n", blocks);
        }

        /// <summary>构建距离列表；非环线终点强制为 NIL。</summary>
        private static string BuildDistanceList(JsonDirection direction)
        {
            var distances = direction.Station
                .Select(s => s.Next.HasValue ? s.Next.Value.ToString("F6") : "NIL")
                .ToList();
            if (!direction.IsLoop && distances.Any() && distances.Last() != "NIL")
                distances[distances.Count - 1] = "NIL";
            return string.Join(", ", distances);
        }

        /// <summary>构建 LOOP 命令（含可选的 INNER/OUTER 类型）。</summary>
        private static string BuildLoopToken(JsonDirection direction)
        {
            if (!direction.IsLoop) return "";
            return string.IsNullOrEmpty(direction.DirectionType)
                ? "LOOP"
                : $"LOOP {direction.DirectionType.ToUpperInvariant()}";
        }

        /// <summary>
        /// 构建方向的反向映射表（通过站点序列逆序比较判断互为反向）。
        /// </summary>
        private static Dictionary<string, string> BuildReverseDirectionMap(string lineId, List<(JsonDirection Direction, int Index)> directionsWithIndex)
        {
            var usedAsRev = new HashSet<string>();
            var pairMap = new Dictionary<string, string>();

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
                        if (string.Compare(stations1.FirstOrDefault(), stations2.FirstOrDefault(), StringComparison.Ordinal) <= 0)
                        {
                            pairMap[id2] = id1;
                            usedAsRev.Add(id2);
                        }
                        else
                        {
                            pairMap[id1] = id2;
                            usedAsRev.Add(id1);
                        }
                        break;
                    }
                }
            }
            return pairMap;
        }

        private void GeneratePlatformCoords()
        {
            var items = new List<IReadOnlyDictionary<string, string?>>();
            foreach (var stationEntry in _root.Stations.OrderBy(kv => kv.Key))
            {
                string station = stationEntry.Key;
                foreach (var line in stationEntry.Value.Lines?.OrderBy(l => l.Id) ?? Enumerable.Empty<JsonStationLine>())
                {
                    if (line.PlatformCoord != null)
                    {
                        items.Add(new Dictionary<string, string?>
                        {
                            ["Station"] = Esc(station),
                            ["LineId"] = line.Id,
                            ["Lon"] = line.PlatformCoord.Lon.ToString("R"),
                            ["Lat"] = line.PlatformCoord.Lat.ToString("R")
                        });
                    }
                }
            }

            Append(_engine.Render("PlatformCoords", new Dictionary<string, string?>
            {
                ["Items"] = _engine.RenderMany("PlatformCoord", items)
            }));
            AddBlankLine();
        }
        #endregion

        #region 运营规则生成
        private void GenerateOperationAndTransfers()
        {
            var opParams = _root.OperationParams;
            if (opParams != null)
            {
                Append(_engine.Render("OperationDefine", new Dictionary<string, string?>
                {
                    ["InnerTransferTime"] = opParams.InnerTransferTime.ToString(),
                    ["OutStationTransferTime"] = opParams.OutStationTransferTime.ToString(),
                    ["DwellTime"] = opParams.DwellTime.ToString(),
                    ["EntryTime"] = opParams.EntryTime > 0 ? opParams.EntryTime.ToString() : ""
                }));
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
                if (stationEntry.Value.Transfers == null) continue;
                foreach (var transferEntry in stationEntry.Value.Transfers)
                {
                    var parts = transferEntry.Key.Split('|');
                    if (parts.Length != 2) continue;

                    ParseTransferValue(transferEntry.Value, out string status, out double timeSec,
                        defaultInnerTransfer, defaultOutTransfer);
                    customTransfers.Add((stationEntry.Key, parts[0], parts[1], status, timeSec));
                }
            }

            if (!customTransfers.Any()) return;

            var items = customTransfers
                .DistinctBy(t => new { t.Station, L1 = Min(t.Line1, t.Line2), L2 = Max(t.Line1, t.Line2) })
                .OrderBy(t => t.Line1).ThenBy(t => t.Line2).ThenBy(t => t.Station)
                .Select(t => new Dictionary<string, string?>
                {
                    ["Station"] = Esc(t.Station),
                    ["Line1"] = t.Line1,
                    ["Line2"] = t.Line2,
                    ["Status"] = t.Status.ToUpperInvariant(),
                    ["Time"] = ((int)t.TimeSec).ToString()
                })
                .ToList();

            Append(_engine.Render("CustomTransfers", new Dictionary<string, string?>
            {
                ["Items"] = _engine.RenderMany("TransferItem", items)
            }));
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
        private static int GetDefaultTimeForStatus(string status, int innerSec, int outerSec) =>
            status.Equals("out_station", StringComparison.OrdinalIgnoreCase) ? outerSec : innerSec;
        #endregion

        #region 其余模块生成
        private void GenerateDwellTimeDefine()
        {
            var dwellList = new List<(string Station, string Line, double Seconds)>();
            foreach (var lineEntry in _root.Lines)
            {
                foreach (var direction in lineEntry.Value.Direction)
                {
                    foreach (var station in direction.Station)
                    {
                        if (station.DwellTime.HasValue)
                            dwellList.Add((station.Name, lineEntry.Key, station.DwellTime.Value));
                    }
                }
            }

            if (!dwellList.Any()) return;

            var items = dwellList
                .GroupBy(x => new { x.Station, x.Line })
                .Select(g => g.Last())
                .OrderBy(x => x.Line).ThenBy(x => x.Station)
                .Select(x => new Dictionary<string, string?>
                {
                    ["Station"] = Esc(x.Station),
                    ["Line"] = x.Line,
                    ["Seconds"] = x.Seconds.ToString("F2")
                })
                .ToList();

            Append(_engine.Render("DwellTimeDefine", new Dictionary<string, string?>
            {
                ["Items"] = _engine.RenderMany("DwellTimeNode", items)
            }));
            AddBlankLine();
        }

        private void GenerateStationStatuses()
        {
            var statuses = new List<(string Station, string Line, string Status)>();
            foreach (var stationEntry in _root.Stations)
            {
                if (stationEntry.Value.Lines == null) continue;
                foreach (var stationLine in stationEntry.Value.Lines)
                {
                    if (!string.IsNullOrEmpty(stationLine.Status))
                        statuses.Add((stationEntry.Key, stationLine.Id, stationLine.Status));
                }
            }

            if (!statuses.Any()) return;

            var items = statuses
                .DistinctBy(t => new { t.Station, t.Line })
                .OrderBy(t => t.Line).ThenBy(t => t.Station)
                .Select(t => new Dictionary<string, string?>
                {
                    ["Station"] = Esc(t.Station),
                    ["Line"] = t.Line,
                    ["Status"] = t.Status.ToUpperInvariant()
                })
                .ToList();

            Append(_engine.Render("StationStatuses", new Dictionary<string, string?>
            {
                ["Items"] = _engine.RenderMany("StationStatus", items)
            }));
            AddBlankLine();
        }

        private void GenerateFareRules()
        {
            var price = _root.Price;
            if (price == null) return;

            var specialLines = new List<string>();
            if (price.SpecialLines != null)
            {
                foreach (var specialLine in price.SpecialLines)
                {
                    if (specialLine.All.HasValue)
                        specialLines.Add(_engine.Render("SpecialLineAll", new Dictionary<string, string?>
                        {
                            ["LineId"] = specialLine.LineId,
                            ["Fare"] = specialLine.All.Value.ToString("F2")
                        }));
                    if (specialLine.Surcharge != null)
                        foreach (var surcharge in specialLine.Surcharge)
                            specialLines.Add(_engine.Render("SpecialLineSegment", new Dictionary<string, string?>
                            {
                                ["LineId"] = specialLine.LineId,
                                ["NodeA"] = Esc(surcharge.NodeA),
                                ["NodeB"] = Esc(surcharge.NodeB),
                                ["Fare"] = surcharge.Price.ToString("F2")
                            }));
                }
            }

            Append(_engine.Render("PriceDefine", new Dictionary<string, string?>
            {
                ["DefaultFare"] = price.DefaultFare > 0 ? price.DefaultFare.ToString("F2") : "",
                ["SegmentLines"] = price.Segments != null
                    ? _engine.RenderMany("PriceSegment", price.Segments.Select(s => new Dictionary<string, string?>
                    {
                        ["Start"] = s.RangeStart.ToString("F2"),
                        ["End"] = s.RangeEnd.ToString("F2"),
                        ["Fare"] = s.Fare.ToString("F2")
                    })) : "",
                ["LinearExtensionLine"] = price.LinearExtension?.RatePerKm > 0
                    ? _engine.Render("LinearExtension", new Dictionary<string, string?>
                    {
                        ["Threshold"] = price.LinearExtension.Threshold.ToString("F2"),
                        ["BaseFare"] = price.LinearExtension.BaseFare.ToString("F2"),
                        ["RatePerKm"] = price.LinearExtension.RatePerKm.ToString("F6")
                    }) : "",
                ["SpecialLineLines"] = string.Join("\n", specialLines),
                ["TransferDiscountLine"] = price.TransferDiscount?.Enabled == true
                    ? _engine.Render("TransferDiscount", new Dictionary<string, string?>
                    {
                        ["Amount"] = price.TransferDiscount.DiscountAmount.ToString("F2"),
                        ["TimeLimit"] = price.TransferDiscount.TimeLimitMinutes.ToString()
                    }) : "",
                ["FareCap"] = price.FareCap > 0 ? price.FareCap.ToString("F2") : ""
            }));
            AddBlankLine();
        }

        private void GenerateHeadwayRules()
        {
            var profiles = _root.HeadwayRules
                .GroupBy(r => r.LineId)
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .OrderBy(g => g.Key)
                .Select(g => _engine.Render("HeadwayProfile", new Dictionary<string, string?>
                {
                    ["LineId"] = g.Key!,
                    ["Periods"] = _engine.RenderMany("HeadwayPeriod", g.Select(rule => new Dictionary<string, string?>
                    {
                        ["Start"] = rule.StartTime,
                        ["End"] = rule.EndTime,
                        ["Interval"] = rule.IntervalSeconds.ToString(),
                        ["Days"] = BuildDaysSuffix(rule.Days)
                    }))
                }))
                .ToList();

            Append(_engine.Render("HeadwayRules", new Dictionary<string, string?>
            {
                ["Profiles"] = string.Join("\n", profiles)
            }));
            AddBlankLine();
        }

        private void GenerateStationModes()
        {
            var items = _root.StationModes
                .OrderBy(r => r.Station)
                .Select(rule => new Dictionary<string, string?>
                {
                    ["Station"] = rule.Station,
                    ["Mode"] = rule.Mode,
                    ["TimeRange"] = !string.IsNullOrEmpty(rule.StartTime) && !string.IsNullOrEmpty(rule.EndTime)
                        ? $" TIME_RANGE {rule.StartTime} {rule.EndTime}" : "",
                    ["Days"] = BuildDaysSuffix(rule.Days)
                })
                .ToList();

            Append(_engine.Render("StationModes", new Dictionary<string, string?>
            {
                ["Items"] = _engine.RenderMany("StationMode", items)
            }));
            AddBlankLine();
        }

        private void GenerateCrowdRules()
        {
            var profiles = _root.CrowdRules
                .GroupBy(r => r.DirectionKey)
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .OrderBy(g => g.Key)
                .Select(g => _engine.Render("CrowdProfile", new Dictionary<string, string?>
                {
                    ["DirectionId"] = MapToInternalDirectionId(g.Key!),
                    ["Periods"] = _engine.RenderMany("CrowdPeriod", g.Select(rule => new Dictionary<string, string?>
                    {
                        ["Start"] = rule.StartTime,
                        ["End"] = rule.EndTime,
                        ["Factor"] = rule.Factor.ToString("F2"),
                        ["Days"] = BuildDaysSuffix(rule.Days)
                    }))
                }))
                .ToList();

            Append(_engine.Render("CrowdRules", new Dictionary<string, string?>
            {
                ["Profiles"] = string.Join("\n", profiles)
            }));
            AddBlankLine();
        }

        private void GenerateOpenTimes()
        {
            var items = _root.OpenTimes
                .OrderBy(r => r.LineId)
                .Select(rule => new Dictionary<string, string?>
                {
                    ["Prefix"] = string.IsNullOrEmpty(rule.LineId) ? "GLOBAL" : rule.LineId,
                    ["Start"] = rule.StartTime,
                    ["End"] = rule.EndTime,
                    ["Days"] = BuildDaysSuffix(rule.Days)
                })
                .ToList();

            Append(_engine.Render("OpenTimes", new Dictionary<string, string?>
            {
                ["Items"] = _engine.RenderMany("OpenTime", items)
            }));
            AddBlankLine();
        }

        private void GenerateDepots()
        {
            var items = _root.Depots
                .OrderBy(r => r.LineId)
                .Select(rule => new Dictionary<string, string?>
                {
                    ["LineId"] = rule.LineId,
                    ["Station"] = Esc(rule.Station),
                    ["Direction"] = !string.IsNullOrEmpty(rule.DirectionKey)
                        ? $" {MapToInternalDirectionId(rule.DirectionKey!)}" : "",
                    ["DepartureTime"] = !string.IsNullOrEmpty(rule.DepartureTime) ? $" {rule.DepartureTime}" : ""
                })
                .ToList();

            Append(_engine.Render("Depots", new Dictionary<string, string?>
            {
                ["Items"] = _engine.RenderMany("Depot", items)
            }));
            AddBlankLine();
        }

        private void GenerateSpeedLimits()
        {
            var profiles = _root.SpeedLimits
                .GroupBy(r => r.DirectionKey)
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .OrderBy(g => g.Key)
                .Select(g => _engine.Render("SpeedLimitProfile", new Dictionary<string, string?>
                {
                    ["DirectionId"] = MapToInternalDirectionId(g.Key!),
                    ["Nodes"] = _engine.RenderMany("SpeedLimitNode", g.OrderBy(r => r.FromStation).Select(rule =>
                        new Dictionary<string, string?>
                        {
                            ["From"] = Esc(rule.FromStation),
                            ["To"] = Esc(rule.ToStation),
                            ["Speed"] = rule.SpeedKmh.ToString()
                        }))
                }))
                .ToList();

            Append(_engine.Render("SpeedLimits", new Dictionary<string, string?>
            {
                ["Profiles"] = string.Join("\n", profiles)
            }));
            AddBlankLine();
        }

        private void GenerateVirtualTransfers()
        {
            var items = _root.VirtualTransfers
                .OrderBy(r => r.StationA)
                .Select(rule => new Dictionary<string, string?>
                {
                    ["StationA"] = Esc(rule.StationA),
                    ["StationB"] = Esc(rule.StationB),
                    ["Time"] = rule.TimeMinutes.ToString("F2"),
                    ["SecurityCheck"] = rule.SecurityCheck ? "SECURITY_CHECK" : "",
                    ["ViaDirections"] = rule.ViaDirections?.Any() == true
                        ? string.Join(", ", rule.ViaDirections.Select(Q)) : ""
                })
                .ToList();

            Append(_engine.Render("VirtualTransfers", new Dictionary<string, string?>
            {
                ["Items"] = _engine.RenderMany("VirtualTransfer", items)
            }));
            AddBlankLine();
        }

        private void GenerateFacilityDefine()
        {
            if (_root.Facilities == null || !_root.Facilities.Any()) return;

            var items = new List<IReadOnlyDictionary<string, string?>>();
            foreach (var facilityEntry in _root.Facilities.OrderBy(x => x.Key))
            {
                string station = facilityEntry.Key;
                var facilities = facilityEntry.Value;

                if (facilities.Toilet != null)
                    items.Add(BuildToiletVars(station, "TOILET", facilities.Toilet));
                if (facilities.AccessibleToilet != null)
                    items.Add(BuildToiletVars(station, "ACCESSIBLE_TOILET", facilities.AccessibleToilet));

                if (facilities.Elevators != null)
                    foreach (var elevator in facilities.Elevators)
                        items.Add(new Dictionary<string, string?>
                        {
                            ["Station"] = Esc(station),
                            ["Location"] = Esc(elevator.Location),
                            ["Accessible"] = elevator.Accessible ? " ACCESSIBLE" : ""
                        });

                if (facilities.WideGate != null)
                    items.Add(new Dictionary<string, string?>
                    {
                        ["Station"] = Esc(station),
                        ["Quantity"] = facilities.WideGate.Quantity.ToString()
                    });

                if (facilities.Escalators != null)
                    foreach (var escalator in facilities.Escalators)
                        items.Add(new Dictionary<string, string?>
                        {
                            ["Station"] = Esc(station),
                            ["Quantity"] = escalator.Quantity.ToString(),
                            ["Direction"] = escalator.Direction != "both"
                                ? $" DIRECTION {escalator.Direction.ToUpperInvariant()}" : ""
                        });

                if (facilities.TactilePaving != null)
                    items.Add(new Dictionary<string, string?>
                    {
                        ["Station"] = Esc(station),
                        ["Status"] = facilities.TactilePaving.Status.ToUpperInvariant()
                    });
            }

            Append(_engine.Render("FacilityDefine", new Dictionary<string, string?>
            {
                ["Items"] = RenderFacilityItems(items)
            }));
            AddBlankLine();
        }

        /// <summary>卫生间条目模板与电梯/扶梯等模板不同，此处按内容特征分派渲染。</summary>
        private string RenderFacilityItems(List<IReadOnlyDictionary<string, string?>> items)
        {
            var rendered = new List<string>();
            foreach (var item in items)
            {
                string template = item.ContainsKey("Command") ? "Toilet"
                    : item.ContainsKey("Location") ? "Elevator"
                    : item.ContainsKey("Status") ? "TactilePaving"
                    : item.ContainsKey("Direction") ? "Escalator"
                    : "WideGate";
                rendered.Add(_engine.Render(template, item));
            }
            return string.Join("\n", rendered);
        }

        private Dictionary<string, string?> BuildToiletVars(string station, string command, JsonToiletInfo toilet) => new()
        {
            ["Command"] = command,
            ["Station"] = Esc(station),
            ["Position"] = toilet.Position.ToUpperInvariant(),
            ["Accessible"] = toilet.Accessible ? " ACCESSIBLE" : "",
            ["Gender"] = toilet.Gender != "mixed" ? $" GENDER {toilet.Gender.ToUpperInvariant()}" : ""
        };

        private void GeneratePromotionDefine()
        {
            if (_root.Promotions == null || !_root.Promotions.Any()) return;

            var policies = _root.Promotions
                .OrderBy(p => p.Id)
                .Select(policy => new Dictionary<string, string?>
                {
                    ["Id"] = policy.Id,
                    ["Name"] = Esc(policy.Name),
                    ["Description"] = Esc(policy.Description),
                    ["Type"] = policy.Type.ToUpperInvariant(),
                    ["Value"] = policy.Value.ToString("F2"),
                    ["Lines"] = policy.ApplicableLines?.Any() == true
                        ? string.Join(", ", policy.ApplicableLines) : "",
                    ["StartTime"] = !string.IsNullOrEmpty(policy.StartTime) && !string.IsNullOrEmpty(policy.EndTime)
                        ? policy.StartTime : "",
                    ["EndTime"] = !string.IsNullOrEmpty(policy.StartTime) && !string.IsNullOrEmpty(policy.EndTime)
                        ? policy.EndTime : "",
                    ["DaysLine"] = BuildDaysToken(policy.Days) is { } token && token.Length > 0 ? $"    {token}" : ""
                })
                .ToList();

            Append(_engine.Render("PromotionDefine", new Dictionary<string, string?>
            {
                ["Policies"] = _engine.RenderMany("PromotionPolicy", policies)
            }));
            AddBlankLine();
        }

        private void GenerateOperatorCompanies()
        {
            if (_root.OperatorCompanies == null || !_root.OperatorCompanies.Any()) return;

            foreach (var company in _root.OperatorCompanies.OrderBy(c => c.Id))
            {
                Append(_engine.Render("OperatorDefine", new Dictionary<string, string?>
                {
                    ["Id"] = company.Id,
                    ["Name"] = Esc(company.Name),
                    ["ShortName"] = Esc(company.ShortName),
                    ["EnName"] = Esc(company.EnName),
                    ["Country"] = Esc(company.Country),
                    ["City"] = Esc(company.City),
                    ["Founded"] = company.Founded.HasValue ? company.Founded.Value.ToString() : "",
                    ["Website"] = Esc(company.Website),
                    ["Hotline"] = Esc(company.Hotline),
                    ["Description"] = Esc(company.Description),
                    // LINES 元素为文本（带引号），否则反编译结果无法再次编译
                    ["Lines"] = company.Lines?.Any() == true
                        ? string.Join(", ", company.Lines.Select(Q)) : ""
                }));
                AddBlankLine();
            }
        }

        private void GenerateBackgrounds()
        {
            if (_root.Backgrounds == null || !_root.Backgrounds.Any()) return;

            foreach (var bg in _root.Backgrounds.OrderBy(b => b.Id))
            {
                Append(_engine.Render("BackgroundDefine", new Dictionary<string, string?>
                {
                    ["Id"] = bg.Id,
                    ["Name"] = Esc(bg.Name),
                    ["Type"] = bg.Type,
                    ["SrcLength"] = (bg.Src?.Length ?? 0).ToString(),
                    ["Bounds"] = bg.Bounds != null && bg.Bounds.Length == 4
                        ? $"{bg.Bounds[0]}, {bg.Bounds[1]}, {bg.Bounds[2]}, {bg.Bounds[3]}" : "",
                    ["Opacity"] = bg.Opacity.HasValue ? bg.Opacity.Value.ToString() : ""
                }));
                AddBlankLine();
            }
        }

        /// <summary>将外部方向 Key 映射为反编译后的内部方向 ID。</summary>
        private string MapToInternalDirectionId(string externalKey) =>
            _externalToInternalDirectionMap.TryGetValue(externalKey, out var id) ? id : externalKey;

        #region 星期集合转换
        /// <summary>
        /// 将星期集合转换为行尾后缀（与原版保持一致的 3 空格前导：
        /// 原版 AppendDaysCondition 在行内场景会先追加 2 空格缩进再追加带前导空格的令牌）。
        /// </summary>
        private static string BuildDaysSuffix(List<string>? days)
        {
            string token = BuildDaysToken(days);
            return token.Length > 0 ? "   " + token : "";
        }

        /// <summary>将星期集合转换为 DSL 令牌（WORKDAY / WEEKEND / 单日 / 列表）。</summary>
        private static string BuildDaysToken(List<string>? days)
        {
            if (days == null || !days.Any()) return "";

            var weekdays = new[] { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY" };
            var upperDays = days.Select(d => d.ToUpperInvariant()).ToList();

            if (upperDays.Count == 5 && weekdays.All(wd => upperDays.Contains(wd)))
                return "WORKDAY";
            if (upperDays.Count == 2 && upperDays.Contains("SATURDAY") && upperDays.Contains("SUNDAY"))
                return "WEEKEND";
            if (upperDays.Count == 1)
                return upperDays[0];
            return $"[{string.Join(", ", upperDays)}]";
        }
        #endregion
        #endregion
    }
}
