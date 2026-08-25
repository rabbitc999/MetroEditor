using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 编译后处理器，在语法解析完成后执行一系列语义优化和校验。
    /// <para>处理流程（按执行顺序）：</para>
    /// <list type="number">
    /// <item><see cref="MarkFutureOnlyStations"/>：标记仅出现在远期方向中的车站为待开通状态</item>
    /// <item><see cref="ApplyPendingTransfers"/>：为待开通车站自动生成换乘规则</item>
    /// <item><see cref="RemoveDuplicateDirections"/>：去重具有相同站点序列的冗余方向</item>
    /// <item><see cref="RemoveSingleStationDirections"/>：移除站点数不足2个的无效方向</item>
    /// <item><see cref="InferStationCities"/>：推断未显式定义城市的车站归属</item>
    /// <item><see cref="FinalChecks"/>：执行特殊线路票价、主城市等最终校验</item>
    /// <item><see cref="ExpandTransferTemplates"/>：展开换乘模板为具体换乘规则</item>
    /// <item><see cref="EvaluateAssertions"/>：执行编译时断言</item>
    /// <item><see cref="ProcessEchoMessages"/>：处理 ECHO 调试输出</item>
    /// <item><see cref="FormatStationKeys"/>：统一站点键格式（添加城市后缀）</item>
    /// </list>
    /// </summary>
    internal class PostProcessor
    {
        /// <summary>语法解析器实例</summary>
        Parser parser;
        /// <summary>编译数据上下文</summary>
        Context context;

        /// <summary>
        /// 创建后处理器实例。
        /// </summary>
        /// <param name="parser">语法解析器，用于获取/添加错误和警告</param>
        /// <param name="context">编译数据上下文，包含所有已解析的数据</param>
        public PostProcessor(Parser parser, Context context)
        {
            this.parser = parser;
            this.context = context;
        }

        /// <summary>
        /// 执行完整的后处理流程。
        /// </summary>
        public void DoProcess()
        {
            MarkFutureOnlyStations();
            ApplyPendingTransfers();
            RemoveDuplicateDirections();
            RemoveSingleStationDirections();
            InferStationCities();
            FinalChecks();
            ExpandTransferTemplates();
            EvaluateAssertions();
            ProcessEchoMessages();
            FormatStationKeys();
        }

        private void ProcessEchoMessages()
        {
            foreach (var raw in context.EchoMessages)
            {
                string resolved = ResolveVariables(raw);
                // 输出到控制台（或日志系统）
                Console.WriteLine($"[ECHO] {resolved}");
                // 同时存入警告列表，便于在 IDE 中查看
                parser.AddWarning($"[信息] {resolved}");
            }
        }

        private void FormatStationKeys()
        {
            // 1. 构建旧键 -> 新键的映射
            var keyMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var newStations = new Dictionary<string, (double Lon, double Lat)>();
            var newStationCity = new Dictionary<string, string>();
            var newOtherNames = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var kv in context.Stations)
            {
                string oldKey = kv.Key;
                var coord = kv.Value;
                string city = context.StationCity.TryGetValue(oldKey, out var c) ? c : context.MainCity;

                string newKey;
                if (oldKey.Contains("::"))
                {
                    string name = oldKey.Split(new[] { "::" }, StringSplitOptions.None)[1];
                    newKey = $"{name} ({city})";
                }
                else
                {
                    newKey = oldKey; // 纯键保持不变
                }

                keyMap[oldKey] = newKey;
                newStations[newKey] = coord;
                newStationCity[newKey] = city;

                if (context.StationOtherNames.TryGetValue(oldKey, out var aliases))
                    newOtherNames[newKey] = aliases.ToList();
            }

            // 2. 更新所有引用站点的数据结构
            // 2.1 方向中的站点列表
            foreach (var dir in context.Directions.Values)
            {
                for (int i = 0; i < dir.Stations.Count; i++)
                {
                    string old = dir.Stations[i];
                    if (keyMap.TryGetValue(old, out var newName))
                        dir.Stations[i] = newName;
                }
            }

            // 2.2 换乘规则
            foreach (var transfer in context.Transfers)
            {
                if (keyMap.TryGetValue(transfer.Station, out var newStation))
                    transfer.Station = newStation;
            }

            // 2.3 虚拟换乘
            foreach (var vt in context.VirtualTransfers)
            {
                if (keyMap.TryGetValue(vt.StationA, out var newA))
                    vt.StationA = newA;
                if (keyMap.TryGetValue(vt.StationB, out var newB))
                    vt.StationB = newB;
            }

            // 2.4 车辆段
            foreach (var depot in context.Depots)
            {
                if (keyMap.TryGetValue(depot.Station, out var newStation))
                    depot.Station = newStation;
            }

            // 2.5 停站时间（键是 (Station, LineId)）
            var dwellKeys = context.DwellTimes.Keys.ToList();
            foreach (var key in dwellKeys)
            {
                if (keyMap.TryGetValue(key.Station, out var newStation))
                {
                    var value = context.DwellTimes[key];
                    context.DwellTimes.Remove(key);
                    context.DwellTimes[(newStation, key.LineId)] = value;
                }
            }

            // 2.6 车站状态（键是 (Station, LineId?)）
            var statusKeys = context.StationStatus.Keys.ToList();
            foreach (var key in statusKeys)
            {
                if (keyMap.TryGetValue(key.Station, out var newStation))
                {
                    var value = context.StationStatus[key];
                    context.StationStatus.Remove(key);
                    context.StationStatus[(newStation, key.LineId)] = value;
                }
            }

            // 2.7 限速规则
            foreach (var limit in context.SpeedLimits)
            {
                if (keyMap.TryGetValue(limit.FromStation, out var newFrom))
                    limit.FromStation = newFrom;
                if (keyMap.TryGetValue(limit.ToStation, out var newTo))
                    limit.ToStation = newTo;
            }

            // 2.8 设施（Facilities 的键）
            var facilityKeys = context.Facilities.Keys.ToList();
            foreach (var oldKey in facilityKeys)
            {
                if (keyMap.TryGetValue(oldKey, out var newKey))
                {
                    var value = context.Facilities[oldKey];
                    context.Facilities.Remove(oldKey);
                    context.Facilities[newKey] = value;
                }
            }

            // 2.9 换乘模板中的 Station? （TransferTemplate.Station）
            foreach (var template in context.TransferTemplates)
            {
                if (keyMap.TryGetValue(template.Station, out var newStation))
                    template.Station = newStation;
            }

            // 2.10 车站模式（StationModeRule.Station）
            foreach (var mode in context.StationModes)
            {
                if (keyMap.TryGetValue(mode.Station, out var newStation))
                    mode.Station = newStation;
            }

            // 2.11 更新 PlatformCoords
            var newPlatformCoords = new Dictionary<(string Station, string LineId), (double Lon, double Lat)>();
            foreach (var kv in context.PlatformCoords)
            {
                var (station, lineId) = kv.Key;
                if (keyMap.TryGetValue(station, out var newStation))
                {
                    newPlatformCoords[(newStation, lineId)] = kv.Value;
                }
                else
                {
                    newPlatformCoords[(station, lineId)] = kv.Value;
                }
            }
            context.PlatformCoords.Clear();
            foreach (var kv in newPlatformCoords)
                context.PlatformCoords[kv.Key] = kv.Value;

            // 3. 替换字典
            context.Stations.Clear();
            foreach (var kv in newStations)
                context.Stations[kv.Key] = kv.Value;

            context.StationCity.Clear();
            foreach (var kv in newStationCity)
                context.StationCity[kv.Key] = kv.Value;

            context.StationOtherNames.Clear();
            foreach (var kv in newOtherNames)
                context.StationOtherNames[kv.Key] = kv.Value;
        }

        private string ResolveVariables(string raw)
        {
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["STATION_COUNT"] = context.Stations.Count.ToString(),
                ["LINE_COUNT"] = context.Lines.Count.ToString(),
                ["DIRECTION_COUNT"] = context.Directions.Count.ToString(),
                ["TRANSFER_COUNT"] = context.Transfers.Count.ToString(),
                ["CITY"] = context.MainCity,
                ["VERSION"] = context.Version
            };

            foreach (var kv in vars)
                raw = raw.Replace($"{{{kv.Key}}}", kv.Value);
            return raw;
        }

        private void EvaluateAssertions()
        {
            foreach (var assertion in context.Assertions)
            {
                bool result = Evaluate(assertion);
                if (!result)
                {
                    string msg = assertion.Message ?? $"断言失败: {FormatAssertion(assertion)}";
                    parser.ErrorList.Add(msg);
                }
            }
        }

        private string FormatAssertion(Assertion assertion)
        {
            // 简单格式化，用于默认错误消息
            return $"条件不满足 ({assertion.Predicate})";
        }

        private bool Evaluate(Assertion assertion)
        {
            if (assertion.Operator != null)
            {
                switch (assertion.Operator)
                {
                    case "NOT": return !Evaluate(assertion.Left);
                    case "AND": return Evaluate(assertion.Left) && Evaluate(assertion.Right);
                    case "OR": return Evaluate(assertion.Left) || Evaluate(assertion.Right);
                    default: throw new InvalidOperationException("未知操作符");
                }
            }

            // 计算谓词值（返回 double 或 bool）
            object value = ComputePredicate(assertion);
            if (assertion.Comparison == CompareOp.None)
            {
                // 期望布尔值
                if (value is bool b) return b;
                throw new InvalidOperationException("谓词返回非布尔值，但未指定比较操作");
            }
            else
            {
                // 比较操作
                if (value is double d)
                {
                    double compare = assertion.CompareValue.Value;
                    return assertion.Comparison switch
                    {
                        CompareOp.Equal => Math.Abs(d - compare) < 1e-9,
                        CompareOp.NotEqual => Math.Abs(d - compare) >= 1e-9,
                        CompareOp.LessThan => d < compare,
                        CompareOp.LessOrEqual => d <= compare,
                        CompareOp.GreaterThan => d > compare,
                        CompareOp.GreaterOrEqual => d >= compare,
                        _ => false,
                    };
                }
                throw new InvalidOperationException("只能对数值类型进行比较");
            }
        }

        private object ComputePredicate(Assertion assertion)
        {
            switch (assertion.Predicate)
            {
                case PredicateType.DirectionExists:
                    return context.Directions.ContainsKey((string)assertion.Arguments[0]);
                case PredicateType.LineExists:
                    return context.Lines.ContainsKey((string)assertion.Arguments[0]);
                case PredicateType.StationExists:
                    return context.Stations.ContainsKey((string)assertion.Arguments[0]);
                case PredicateType.StationCount:
                    string lineId = (string)assertion.Arguments[0];
                    if (!context.Lines.ContainsKey(lineId)) return 0.0; // 或抛出？
                    int count = context.Directions.Values.Where(d => d.LineId == lineId).SelectMany(d => d.Stations).Distinct().Count();
                    return (double)count;
                case PredicateType.TransferExists:
                    string station = (string)assertion.Arguments[0];
                    string l1 = (string)assertion.Arguments[1];
                    string l2 = (string)assertion.Arguments[2];
                    return context.Transfers.Any(t => t.Station == station && ((t.L1 == l1 && t.L2 == l2) || (t.L1 == l2 && t.L2 == l1)));
                case PredicateType.DistanceBetween:
                    string stationA = (string)assertion.Arguments[0];
                    string stationB = (string)assertion.Arguments[1];
                    if (!context.Stations.ContainsKey(stationA) || !context.Stations.ContainsKey(stationB))
                        return double.NaN;
                    var coordA = context.Stations[stationA];
                    var coordB = context.Stations[stationB];
                    double dist = Utils.Haversine(coordA.Lat, coordA.Lon, coordB.Lat, coordB.Lon) * 1000; // 转为米
                    return dist;
                case PredicateType.DirectionAttr:
                    string dirId = (string)assertion.Arguments[0];
                    string attr = (string)assertion.Arguments[1];
                    if (!context.Directions.TryGetValue(dirId, out var dir)) return false;
                    return dir.Attrs.Contains(attr.ToUpperInvariant());
                default:
                    throw new InvalidOperationException("未知谓词");
            }
        }

        // ====================================================================
        // 标记仅远期车站：识别仅在远期方向中出现的车站并标记为待开通状态
        // 逻辑：对比正常运营方向与远期方向的车站集合，找出差异并进行状态标记
        // ====================================================================
        private void MarkFutureOnlyStations()
        {
            // 收集每条线路在正常运营方向中出现的车站集合
            var normalStationsByLine = new Dictionary<string, HashSet<string>>();
            foreach (var direction in context.Directions.Values)
            {
                // 跳过远期方向和环线方向
                if (direction.IsFuture) continue;
                if (direction.Attrs.Contains("LOOP")) continue;

                // 初始化或获取线路对应的车站集合
                if (!normalStationsByLine.TryGetValue(direction.LineId, out var stationSet))
                {
                    stationSet = new HashSet<string>();
                    normalStationsByLine[direction.LineId] = stationSet;
                }

                // 将方向中的所有车站添加到集合中
                foreach (var station in direction.Stations)
                    stationSet.Add(station);
            }

            // 遍历所有远期方向，标记仅出现在远期方向中的车站
            foreach (var direction in context.Directions.Values)
            {
                if (!direction.IsFuture) continue;
                if (direction.Attrs.Contains("LOOP")) continue;

                var lineId = direction.LineId;
                // 获取该线路的正常运营车站集合，若不存在则为空集合
                var normalStationSet = normalStationsByLine.TryGetValue(lineId, out var normalSet)
                    ? normalSet
                    : new HashSet<string>();

                // 检查方向中的每个车站
                foreach (var station in direction.Stations)
                {
                    // 如果车站不在正常运营集合中，则标记为待开通状态
                    if (!normalStationSet.Contains(station))
                    {
                        var statusKey = (station, lineId);
                        if (!context.StationStatus.ContainsKey(statusKey))
                            context.StationStatus[statusKey] = "pending";
                    }
                }
            }
        }

        // ====================================================================
        // 应用待开通换乘规则：为待开通状态的车站自动生成换乘规则
        // 逻辑：当车站在某个线路上为待开通状态时，与其他线路的换乘自动设为待开通
        // ====================================================================
        private void ApplyPendingTransfers()
        {
            // 收集所有待开通状态的车站-线路组合
            var pendingStationLineSet = new HashSet<(string Station, string LineId)>();
            foreach (var statusEntry in context.StationStatus)
                if (statusEntry.Value == "pending")
                    pendingStationLineSet.Add((statusEntry.Key.Station, statusEntry.Key.LineId));

            // 如果没有待开通状态，直接返回
            if (pendingStationLineSet.Count == 0) return;

            // 构建车站-线路映射：记录每个车站上有哪些线路经过
            var stationLinesMapping = new Dictionary<string, HashSet<string>>();
            foreach (var direction in context.Directions.Values)
                foreach (var station in direction.Stations)
                {
                    if (!stationLinesMapping.TryGetValue(station, out var lineSet))
                    {
                        lineSet = new HashSet<string>();
                        stationLinesMapping[station] = lineSet;
                    }
                    lineSet.Add(direction.LineId);
                }

            // 为每个待开通的车站-线路组合生成换乘规则
            foreach (var (station, lineId) in pendingStationLineSet)
            {
                if (!stationLinesMapping.TryGetValue(station, out var linesAtStation)) continue;

                foreach (var otherLine in linesAtStation)
                {
                    // 跳过同一条线路
                    if (otherLine == lineId) continue;

                    // 检查是否已存在换乘规则
                    var existingTransfer = context.Transfers.FirstOrDefault(transfer =>
                        transfer.Station == station &&
                        (transfer.L1 == lineId && transfer.L2 == otherLine ||
                         transfer.L1 == otherLine && transfer.L2 == lineId));

                    if (existingTransfer != null)
                    {
                        // 更新现有规则的状态为待开通
                        existingTransfer.Status = "pending";
                    }
                    else
                    {
                        // 创建新的待开通换乘规则
                        context.Transfers.Add(new TransferRule
                        {
                            Station = station,
                            L1 = lineId,
                            L2 = otherLine,
                            Status = "pending",
                            Time = null
                        });
                    }
                }
            }
        }

        // ====================================================================
        // 移除重复方向：清理具有相同车站序列和距离序列的冗余方向定义
        // 逻辑：按线路ID、车站序列、距离序列、快车停靠、路段类型、经由方向分组，保留优先级最高的方向
        // 优先级：非远期 > 方向ID字典序较小
        // 注意：快车方向（EXPRESS_STOPS）与普通方向即使物理径路完全相同，也不视为重复，
        //       SegmentTypes / ViaDirections 同理，因此这些信息都参与分组键。
        // ====================================================================
        private void RemoveDuplicateDirections()
        {
            // 按 (线路ID, 车站序列, 距离序列, 快车停靠, 路段类型, 经由方向) 分组，环线暂不处理
            var directionGroups = context.Directions
                .Where(keyValuePair => !keyValuePair.Value.Attrs.Contains("LOOP"))
                .GroupBy(keyValuePair => new
                {
                    keyValuePair.Value.LineId,
                    // 使用不可见分隔符连接车站名称，避免名称中包含分隔符导致的歧义
                    Stations = string.Join("\x1F", keyValuePair.Value.Stations),
                    Distances = context.Distances.TryGetValue(keyValuePair.Key, out var distanceList)
                        ? string.Join("\x1F", distanceList.Select(distance => distance.ToString()))
                        : "",
                    ExpressStops = string.Join("\x1F", keyValuePair.Value.ExpressStops),
                    SegmentTypes = string.Join("\x1F",
                        keyValuePair.Value.SegmentTypes.Select(s => $"{s.FromStation}>{s.ToStation}:{s.Type}")),
                    ViaDirections = string.Join("\x1F", keyValuePair.Value.ViaDirections),
                    Routings = string.Join("\x1F",
                        keyValuePair.Value.Routings.Select(r => $"{r.Name}:{string.Join(",", r.Stations)}"))
                })
                .Where(group => group.Count() > 1)
                .ToList();

            var directionsToRemove = new HashSet<string>();

            foreach (var group in directionGroups)
            {
                // 排序规则：非远期方向优先，方向ID字典序较小优先
                var sortedDirections = group.OrderBy(keyValuePair =>
                {
                    var direction = keyValuePair.Value;
                    bool isFuture = direction.IsFuture;
                    // 降序排列：false（非远期）排前面
                    return isFuture;
                }).ThenBy(keyValuePair => keyValuePair.Key) // 优先级相同时按方向ID字典序
                .ToList();

                // 保留第一个（优先级最高），其余标记为待删除
                var keepDirectionId = sortedDirections.First().Key;
                foreach (var item in sortedDirections.Skip(1))
                    directionsToRemove.Add(item.Key);
            }

            // 如果没有需要删除的方向，直接返回
            if (directionsToRemove.Count == 0) return;

            // 从方向和距离字典中移除重复项
            foreach (var directionId in directionsToRemove)
            {
                context.Directions.Remove(directionId);
                context.Distances.Remove(directionId);
            }
        }

        /// <summary>
        /// 推断未显式定义城市的车站所属城市
        /// 在解析完成后调用，确保所有车站都有城市归属
        /// </summary>
        private void InferStationCities()
        {
            // 获取已显式定义城市的车站集合
            var explicitlyDefinedCities = new HashSet<string>(context.StationCity.Keys);

            foreach (var stationName in context.Stations.Keys)
            {
                // 跳过已显式定义城市的车站
                if (explicitlyDefinedCities.Contains(stationName))
                    continue;

                var candidateCities = new HashSet<string>();
                // 遍历所有方向，查找经过该站点的线路
                foreach (var direction in context.Directions.Values)
                {
                    if (direction.Stations.Contains(stationName) &&
                        context.Lines.TryGetValue(direction.LineId, out var lineInfo) &&
                        !string.IsNullOrEmpty(lineInfo.City))
                        candidateCities.Add(lineInfo.City);
                }

                // 根据候选城市数量决定最终城市
                if (candidateCities.Count == 1)
                {
                    // 所有线路属于同一城市
                    context.StationCity[stationName] = candidateCities.First();
                }
                else if (candidateCities.Count > 1)
                {
                    // 线路跨城市：使用主城市并发出警告
                    context.StationCity[stationName] = context.MainCity;
                    parser.AddWarning($"站点 '{stationName}' 途经线路城市不一致 ({string.Join(", ", candidateCities)})，将使用主城市 '{context.MainCity}'");
                }
                else
                {
                    // 无线路信息：回退到主城市
                    context.StationCity[stationName] = context.MainCity;
                }
            }
        }

        /// <summary>
        /// 展开换乘模板：将模板定义转换为具体的换乘规则
        /// 处理 TRANSFER 指令创建的模板，生成实际的换乘关系
        /// </summary>
        private void ExpandTransferTemplates()
        {
            foreach (var template in context.TransferTemplates)
            {
                // 获取站点所属的所有线路
                var linesAtStation = GetLinesForStation(template.Station);

                // 验证模板中的线路是否确实经过该站点
                if (!linesAtStation.Contains(template.LineId))
                {
                    parser.ErrorList.Add($"站点 '{template.Station}' 不包含线路 '{template.LineId}'，模板无效");

                    continue;
                }

                // 为站点上的其他每条线路创建换乘规则
                foreach (var otherLine in linesAtStation)
                {
                    if (otherLine == template.LineId) continue;
                    AddTransferFromTemplate(template.TypeId, template.Station, template.LineId, otherLine);
                }
            }
            // 清空模板列表，避免重复处理
            context.TransferTemplates.Clear();
        }

        /// <summary>
        /// 移除未被任何方向引用的孤立车站
        /// 清理解析过程中产生的无用数据，确保输出数据的整洁性
        /// </summary>
        public void RemoveUnusedStations()
        {
            // 收集所有被方向引用的车站
            var usedStations = new HashSet<string>();
            foreach (var direction in context.Directions.Values)
            {
                foreach (var station in direction.Stations)
                    usedStations.Add(station);
            }

            // 找出未使用的车站
            var unusedStations = context.Stations.Keys.Where(s => !usedStations.Contains(s)).ToList();
            if (unusedStations.Count == 0) return;

            // 从车站坐标字典中移除
            foreach (var station in unusedStations)
                context.Stations.Remove(station);

            // 清理相关数据结构中的引用
            var statusKeysToRemove = context.StationStatus.Keys.Where(k => unusedStations.Contains(k.Station)).ToList();
            foreach (var key in statusKeysToRemove)
                context.StationStatus.Remove(key);

            foreach (var station in unusedStations)
            {
                context.StationCity.Remove(station);
                context.Facilities.Remove(station);
                // 移除停站时间中涉及该站点的条目
                var dwellKeys = context.DwellTimes.Keys.Where(k => k.Station == station).ToList();
                foreach (var key in dwellKeys)
                    context.DwellTimes.Remove(key);
            }

            // 记录清理操作
            parser.AddWarning($"删除了 {unusedStations.Count} 个未被任何方向引用的站点: {string.Join(", ", unusedStations)}");
        }

        /// <summary>
        /// 根据换乘模板创建具体的换乘规则
        /// </summary>
        /// <param name="typeId">换乘类型ID</param>
        /// <param name="station">换乘站名称</param>
        /// <param name="line1">第一条线路ID</param>
        /// <param name="line2">第二条线路ID</param>
        private void AddTransferFromTemplate(string typeId, string station, string line1, string line2)
        {
            // 验证换乘类型是否存在
            if (!context.TransferTypes.TryGetValue(typeId, out var transferDefinition))
            {
                parser.ErrorList.Add($"换乘类型 '{typeId}' 未定义");

                return;
            }

            // 避免重复创建相同的换乘规则
            if (context.Transfers.Any(t => t.Station == station && t.L1 == line1 && t.L2 == line2))
                return;

            // 创建新的换乘规则
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

        /// <summary>
        /// 获取指定站点所属的所有线路ID集合
        /// </summary>
        /// <param name="station">站点名称</param>
        /// <returns>包含该站点的线路ID集合</returns>
        private HashSet<string> GetLinesForStation(string station)
        {
            var lineSet = new HashSet<string>();
            foreach (var direction in context.Directions.Values)
                if (direction.Stations.Contains(station))
                    lineSet.Add(direction.LineId);
            return lineSet;
        }

        // ====================================================================
        // 清理单站方向：移除站点数量少于2个的方向
        // 确保方向至少包含两个车站才能形成有效路径
        // ====================================================================
        private void RemoveSingleStationDirections()
        {
            var directionsToRemove = context.Directions
                .Where(kv => kv.Value.Stations.Count < 2)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var directionId in directionsToRemove)
            {
                context.Directions.Remove(directionId);
                context.Distances.Remove(directionId);
            }
        }

        // ====================================================================
        // 最终校验：在解析完成后执行全面的语义检查
        // 检查内容包括：特殊线路票价定义、主城市设置、车站状态有效性等
        // ====================================================================
        private void FinalChecks()
        {
            // 检查特殊线路是否定义了票价
            foreach (var lineId in context.SpecialLineIds)
            {
                bool hasFareDefinition = false;

                // 检查特殊票价配置中是否有匹配该线路的规则
                foreach (var specialLine in context.PriceConfig.SpecialLines)
                {
                    // 直接匹配线路ID
                    if (specialLine.LineId == lineId)
                    {
                        if (specialLine.All.HasValue || specialLine.Surcharge?.Any() == true ||
                            specialLine.DefaultFare.HasValue || specialLine.Segments?.Any() == true)
                        {
                            hasFareDefinition = true;
                            break;
                        }
                    }

                    // 匹配线路组（UseLines）
                    if (specialLine.UseLines != null && specialLine.UseLines.Contains(lineId))
                    {
                        if (specialLine.All.HasValue || specialLine.Surcharge?.Any() == true ||
                            specialLine.DefaultFare.HasValue || specialLine.Segments?.Any() == true)
                        {
                            hasFareDefinition = true;
                            break;
                        }
                    }
                }

                // 若未定义票价，记录错误
                if (!hasFareDefinition)
                {
                    parser.ErrorList.Add($"特殊线路 '{lineId}' 未定义票价（请使用 SPECIAL_MONEY、SPECIAL_DEFINE 或 PRICE_DEFINE 中的特殊票价）");
                }
            }

            // 检查主城市是否定义
            if (string.IsNullOrEmpty(context.MainCity))
            {
                parser.ErrorList.Add("未定义主城市 KEY_CITY");
            }

            // 检查车站状态的有效性
            foreach (var ((station, lineId), status) in context.StationStatus)
            {
                if (!new[] { "operating", "pending", "out_station" }.Contains(status))
                {
                    parser.ErrorList.Add($"站台 '{station}' 线路 '{lineId}' 的状态 '{status}' 无效");
                }
            }

            // 区域校验
            var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "COMMERCIAL", "RESIDENTIAL", "CBD", "GOVERNMENT", "TOURIST",
                "TRANSPORT_HUB", "EDUCATIONAL", "MEDICAL", "MIXED", "OTHER"
            };

            foreach (var kv in context.Zones)
            {
                var zoneId = kv.Key;
                var zone = kv.Value;

                // 必须名称和类型
                if (string.IsNullOrEmpty(zone.Name))
                    parser.ErrorList.Add($"区域 '{zoneId}' 缺少 NAME");

                if (string.IsNullOrEmpty(zone.Type) || !validTypes.Contains(zone.Type))
                    parser.ErrorList.Add($"区域 '{zoneId}' 的 TYPE 无效或缺失");

                // 城市存在性（若指定）
                if (!string.IsNullOrEmpty(zone.City) && !context.Cities.ContainsKey(zone.City))
                    parser.ErrorList.Add($"区域 '{zoneId}' 引用的城市 '{zone.City}' 未定义");

                // 检查定位方式：至少一个（CENTER+RADIUS 或 POLYGON）
                bool hasCenter = zone.Center != null && zone.Center.Length == 2;
                bool hasRadius = zone.Radius.HasValue;
                bool hasPolygon = zone.Polygon != null && zone.Polygon.Count > 0;
                if (!hasCenter && !hasPolygon)
                    context.Warnings.Add($"区域 '{zoneId}' 未定义 CENTER/RADIUS 或 POLYGON，可能无法在地图上定位");

                if (hasCenter != hasRadius) // 一个存在另一个缺失
                    context.Warnings.Add($"区域 '{zoneId}' 的 CENTER 和 RADIUS 应同时使用");

                // 检查关联车站是否存在
                foreach (var station in zone.Stations)
                {
                    if (!context.Stations.ContainsKey(station))
                        parser.ErrorList.Add($"区域 '{zoneId}' 引用了不存在的车站 '{station}'");
                }
            }

            // 运营公司校验
            foreach (var kv in context.OperatorCompanies)
            {
                var companyId = kv.Key;
                var company = kv.Value;

                // 必须名称
                if (string.IsNullOrEmpty(company.Name))
                    parser.ErrorList.Add($"运营公司 '{companyId}' 缺少 NAME");

                // 城市存在性（若指定）
                if (!string.IsNullOrEmpty(company.City) && !context.Cities.ContainsKey(company.City))
                    parser.ErrorList.Add($"运营公司 '{companyId}' 引用的城市 '{company.City}' 未定义");

                // 检查运营的线路是否存在
                foreach (var lineId in company.Lines)
                {
                    if (!context.Lines.ContainsKey(lineId))
                        parser.ErrorList.Add($"运营公司 '{companyId}' 引用了不存在的线路 '{lineId}'");
                }
            }

            // 背景图片校验
            foreach (var kv in context.Backgrounds)
            {
                var bgId = kv.Key;
                var bg = kv.Value;

                if (string.IsNullOrEmpty(bg.SrcBase64))
                    parser.ErrorList.Add($"背景 '{bgId}' 缺少 SRC（图片数据）");

                if (bg.Bounds == null || bg.Bounds.Length != 4)
                    parser.ErrorList.Add($"背景 '{bgId}' 的 BOUNDS 格式不正确，应为 [minLon, minLat, maxLon, maxLat]");
                else if (bg.Bounds[0] >= bg.Bounds[2] || bg.Bounds[1] >= bg.Bounds[3])
                    parser.ErrorList.Add($"背景 '{bgId}' 的 BOUNDS 范围无效：minLon 应小于 maxLon，minLat 应小于 maxLat");

                if (bg.Opacity.HasValue && (bg.Opacity.Value < 0 || bg.Opacity.Value > 1))
                    parser.ErrorList.Add($"背景 '{bgId}' 的 OPACITY 应在 0 到 1 之间");
            }

            // 路段类型校验
            var validSegmentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ELEVATED", "UNDERGROUND", "GROUND"
            };
            foreach (var dirKv in context.Directions)
            {
                var dirId = dirKv.Key;
                var dir = dirKv.Value;

                foreach (var seg in dir.SegmentTypes)
                {
                    if (!validSegmentTypes.Contains(seg.Type))
                        parser.ErrorList.Add($"方向 '{dirId}' 的 SEGMENT_TYPE 类型 '{seg.Type}' 无效，应为 ELEVATED、UNDERGROUND 或 GROUND");

                    if (!dir.Stations.Contains(seg.FromStation))
                        parser.ErrorList.Add($"方向 '{dirId}' 的 SEGMENT_TYPE 引用了不在方向中的车站 '{seg.FromStation}'");
                    if (!dir.Stations.Contains(seg.ToStation))
                        parser.ErrorList.Add($"方向 '{dirId}' 的 SEGMENT_TYPE 引用了不在方向中的车站 '{seg.ToStation}'");
                }

                // 快车停靠站校验：必须存在于方向站点序列中，且数量不少于2个
                if (dir.ExpressStops.Count > 0)
                {
                    if (dir.ExpressStops.Count < 2)
                        parser.ErrorList.Add($"方向 '{dirId}' 的 EXPRESS_STOPS 至少需要 2 个停靠站");
                    foreach (var stop in dir.ExpressStops)
                    {
                        if (!dir.Stations.Contains(stop))
                            parser.ErrorList.Add($"方向 '{dirId}' 的快车停靠站 '{stop}' 不在方向站点序列中");
                    }
                }

                // 交路校验：名称唯一、至少 1 个站点、站点必须属于方向站点序列
                var routingNames = new HashSet<string>();
                foreach (var routing in dir.Routings)
                {
                    if (string.IsNullOrWhiteSpace(routing.Name))
                        parser.ErrorList.Add($"方向 '{dirId}' 的交路名称不能为空");
                    else if (!routingNames.Add(routing.Name))
                        parser.ErrorList.Add($"方向 '{dirId}' 的交路名称 '{routing.Name}' 重复");

                    if (routing.Stations.Count == 0)
                        parser.ErrorList.Add($"方向 '{dirId}' 的交路 '{routing.Name}' 必须至少包含 1 个经过站点");
                    foreach (var stop in routing.Stations)
                    {
                        if (!dir.Stations.Contains(stop))
                            parser.ErrorList.Add($"方向 '{dirId}' 的交路 '{routing.Name}' 经过的站点 '{stop}' 不在方向站点序列中");
                    }
                }
            }
        }
    }
}
