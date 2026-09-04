using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.Simulator.Models;
using MetroEditor.Simulator.Utils;

namespace MetroEditor.Simulator.Data
{
    /// <summary>
    /// 地图数据解析器
    /// 将 JsonRoot 结构转换为 SimDataCache 内部数据。
    /// </summary>
    public static class SimDataParser
    {
        /// <summary>
        /// 将 <see cref="JsonRoot"/> 路网数据解析为仿真内部缓存 <see cref="SimDataCache"/>。
        /// <para>解析过程包含：</para>
        /// <list type="number">
        /// <item>提取基础信息和运营参数</item>
        /// <item>解析站点坐标、状态和换乘规则</item>
        /// <item>解析线路属性、方向、距离和速度限制</item>
        /// <item>识别线路分段运营标记</item>
        /// <item>解析票价规则</item>
        /// <item>转换发车间隔、拥挤度、运营时间等其他规则</item>
        /// </list>
        /// </summary>
        /// <param name="root">JSON 路网数据根对象</param>
        /// <returns>填充完成的数据缓存对象</returns>
        public static SimDataCache Parse(JsonRoot root)
        {
            SimDataCache cache = new SimDataCache();

            // ====== 1. 基础信息 ======
            // 先保存原始根对象，后续算法可按需读取未展开的 JSON 字段。
            cache.JsonData = root;
            cache.KeyCity = root.KeyCity ?? "未知";
            cache.Version = root.Version ?? "未知";
            cache.UpdateDate = root.UpdateDate ?? "未知";

            // ====== 2. 运营参数 ======
            var op = root.OperationParams;
            if (op != null)
            {
                cache.AvgTransferTime = op.InnerTransferTime;
                cache.AvgStopTime = op.DwellTime;
                cache.OutStationTransferTime = op.OutStationTransferTime;
                cache.AvgEntryTime = op.EntryTime;
            }

            // ====== 3. 解析站点 ======
            var stationCoords = new Dictionary<string, double[]>();
            var stationLineStatus = new Dictionary<string, Dictionary<string, string>>();
            var transferStatus = new Dictionary<string, Dictionary<string, string>>();
            var specialTransferTimeMap = new Dictionary<string, double>();

            if (root.Stations != null)
            {
                ParseStations(root.Stations, stationCoords, stationLineStatus,
                              transferStatus, specialTransferTimeMap);
            }

            // 将临时普通字典复制到并发缓存，便于仿真查询阶段并行读取。
            foreach (var kv in stationCoords) cache.StationCoords[kv.Key] = kv.Value;
            foreach (var kv in stationLineStatus) cache.StationLineStatusMap[kv.Key] = kv.Value;
            foreach (var kv in transferStatus) cache.TransferStatusMap[kv.Key] = kv.Value;
            foreach (var kv in specialTransferTimeMap) cache.SpecialTransferTimeMap[kv.Key] = kv.Value;

            // ====== 4. 解析线路 ======
            if (root.Lines != null)
            {
                ParseLines(root.Lines, cache.KeyCity, cache);
            }

            // ====== 5. 线路分段运营标记 ======
            foreach (var lineId in cache.LineNameMap.Keys)
            {
                // 分段判断依赖已解析的方向站点列表，因此必须在 ParseLines 之后执行。
                cache.LineSegmentedMap[lineId] = IsLineSegmented(cache, lineId, cache.DirStations);
            }

            // ====== 6. 票价解析 ======
            if (root.Price != null)
            {
                ParsePrice(root.Price, cache);
            }

            // ====== 7. 其他规则 ======
            if (root.HeadwayRules != null)
            {
                // 规则对象保持字符串时间格式，实际时间计算由 SimTimeCalculator 统一完成。
                cache.HeadwayRules = root.HeadwayRules.Select(hr => new SimHeadwayRule
                {
                    LineId = hr.LineId,
                    StartTime = hr.StartTime,
                    EndTime = hr.EndTime,
                    IntervalSeconds = hr.IntervalSeconds,
                    Days = hr.Days
                }).ToList();
            }

            if (root.CrowdRules != null)
            {
                cache.CrowdRules = root.CrowdRules.Select(cr => new SimCrowdRule
                {
                    DirectionKey = cr.DirectionKey,
                    StartTime = cr.StartTime,
                    EndTime = cr.EndTime,
                    Factor = cr.Factor,
                    Days = cr.Days
                }).ToList();
            }

            if (root.OpenTimes != null)
            {
                cache.OpenTimes = root.OpenTimes.Select(ot => new SimOpenTimeRule
                {
                    LineId = ot.LineId,
                    StartTime = ot.StartTime,
                    EndTime = ot.EndTime,
                    Days = ot.Days
                }).ToList();
            }

            if (root.VirtualTransfers != null)
            {
                cache.VirtualTransfers = root.VirtualTransfers.Select(vt => new SimVirtualTransfer
                {
                    StationA = vt.StationA,
                    StationB = vt.StationB,
                    TimeMinutes = vt.TimeMinutes,
                    SecurityCheck = vt.SecurityCheck,
                    ViaDirections = vt.ViaDirections
                }).ToList();
            }

            if (root.SpeedLimits != null)
            {
                cache.SpeedLimits = root.SpeedLimits.Select(sl => new SimSpeedLimit
                {
                    DirectionKey = sl.DirectionKey,
                    FromStation = sl.FromStation,
                    ToStation = sl.ToStation,
                    SpeedKmh = sl.SpeedKmh
                }).ToList();
            }

            return cache;
        }

