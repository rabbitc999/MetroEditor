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
    ///  编译后处理流程
    /// </summary>
    internal class PostProcessor
    {
        Parser parser;
        Context context;

        public PostProcessor(Parser parser, Context context)
        {
            this.parser = parser;
            this.context = context;
        }

        public void DoProcess()
        {
            MarkFutureOnlyStations();
            ApplyPendingTransfers();
            RemoveDuplicateDirections();
            RemoveSingleStationDirections();
            RemoveUnusedStations();
            InferStationCities();
            FinalChecks();
            ExpandTransferTemplates();
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
        // 逻辑：按线路ID、车站序列和距离序列分组，保留优先级最高的方向
        // 优先级：非远期 > 无别名颜色 > 方向ID字典序较小
        // ====================================================================
        private void RemoveDuplicateDirections()
        {
            // 按 (线路ID, 车站序列, 距离序列) 分组，环线暂不处理
            var directionGroups = context.Directions
                .Where(keyValuePair => !keyValuePair.Value.Attrs.Contains("LOOP"))
                .GroupBy(keyValuePair => new
                {
                    keyValuePair.Value.LineId,
                    // 使用不可见分隔符连接车站名称，避免名称中包含分隔符导致的歧义
                    Stations = string.Join("\x1F", keyValuePair.Value.Stations),
                    Distances = context.Distances.TryGetValue(keyValuePair.Key, out var distanceList)
                        ? string.Join("\x1F", distanceList.Select(distance => distance.ToString()))
                        : ""
                })
                .Where(group => group.Count() > 1)
                .ToList();

            var directionsToRemove = new HashSet<string>();

            foreach (var group in directionGroups)
            {
                // 排序规则：非远期方向优先，无别名颜色优先，方向ID字典序较小优先
                var sortedDirections = group.OrderBy(keyValuePair =>
                {
                    var direction = keyValuePair.Value;
                    bool isFuture = direction.IsFuture;
                    bool hasNickColor = !string.IsNullOrEmpty(direction.AliasColor);
                    // 降序排列：false（非远期）排前面，false（无别名颜色）排前面
                    return (isFuture ? 1 : 0, hasNickColor ? 1 : 0);
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
        private void RemoveUnusedStations()
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
        }
    }
}