        #region 私有辅助方法

        private static void ParseStations(
            Dictionary<string, JsonStation> stations,
            Dictionary<string, double[]> coords,
            Dictionary<string, Dictionary<string, string>> stationLineStatus,
            Dictionary<string, Dictionary<string, string>> transferStatus,
            Dictionary<string, double> specialTransferTime)
        {
            foreach (var kv in stations)
            {
                string name = kv.Key;
                JsonStation st = kv.Value;

                // 坐标使用 [经度, 纬度] 顺序，与 JsonModel 和地图绘制约定一致。
                coords[name] = new double[] { st.Lon, st.Lat };

                if (st.Lines != null)
                {
                    var statusMap = new Dictionary<string, string>();
                    foreach (var sl in st.Lines)
                    {
                        statusMap[sl.Id] = sl.Status ?? "operating";
                    }
                    stationLineStatus[name] = statusMap;
                }

                if (st.Transfers != null)
                {
                    var transferMap = new Dictionary<string, string>();
                    foreach (var tKv in st.Transfers)
                    {
                        // Transfers 兼容字符串状态和包含 status/time 的对象两种 JSON 形态。
                        string key = tKv.Key;
                        object? value = tKv.Value;
                        string status = "operating";

                        if (value is JsonElement elem)
                        {
                            if (elem.ValueKind == JsonValueKind.Object)
                            {
                                if (elem.TryGetProperty("status", out var statusProp))
                                    status = statusProp.GetString() ?? "operating";

                                if (elem.TryGetProperty("time", out var timeProp) &&
                                    timeProp.TryGetDouble(out double time) && time > 0)
                                {
                                    // 特殊换乘时间按正向和反向键各存一份，查询时无需再反转线路顺序。
                                    string cacheKey = name + "|" + key;
                                    specialTransferTime[cacheKey] = time;
                                    string[] parts = key.Split('|');
                                    if (parts.Length >= 2)
                                    {
                                        string revKey = parts[1] + "|" + parts[0];
                                        specialTransferTime[name + "|" + revKey] = time;
                                    }
                                }
                            }
                            else if (elem.ValueKind == JsonValueKind.String)
                            {
                                status = elem.GetString() ?? "operating";
                            }
                        }
                        transferMap[key] = status;
                    }
                    transferStatus[name] = transferMap;
                }
            }
        }

        private static void ParseLines(Dictionary<string, JsonLine> lines, string keyCity, SimDataCache cache)
        {
            foreach (var lineKv in lines)
            {
                string lineId = lineKv.Key;
                JsonLine line = lineKv.Value;
                bool allFuture = true;

                string city = string.IsNullOrEmpty(line.City) ? keyCity : line.City;
                string baseName = string.IsNullOrEmpty(line.Name) ? lineId : line.Name;
                string color = string.IsNullOrEmpty(line.Color) ? "#999999" : line.Color;
                if (!color.StartsWith("#")) color = "#" + color;

                string displayName = city.Equals(keyCity) ? baseName : city + baseName;

                cache.LineNameMap[lineId] = displayName;
                cache.LineColorMap[lineId] = color;
                cache.LineCityMap[lineId] = city;
                cache.LineSpecialMap[lineId] = line.IsSpecial;
                cache.LineMaxSpeedMap[lineId] = (int)line.Maxspeed;

                if (!string.IsNullOrEmpty(line.Formation))
                    cache.LineFormationMap[lineId] = line.Formation;
                if (line.PowerKw.HasValue)
                    cache.LinePowerKwMap[lineId] = line.PowerKw.Value;
                if (line.Brake.HasValue)
                    cache.LineBrakeMap[lineId] = line.Brake.Value;

                if (line.Direction != null && line.Direction.Any())
                {
                    bool hasLoop = false;
                    bool hasFuture = false;

                    var edgeSetAll = new HashSet<string>();
                    var edgeSetWithoutFuture = new HashSet<string>();
                    double totalLengthAll = 0.0;
                    double totalLengthWithoutFuture = 0.0;

                    foreach (var dir in line.Direction)
                    {
                        // 每个方向独立累积站点、环线信息和区间总长度。
                        string start = dir.Start;
                        string end = dir.End;
                        string dirKey = SimGraphKeyUtils.MakeDirKey(lineId, start, end);

                        if (dir.IsFuture) hasFuture = true;
                        else allFuture = false;
                        cache.DirFutureMap[dirKey] = dir.IsFuture;

                        if (dir.IsLoop)
                        {
                            hasLoop = true;
                            cache.DirLoopMap[dirKey] = true;
                            if (!string.IsNullOrEmpty(dir.DirectionType))
                                cache.DirDirectionTypeMap[dirKey] = dir.DirectionType;
                        }
                        else
                        {
                            cache.DirLoopMap[dirKey] = false;
                        }

                        var stationNames = new List<string>();
                        var stations = dir.Station;
                        if (stations != null && stations.Any())
                        {
                            int count = stations.Count;
                            for (int i = 0; i < count; i++)
                            {
                                var ds = stations[i];
                                string name = ds.Name;
                                stationNames.Add(name);

                                // 停站时间按“站点|方向键”索引，未提供或非正值时使用全局默认值。
                                if (ds.DwellTime.HasValue && ds.DwellTime.Value > 0)
                                {
                                    cache.StationStopTimeMap[name + "|" + dirKey] = ds.DwellTime.Value;
                                }

                                if (i < count - 1)
                                {
                                    // 使用排序后的站名组合键去重，避免往返方向重复累计同一物理区间。
                                    var nextDs = stations[i + 1];
                                    double dist = ds.Next ?? 0.0;
                                    string key = name.CompareTo(nextDs.Name) < 0
                                        ? name + "|" + nextDs.Name
                                        : nextDs.Name + "|" + name;

                                    if (edgeSetAll.Add(key))
                                        totalLengthAll += dist;
                                    if (!dir.IsFuture && edgeSetWithoutFuture.Add(key))
                                        totalLengthWithoutFuture += dist;
                                }
                                else if (dir.IsLoop)
                                {
                                    // 环线最后一站还要补上与首站之间的闭合边。
                                    var firstDs = stations[0];
                                    double dist = ds.Next ?? 0.0;
                                    string key = name.CompareTo(firstDs.Name) < 0
                                        ? name + "|" + firstDs.Name
                                        : firstDs.Name + "|" + name;
                                    if (edgeSetAll.Add(key))
                                        totalLengthAll += dist;
                                    if (!dir.IsFuture && edgeSetWithoutFuture.Add(key))
                                        totalLengthWithoutFuture += dist;
                                }
                            }
                        }
                        cache.DirStations[dirKey] = stationNames;
                    }

                    cache.HasLoopMap[lineId] = hasLoop;
                    cache.AllFutureMap[lineId] = allFuture;
                    cache.LineTotalLengthMap[lineId] = totalLengthAll;
                    cache.LineTotalLengthMapWithoutFuture[lineId] = totalLengthWithoutFuture;
                    cache.HasFutureMap[lineId] = hasFuture;
                }
            }
        }

        private static void ParsePrice(JsonPrice price, SimDataCache cache)
        {
            if (price == null) return;

            cache.PricingType = price.PricingType ?? "distance";
            cache.FareCap = price.FareCap;
            cache.DefaultFare = price.DefaultFare;

            if (price.Segments != null)
            {
                // 将 JSON 票价区间转换为仿真使用的轻量模型，避免算法层依赖序列化类型。
                cache.FareSegments = price.Segments.Select(seg => new SimFareSegment
                {
                    RangeStart = seg.RangeStart,
                    RangeEnd = seg.RangeEnd,
                    Fare = seg.Fare
                }).ToList();
            }

            if (price.LinearExtension != null)
            {
                cache.LinearExtension = new SimLinearExtension
                {
                    Threshold = price.LinearExtension.Threshold,
                    BaseFare = price.LinearExtension.BaseFare,
                    RatePerKm = price.LinearExtension.RatePerKm
                };
            }

            if (price.SpecialLines != null)
            {
                // 特殊线路同时支持线路组、统一票价、附加费和独立分段计价。
                cache.SpecialLineRules = price.SpecialLines.Select(sp => new SimSpecialLineRule
                {
                    LineId = sp.LineId,
                    UseLines = sp.UseLines,
                    All = sp.All,
                    DefaultFare = sp.DefaultFare,
                    Surcharge = sp.Surcharge?.Select(sur => new SimSurcharge
                    {
                        Node_a = sur.NodeA,
                        Node_b = sur.NodeB,
                        Price = sur.Price
                    }).ToList(),
                    Segments = sp.Segments?.Select(seg => new SimFareSegment
                    {
                        RangeStart = seg.RangeStart,
                        RangeEnd = seg.RangeEnd,
                        Fare = seg.Fare
                    }).ToList(),
                    LinearExtension = sp.LinearExtension != null ? new SimLinearExtension
                    {
                        Threshold = sp.LinearExtension.Threshold,
                        BaseFare = sp.LinearExtension.BaseFare,
                        RatePerKm = sp.LinearExtension.RatePerKm
                    } : null
                }).ToList();
            }
        }

        private static bool IsLineSegmented(SimDataCache cache, string lineId, ConcurrentDictionary<string, List<string>> dirStations)
        {
            var adj = new Dictionary<string, HashSet<string>>();

            foreach (var kv in dirStations)
            {
                string dirKey = kv.Key;
                var parts = SimGraphKeyUtils.ParseDirKey(dirKey);
                if (!SimStationUtils.IsFutureDirection(cache, dirKey)) continue;
                if (parts[0] != lineId) continue;

                var stations = kv.Value;
                // 仅把远期方向的连续站点连边，用于判断其是否由多个不连通区段组成。
                for (int i = 0; i < stations.Count - 1; i++)
                {
                    string a = stations[i];
                    string b = stations[i + 1];
                    if (!adj.ContainsKey(a)) adj[a] = new HashSet<string>();
                    if (!adj.ContainsKey(b)) adj[b] = new HashSet<string>();
                    adj[a].Add(b);
                    adj[b].Add(a);
                }

                string start = parts[1];
                string end = parts[2];
                if (start == end && stations.Count > 1)
                {
                    string first = stations[0];
                    string last = stations[stations.Count - 1];
                    if (!adj.ContainsKey(first)) adj[first] = new HashSet<string>();
                    if (!adj.ContainsKey(last)) adj[last] = new HashSet<string>();
                    adj[first].Add(last);
                    adj[last].Add(first);
                }
            }

            if (adj.Count == 0) return false;

            var visited = new HashSet<string>();
            int components = 0;
            // 对未来方向图做连通分量遍历；超过一个分量即视为分段运营。
            foreach (var station in adj.Keys)
            {
                if (!visited.Contains(station))
                {
                    components++;
                    if (components > 1) return true;
                    var stack = new Stack<string>();
                    stack.Push(station);
                    visited.Add(station);
                    while (stack.Count > 0)
                    {
                        string cur = stack.Pop();
                        foreach (string nb in adj[cur])
                        {
                            if (!visited.Contains(nb))
                            {
                                visited.Add(nb);
                                stack.Push(nb);
                            }
                        }
                    }
                }
            }
            return false;
        }

        #endregion
    }
}
