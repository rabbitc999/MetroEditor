using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.Simulator.Data;
using MetroEditor.Simulator.Models;
using MetroEditor.Simulator.Utils;

namespace MetroEditor.Simulator.Algorithms
{
    /// <summary>
    /// 地铁线路图核心数据结构与路径规划算法。
    /// <para>该类负责将 <see cref="JsonRoot"/> 路网数据构建为图结构，并在此基础上执行路径搜索。
    /// 图的节点为「方向+车站」的组合，边分为运行边（同方向相邻站间）和换乘边（同站跨方向）。</para>
    /// <para>路径搜索采用 A* 算法，利用地理距离估算启发式加速搜索。支持多目标加权搜索
    /// （距离、换乘、时间、票价、站点数、拥挤度），以及随机路径和最长路径搜索。</para>
    /// <para>该类从 Android 版 MetroGraph 移植，核心逻辑保持一致，但将内部 Dijkstra 升级为 A*。</para>
    /// </summary>
    public class SimMetroGraph
    {
        // ==================== 图结构 ====================
        private readonly Dictionary<string, List<SimEdge>> _transferGraph = new();
        private readonly Dictionary<string, HashSet<string>> _stationLines = new();
        private readonly Dictionary<string, Dictionary<string, Dictionary<string, double>>> _lineAdjDist = new();

        // ==================== 辅助映射 ====================
        private readonly Dictionary<string, List<string>> _dirStations = new();
        private readonly Dictionary<string, string> _lineNameMap = new();
        private readonly Dictionary<string, string> _lineCityMap = new();
        private readonly Dictionary<string, bool> _hasLoopMap = new();
        private readonly Dictionary<string, string> _dirDisplayNameMap = new();
        private string _keyCity = string.Empty;

        private readonly SimDataCache _cache = new();

        public SimDataCache Cache { get { return _cache; } }

        public SimMetroGraph(SimDataCache simDataCache)
        {
            _cache = simDataCache;
            BuildGraphs();
        }

        /// <summary>
        /// 构建图结构：解析线路方向、站点信息和换乘关系。
        /// </summary>
        /// <returns>图中所有站点名称集合</returns>
        public HashSet<string> BuildGraphs()
        {
            JsonRoot metroData = _cache.JsonData;
            string keyCity = _cache.KeyCity;
            _keyCity = keyCity;

            if (metroData.Lines == null)
                throw new ArgumentException("数据中缺少 Lines 字段");

            ParseLines(metroData.Lines);
            if (metroData.Stations == null)
                throw new ArgumentException("数据中缺少 Stations 字段");
            ParseStations(metroData.Stations);

            BuildDirDisplayNames();
            return new HashSet<string>(_stationLines.Keys);
        }

        // ==================== 第一阶段：解析线路方向 ====================
        private void ParseLines(Dictionary<string, JsonLine> linesMap)
        {
            foreach (var kv in linesMap)
            {
                string lineId = kv.Key;
                var line = kv.Value;

                string lineName = string.IsNullOrEmpty(line.Name) ? lineId : line.Name;
                _lineNameMap[lineId] = lineName;

                string city = string.IsNullOrEmpty(line.City) ? "广州" : line.City;
                _lineCityMap[lineId] = city;

                var directions = line.Direction;
                if (directions == null || directions.Count == 0) continue;

                foreach (var dir in directions)
                {
                    if (dir.IsFuture) continue;

                    string start = dir.Start;
                    string end = dir.End;
                    bool isLoop = dir.IsLoop;
                    string dirKey = SimGraphKeyUtils.MakeDirKey(lineId, start, end);
                    _hasLoopMap[lineId] = isLoop;

                    var stationList = dir.Station;
                    if (stationList == null || stationList.Count == 0) continue;

                    var stationNames = new List<string>();
                    var adj = new Dictionary<string, Dictionary<string, double>>();
                    _lineAdjDist[dirKey] = adj;
                    _dirStations[dirKey] = stationNames;

                    for (int i = 0; i < stationList.Count; i++)
                    {
                        var ds = stationList[i];
                        string name = ds.Name;
                        if (string.IsNullOrEmpty(name)) continue;
                        stationNames.Add(name);

                        if (i < stationList.Count - 1)
                        {
                            string nxt = stationList[i + 1].Name;
                            if (ds.Next.HasValue)
                            {
                                double dist = ds.Next.Value;
                                if (!adj.ContainsKey(name)) adj[name] = new();
                                adj[name][nxt] = dist;
                                if (!adj.ContainsKey(nxt)) adj[nxt] = new();
                                adj[nxt][name] = dist;
                            }
                        }
                    }

                    if (isLoop && stationList.Count > 1)
                    {
                        string first = stationNames[0];
                        string last = stationNames[^1];
                        var lastDs = stationList[^1];
                        if (lastDs.Next.HasValue)
                        {
                            double dist = lastDs.Next.Value;
                            if (!adj.ContainsKey(last)) adj[last] = new();
                            adj[last][first] = dist;
                            if (!adj.ContainsKey(first)) adj[first] = new();
                            adj[first][last] = dist;
                        }
                    }

                    // 添加同线路边
                    for (int i = 0; i < stationNames.Count - 1; i++)
                    {
                        string cur = stationNames[i];
                        string nxt = stationNames[i + 1];
                        AddEdge(dirKey, cur, nxt);
                    }
                    if (isLoop && stationNames.Count > 1)
                    {
                        string first = stationNames[0];
                        string last = stationNames[^1];
                        AddEdge(dirKey, last, first);
                    }
                }
            }
        }

        // ==================== 第二阶段：解析站点，建立换乘边 ====================
        private void ParseStations(Dictionary<string, JsonStation> stationsMap)
        {
            foreach (var kv in stationsMap)
            {
                string stationName = kv.Key;
                var stInfo = kv.Value;

                var lines = stInfo.Lines;
                if (lines == null) continue;

                var dirKeys = new HashSet<string>();
                foreach (var lineEntry in lines)
                {
                    string id = lineEntry.Id;
                    string start = lineEntry.Start;
                    string end = lineEntry.End;
                    string status = SimStationUtils.GetStationLineStatus(_cache, stationName, id);
                    if (SimStationUtils.IsClosed(status)) continue;
                    dirKeys.Add(SimGraphKeyUtils.MakeDirKey(id, start, end));
                }

                var dirKeyList = dirKeys.ToList();
                for (int i = 0; i < dirKeyList.Count; i++)
                {
                    for (int j = i + 1; j < dirKeyList.Count; j++)
                    {
                        string dk1 = dirKeyList[i];
                        string dk2 = dirKeyList[j];
                        string transferStatus = SimStationUtils.GetTransferStatus(_cache, stationName, dk1, dk2);
                        if (SimStationUtils.IsClosed(transferStatus)) continue;
                        string n1 = SimGraphKeyUtils.MakeNode(dk1, stationName);
                        string n2 = SimGraphKeyUtils.MakeNode(dk2, stationName);
                        AddEdgeToGraph(n1, n2, 1);
                    }
                }

                foreach (string dk in dirKeys)
                {
                    if (!_stationLines.ContainsKey(stationName))
                        _stationLines[stationName] = new();
                    _stationLines[stationName].Add(dk);
                }
            }
        }

        // ==================== 后处理：方向显示名称 ====================
        private void BuildDirDisplayNames()
        {
            foreach (string dirKey in _dirStations.Keys)
            {
                string lineId = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey)!;
                string city = _lineCityMap[lineId];
                string lineName = _lineNameMap[lineId];
                _dirDisplayNameMap[dirKey] = BuildDisplayName(city, lineName);
            }
        }

        private string BuildDisplayName(string city, string lineName)
        {
            return city == _keyCity ? lineName : city + lineName;
        }

        // ==================== 图边添加辅助 ====================
        private void AddEdge(string dirKey, string from, string to)
        {
            string n1 = SimGraphKeyUtils.MakeNode(dirKey, from);
            string n2 = SimGraphKeyUtils.MakeNode(dirKey, to);
            AddEdgeToGraph(n1, n2, 0);

            if (!_stationLines.ContainsKey(from)) _stationLines[from] = new();
            _stationLines[from].Add(dirKey);
            if (!_stationLines.ContainsKey(to)) _stationLines[to] = new();
            _stationLines[to].Add(dirKey);
        }

        private void AddEdgeToGraph(string n1, string n2, int weight)
        {
            if (!_transferGraph.ContainsKey(n1)) _transferGraph[n1] = new();
            _transferGraph[n1].Add(new SimEdge(n2, weight));
            if (!_transferGraph.ContainsKey(n2)) _transferGraph[n2] = new();
            _transferGraph[n2].Add(new SimEdge(n1, weight));
        }

        /// <summary>
        /// 路径搜索公共入口。并行执行三路搜索后择优返回：
        /// <list type="number">
        /// <item>用户权重 + 排除特殊线路</item>
        /// <item>用户权重 + 包含特殊线路</item>
        /// <item>最短距离权重 + 排除特殊线路（作为基准）</item>
        /// </list>
        /// 结果经过前缀去重和票价计算，选择综合代价最小的路径。
        /// </summary>
        /// <param name="start">起点站名</param>
        /// <param name="end">终点站名</param>
        /// <param name="weight">搜索权重配置</param>
        /// <param name="travelTimeMillis">出行时间戳（毫秒），用于计算拥挤度和发车间隔</param>
        /// <returns>路径搜索结果，包含换乘次数、总代价、路径节点列表和票价</returns>
        public SimPathResult FindTransferPath(string start, string end, SimSearchWeight weight, long travelTimeMillis)
        {
            var shortestWeight = SimSearchWeight.GetShortestWeight();

            // 并行执行三个任务
            var task1 = Task.Run(() => FindTransferPathInternal(start, end, weight, true, travelTimeMillis));
            var task2 = Task.Run(() => FindTransferPathInternal(start, end, weight, false, travelTimeMillis));
            var task3 = Task.Run(() => FindTransferPathInternal(start, end, shortestWeight, true, travelTimeMillis));

            Task.WhenAll(task1, task2, task3).Wait();

            var result1 = task1.Result;
            var result2 = task2.Result;
            var shortest = task3.Result;

            var unique1 = SimPrefixFilter.FilterUniquePrefixBlocksWithCount(result1.Path);
            var unique2 = SimPrefixFilter.FilterUniquePrefixBlocksWithCount(result2.Path);
            var unique3 = SimPrefixFilter.FilterUniquePrefixBlocksWithCount(shortest.Path);

            result1.Path = unique1.FilteredList;
            result2.Path = unique2.FilteredList;
            shortest.Path = unique3.FilteredList;

            result1.Transfers -= unique1.RemovedCount;
            result2.Transfers -= unique2.RemovedCount;
            shortest.Transfers -= unique3.RemovedCount;

            var fare1 = SimFareCalculator.CalculateFareWithoutSpecial(_cache, shortest.Path, this);
            var fare2 = SimFareCalculator.CalculateFareWithSpecial(_cache, result2.Path, this);

            double cost1 = fare1.Total * weight.MoneyWeight + result1.Cost;
            double cost2 = fare2.Total * weight.MoneyWeight + result2.Cost;

            result1.TotalMoney = fare1.Total;
            result1.SpecialMoney = fare1.Special;
            result1.Cost = cost1;

            result2.TotalMoney = fare2.Total;
            result2.SpecialMoney = fare2.Special;
            result2.Cost = cost2;

            if (result1.Transfers == -1) return result2;
            return cost1 < cost2 ? result1 : result2;
        }

        /// <summary>
        /// 路径搜索公共入口（使用当前时间作为出行时间戳）。
        /// </summary>
        /// <inheritdoc cref="FindTransferPath(string, string, SimSearchWeight, long)"/>
        public SimPathResult FindTransferPath(string start, string end, SimSearchWeight weight)
        {
            return FindTransferPath(start, end, weight, new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds());
        }

        // ==================== 内部 A* 路径搜索 ====================
        private SimPathResult FindTransferPathInternal(string start, string end, SimSearchWeight weight,
                                                       bool excludeSpecial, long travelTimeMillis)
        {
            if (start == end)
            {
                if (_stationLines.TryGetValue(start, out var dirs) && dirs.Count > 0)
                {
                    var filtered = excludeSpecial ? SimStationUtils.FilterOutSpecialDirs(_cache, dirs) : dirs;
                    if (filtered.Count > 0)
                    {
                        string firstDir = filtered.First();
                        return new SimPathResult(0, 0, new List<string> { SimGraphKeyUtils.MakeNode(firstDir, start) });
                    }
                }
                return new SimPathResult(0, 0, new List<string>());
            }

            if (!_stationLines.TryGetValue(start, out var startDirs) || startDirs.Count == 0 ||
                !_stationLines.TryGetValue(end, out var endDirs) || endDirs.Count == 0)
                return new SimPathResult(-1, 0, new List<string>());

            if (excludeSpecial)
            {
                startDirs = SimStationUtils.FilterOutSpecialDirs(_cache, startDirs);
                endDirs = SimStationUtils.FilterOutSpecialDirs(_cache, endDirs);
                if (startDirs.Count == 0 || endDirs.Count == 0)
                    return new SimPathResult(-1, 0, new List<string>());
            }

            // 准备终点节点集合（用于终止条件）
            var endNodes = new HashSet<string>();
            foreach (string dir in endDirs)
            {
                string node1 = SimGraphKeyUtils.MakeNode(dir, end);
                endNodes.Add(node1);
            }
            // 取第一个终点节点作为启发式目标（启发式只依赖站点，任意一个均可）
            string targetNodeForHeuristic = endNodes.First();

            var distTotal = new Dictionary<string, double>(); // g 值
            var prev = new Dictionary<string, string>();
            var heap = new PriorityQueue<SimNodeState, double>();

            // 初始化起点
            foreach (string dir in startDirs)
            {
                string lineId = SimGraphKeyUtils.GetLineIdFromDirKey(dir)!;
                string status = SimStationUtils.GetStationLineStatus(_cache, start, lineId);
                if (SimStationUtils.IsClosed(status)) continue;
                if (SimStationUtils.IsFutureStation(_cache, start)) continue;
                string node1 = SimGraphKeyUtils.MakeNode(dir, start);
                distTotal[node1] = 0.0;
                double h = ComputeHeuristic(node1, targetNodeForHeuristic, weight);
                heap.Enqueue(new SimNodeState(node1, 0.0), 0.0 + h);
            }

            string? foundNode = null;

            while (heap.Count > 0)
            {
                var cur = heap.Dequeue();
                string curNode = cur.Node;
                double curG = cur.TotalCost; // 这里存储的是 g
                if (distTotal.TryGetValue(curNode, out double existingG) && existingG < curG - 1e-9)
                    continue;

                // 检查是否到达终点
                if (endNodes.Contains(curNode))
                {
                    foundNode = curNode;
                    break;
                }

                string[] curParsed = SimGraphKeyUtils.ParseNode(curNode);
                string station = curParsed[1];

                if (!_transferGraph.TryGetValue(curNode, out var edges)) continue;

                foreach (var edge in edges)
                {
                    string neighbor = edge.Node;
                    double edgeCost = ComputeEdgeCost(curNode, neighbor, weight, _cache, excludeSpecial, travelTimeMillis);
                    if (edgeCost < 0) continue;

                    double newG = curG + edgeCost;
                    if (!distTotal.TryGetValue(neighbor, out double oldG) || newG < oldG - 1e-9)
                    {
                        distTotal[neighbor] = newG;
                        prev[neighbor] = curNode;
                        double h = ComputeHeuristic(neighbor, targetNodeForHeuristic, weight);
                        heap.Enqueue(new SimNodeState(neighbor, newG), newG + h);
                    }
                }
            }

            if (foundNode == null)
                return new SimPathResult(-1, 0, new List<string>());

            // 回溯路径
            var path = new List<string>();
            string node = foundNode;
            while (prev.TryGetValue(node, out string? p))
            {
                path.Add(node);
                node = p;
            }
            path.Add(node);
            path.Reverse();

            int transfers = CountTransfers(path);
            return new SimPathResult(transfers, distTotal[foundNode], path);
        }

        // ==================== A* 启发式计算 ====================
        /// <summary>
        /// 计算 A* 启发式：估计从当前节点到目标节点的最小可能代价（下界）。
        /// 仅考虑距离和时间权重，忽略换乘、停站、拥挤等离散因素。
        /// 使用地理距离除以全局最高速度估算最小行驶时间。
        /// </summary>
        private double ComputeHeuristic(string fromNode, string toNode, SimSearchWeight weight)
        {
            string fromStation = SimGraphKeyUtils.ParseNode(fromNode)[1];
            string toStation = SimGraphKeyUtils.ParseNode(toNode)[1];

            if (_cache.StationCoords.TryGetValue(fromStation, out double[]? fromCoord) &&
                _cache.StationCoords.TryGetValue(toStation, out double[]? toCoord))
            {
                double geoDist = GeoDistance(fromCoord[0], fromCoord[1], toCoord[0], toCoord[1]);
                // 取所有线路最高速度（km/h），若没有则默认 80
                double maxSpeed = _cache.LineMaxSpeedMap.Values.DefaultIfEmpty(80).Max();
                double minTravelSec = geoDist / (maxSpeed / 3.6);
                return weight.DistanceWeight * geoDist + weight.TimeWeight * minTravelSec;
            }
            // 若无坐标信息，返回 0（退化为 Dijkstra）
            return 0.0;
        }

        /// <summary> 计算两点经纬度距离（千米）</summary>
        private static double GeoDistance(double lon1, double lat1, double lon2, double lat2)
        {
            const double R = 6371.0;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        // ==================== 边代价计算（与原相同） ====================
        private double ComputeEdgeCost(string fromNode, string toNode, SimSearchWeight weight,
                                       SimDataCache cache, bool excludeSpecial, long travelTimeMillis)
        {
            string[] fromParsed = SimGraphKeyUtils.ParseNode(fromNode);
            string[] toParsed = SimGraphKeyUtils.ParseNode(toNode);
            string fromDir = fromParsed[0];
            string toDir = toParsed[0];
            string fromStation = fromParsed[1];
            string toStation = toParsed[1];

            // 换乘边
            if (!SimGraphKeyUtils.IsEndEqual(cache, fromDir, toDir))
            {
                string transferStatus = SimStationUtils.GetTransferStatus(cache, fromStation, fromDir, toDir);
                if (SimStationUtils.IsClosed(transferStatus))
                    return -1;
                double transferTime = SimStationUtils.GetTransferTime(cache, fromStation, fromDir, toDir);
                double waitTime = SimTimeCalculator.ComputeTransferWaitTime(cache, toDir, travelTimeMillis);
                return weight.TransferWeight + weight.TimeWeight * (transferTime + waitTime) / 60;
            }

            // 同一方向内边
            if (excludeSpecial && SimStationUtils.IsSpecialDirKey(cache, fromDir))
                return -1;

            if (!_lineAdjDist.TryGetValue(fromDir, out var adj))
                return -1;

            double? addDist = null;
            if (adj.TryGetValue(fromStation, out var fromMap) && fromMap.TryGetValue(toStation, out double d1))
                addDist = d1;
            else if (adj.TryGetValue(toStation, out var toMap) && toMap.TryGetValue(fromStation, out double d2))
                addDist = d2;

            if (!addDist.HasValue)
                return -1;

            string lineId = SimGraphKeyUtils.GetLineIdFromDirKey(fromDir)!;
            int maxSpeed = cache.LineMaxSpeedMap.TryGetValue(lineId, out int speed) ? speed : 80;

            // 应用限速
            var speedLimits = cache.SpeedLimits;
            if (speedLimits != null)
            {
                foreach (var sl in speedLimits)
                {
                    if (sl.DirectionKey == fromDir &&
                        sl.FromStation == fromStation &&
                        sl.ToStation == toStation)
                    {
                        maxSpeed = sl.SpeedKmh;
                        break;
                    }
                }
            }

            string formation = cache.LineFormationMap.TryGetValue(lineId, out string? f) ? f : "6B";
            double powerKw = cache.LinePowerKwMap.TryGetValue(lineId, out double p) ? p : 2000.0;
            double brake = cache.LineBrakeMap.TryGetValue(lineId, out double b) ? b : 0.85;

            double segmentTime = SimTrainTimeUtils.ComputeSegmentTime(
                addDist.Value, formation, maxSpeed, powerKw, brake);

            double cost = weight.DistanceWeight * addDist.Value
                        + weight.TimeWeight * segmentTime / 60
                        + weight.StationWeight * 1;

            if (weight.CrowdWeight > 0)
            {
                double crowdFactor = SimTimeCalculator.FindCrowdFactor(cache, fromDir, travelTimeMillis);
                if (crowdFactor > 1.0)
                    cost += weight.CrowdWeight * (crowdFactor - 1.0);
            }

            return cost;
        }

        private int CountTransfers(List<string> path)
        {
            int transfers = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                string dir1 = SimGraphKeyUtils.ParseNode(path[i])[0];
                string dir2 = SimGraphKeyUtils.ParseNode(path[i + 1])[0];
                if (dir1 != dir2) transfers++;
            }
            return transfers;
        }

        /// <summary>
        /// 计算路径的总行驶距离（米）。仅累加同方向相邻站间的距离，不含换乘。
        /// </summary>
        /// <param name="path">路径节点列表</param>
        /// <returns>总行驶距离（米）</returns>
        public double ComputePathDistance(List<string> path)
        {
            double total = 0.0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                string[] p1 = SimGraphKeyUtils.ParseNode(path[i]);
                string[] p2 = SimGraphKeyUtils.ParseNode(path[i + 1]);
                string dir1 = p1[0], st1 = p1[1];
                string dir2 = p2[0], st2 = p2[1];
                if (st1 == st2 || dir1 != dir2) continue;
                if (!_lineAdjDist.TryGetValue(dir1, out var adj)) continue;
                if (adj.TryGetValue(st1, out var map1) && map1.TryGetValue(st2, out double d))
                    total += d;
                else if (adj.TryGetValue(st2, out var map2) && map2.TryGetValue(st1, out d))
                    total += d;
            }
            return total;
        }

        /// <summary>
        /// 计算路径中不重复的站点数量。
        /// </summary>
        /// <param name="path">路径节点列表</param>
        /// <returns>唯一站点数</returns>
        public int GetUniqueStationCount(List<string> path)
        {
            if (path == null || path.Count == 0) return 0;
            var stationSet = new HashSet<string>();
            foreach (string node in path)
                stationSet.Add(SimGraphKeyUtils.ParseNode(node)[1]);
            return stationSet.Count;
        }

        /// <summary>
        /// 获取指定方向中相邻两站间的距离。
        /// </summary>
        /// <param name="dirKey">方向键</param>
        /// <param name="from">起点站名</param>
        /// <param name="to">终点站名</param>
        /// <returns>站间距离（米），未找到返回 -1</returns>
        public double GetDistanceInDirection(string dirKey, string from, string to)
        {
            if (!_lineAdjDist.TryGetValue(dirKey, out var adj)) return -1;
            if (adj.TryGetValue(from, out var map1) && map1.TryGetValue(to, out double d1))
                return d1;
            if (adj.TryGetValue(to, out var map2) && map2.TryGetValue(from, out double d2))
                return d2;
            return -1;
        }

        public static bool IsActualEndOfSegment(SimDataCache cache, string segStart, string segEnd, string candidate)
        {
            var dirStations = cache.DirStations;
            if (dirStations == null) return false;

            foreach (var kv in dirStations)
            {
                string dirKey = kv.Key;
                var stList = kv.Value;
                if (stList == null) continue;

                int idxStart = stList.IndexOf(segStart);
                int idxEnd = stList.IndexOf(segEnd);
                if (idxStart < 0 || idxEnd < 0 || idxStart == idxEnd) continue;

                string[] parts = SimGraphKeyUtils.ParseDirKey(dirKey);
                if (parts.Length < 3) continue;
                string start = parts[1];
                string end = parts[2];
                string actualEnd = idxStart < idxEnd ? end : start;

                if (candidate.Contains(actualEnd) || actualEnd.Contains(candidate))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 将路径节点列表转换为行程分段列表，包含每段的线路名、方向、起讫站、站数等信息。
        /// </summary>
        /// <param name="path">路径节点列表</param>
        /// <returns>行程分段列表</returns>
        public List<SimRouteSegment> GetRouteSegments(List<string> path)
        {
            var segments = new List<SimRouteSegment>();
            if (path == null || path.Count < 2) return segments;

            var rawSegments = new List<SimSegment>();
            string[] firstParsed = SimGraphKeyUtils.ParseNode(path[0]);
            string curDir = firstParsed[0];
            var curStations = new List<string> { firstParsed[1] };

            for (int i = 1; i < path.Count; i++)
            {
                string[] parsed = SimGraphKeyUtils.ParseNode(path[i]);
                string dir = parsed[0];
                string st = parsed[1];
                if (dir == curDir)
                    curStations.Add(st);
                else
                {
                    rawSegments.Add(new SimSegment(curDir, curStations));
                    curDir = dir;
                    curStations = new List<string> { st };
                }
            }
            rawSegments.Add(new SimSegment(curDir, curStations));

            for (int idx = 0; idx < rawSegments.Count; idx++)
            {
                var seg = rawSegments[idx];
                string dirKey = seg.DirKey;
                var stations = seg.Stations;
                if (stations.Count == 0) continue;

                string startStation = stations[0];
                string endStation = stations[^1];

                bool isOutStationTransfer = false;
                if (idx > 0)
                {
                    string prevDirKey = rawSegments[idx - 1].DirKey;
                    string status = SimStationUtils.GetTransferStatus(_cache, startStation, prevDirKey, dirKey);
                    if (status == "out_station")
                        isOutStationTransfer = true;
                }

                string[] dirParts = SimGraphKeyUtils.ParseDirKey(dirKey);
                string lineId = dirParts[0];
                string start = dirParts[1];
                string end = dirParts[2];

                string actualEnd = end;
                if (_dirStations.TryGetValue(dirKey, out var stList) && stations.Count >= 2)
                {
                    string first = stations[0];
                    string last = stations[^1];
                    int idxFirst = stList.IndexOf(first);
                    int idxLast = stList.IndexOf(last);
                    if (idxFirst >= 0 && idxLast >= 0)
                        actualEnd = idxFirst < idxLast ? end : start;
                }

                string directionDisplay = BuildDirectionDisplay(lineId, dirKey, stations, actualEnd);
                string lineName = _dirDisplayNameMap.TryGetValue(dirKey, out string? name) ? name : lineId;
                int stationCount = stations.Count - 1;

                segments.Add(new SimRouteSegment(
                    startStation, lineId, lineName,
                    directionDisplay, endStation, stationCount,
                    isOutStationTransfer, dirKey
                ));
            }

            return segments;
        }

        private string BuildDirectionDisplay(string lineId, string dirKey, List<string> stations,
                                             string actualEnd)
        {
            if (_hasLoopMap.TryGetValue(lineId, out bool isLoop) && isLoop)
            {
                string dirType = _cache.DirDirectionTypeMap.TryGetValue(dirKey, out string? dt) ? dt : "";
                if (dirType == "inner")
                    return stations[1] + "（内环）";
                else if (dirType == "outer")
                    return stations[1] + "（外环）";
                else
                    return actualEnd;
            }
            else
            {
                var allEnds = new List<string> { actualEnd };
                var uniqueEnds = allEnds.Distinct()
                    .Where(e => IsActualEndOfSegment(_cache, stations[0], stations[^1], e))
                    .ToList();

                var finalEnds = new List<string>();
                foreach (string e in uniqueEnds)
                {
                    string status = SimStationUtils.GetStationLineStatus(_cache, e, lineId);
                    if (SimStationUtils.IsClosed(status) || SimStationUtils.IsFutureStation(_cache, e))
                    {
                        if (_dirStations.TryGetValue(dirKey, out var stList) && stList.Count > 0)
                        {
                            int startIdx = stList.IndexOf(stations[0]);
                            int endIdx = stList.IndexOf(stations[^1]);
                            if (startIdx >= 0 && endIdx >= 0)
                            {
                                int step = startIdx < endIdx ? -1 : 1;
                                int idx = stList.Count - 1;
                                while (idx >= 0 && idx < stList.Count)
                                {
                                    string candidate = stList[idx];
                                    string candStatus = SimStationUtils.GetStationLineStatus(_cache, candidate, lineId);
                                    if (!SimStationUtils.IsClosed(candStatus) && !SimStationUtils.IsFutureStation(_cache, candidate))
                                    {
                                        finalEnds.Add(candidate);
                                        break;
                                    }
                                    idx += step;
                                }
                            }
                        }
                    }
                    else
                    {
                        finalEnds.Add(e);
                    }
                }
                if (finalEnds.Count == 0)
                    finalEnds = uniqueEnds;

                return string.Join(" / ", finalEnds);
            }
        }

        /// <summary>
        /// 将路径格式化为自然语言乘车指引文本。
        /// </summary>
        /// <param name="path">路径节点列表</param>
        /// <returns>格式化的乘车指引字符串</returns>
        public string FormatRouteWithDirection(List<string> path)
        {
            var segments = GetRouteSegments(path);
            if (path == null || path.Count == 0) return "无效的路径！";
            if (path.Count == 1) return "无需乘坐地铁！";

            var clauses = new List<string>();

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                string line = seg.LineName;
                string dir = seg.Direction;
                string end = seg.EndStation;
                int count = seg.StationCount;
                string endQuote = $"【{end}】";

                if (i == 0)
                {
                    string startQuote = $"【{seg.StartStation}】";
                    // 核心改动：用“开往”替代括号
                    clauses.Add($"在{startQuote}上车，先乘坐开往{dir}方向的{line}，坐{count}站到{endQuote}");
                }
                else if (i == segments.Count - 1)
                {
                    clauses.Add($"最后换乘开往{dir}方向的{line}，坐{count}站即可到达{endQuote}");
                }
                else
                {
                    clauses.Add($"再换乘开往{dir}方向的{line}，坐{count}站到{endQuote}");
                }
            }

            return string.Join("；", clauses) + "。";
        }

        /// <summary>
        /// 随机路径搜索：在最优路径的基础上引入随机扰动，生成多样化的可选路径。
        /// 使用 Box-Muller 正态分布生成站点随机权重，通过随机化 Dijkstra 探索不同路径。
        /// </summary>
        /// <param name="start">起点站名</param>
        /// <param name="end">终点站名</param>
        /// <param name="weight">搜索权重配置</param>
        /// <param name="maxAttempts">最大尝试次数</param>
        /// <returns>满足时间约束的随机路径，或最优路径作为兜底</returns>
        public SimPathResult FindRandomPath(string start, string end, SimSearchWeight weight, int maxAttempts)
        {
            var shortest = FindTransferPath(start, end, SimSearchWeight.GetShortestWeight());
            double maxHours = 4.0;
            double minHours = shortest.Cost * 1.2 / 60.0;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var rand = new Random(Guid.NewGuid().GetHashCode());
                var sortedStations = new List<string>(_stationLines.Keys);
                sortedStations.Sort();
                var stationRandomWeight = new Dictionary<string, double>();
                foreach (string station in sortedStations)
                    stationRandomWeight[station] = NextGaussian(rand);

                var result = DoRandomDijkstra(start, end, weight, stationRandomWeight);
                if (result != null && result.Transfers != -1)
                {
                    double totalHours = result.Cost / 3600.0;
                    if (totalHours <= maxHours && totalHours >= minHours)
                    {
                        var fare = SimFareCalculator.CalculateFareWithoutSpecial(_cache, result.Path, this);
                        result.TotalMoney = fare.Total;
                        result.SpecialMoney = fare.Special;
                        var unique = SimPrefixFilter.FilterUniquePrefixBlocksWithCount(result.Path);
                        result.Path = unique.FilteredList;
                        result.Transfers -= unique.RemovedCount;
                        return result;
                    }
                }
            }
            return shortest;
        }

        /// <summary>
        /// 最长路径搜索：并行尝试多条随机路径，返回满足时间约束的最长路径。
        /// </summary>
        /// <param name="start">起点站名</param>
        /// <param name="end">终点站名</param>
        /// <param name="weight">搜索权重配置</param>
        /// <param name="maxAttempts">最大并行尝试次数</param>
        /// <returns>满足时间约束的最长路径</returns>
        public SimPathResult FindLongestPath(string start, string end, SimSearchWeight weight, int maxAttempts)
        {
            var longest = FindTransferPath(start, end, SimSearchWeight.GetShortestWeight());
            double maxHours = 4.0;
            double minHours = longest.Cost * 1.2 / 60.0;

            var tasks = new List<Task<SimPathResult?>>();
            for (int i = 0; i < maxAttempts; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var rand = new Random(Guid.NewGuid().GetHashCode());
                    var sortedStations = new List<string>(_stationLines.Keys);
                    sortedStations.Sort();
                    var stationRandomWeight = new Dictionary<string, double>();
                    foreach (string station in sortedStations)
                        stationRandomWeight[station] = NextGaussian(rand);

                    var result = DoRandomDijkstra(start, end, weight, stationRandomWeight);
                    if (result != null && result.Transfers != -1)
                    {
                        double totalHours = result.Cost / 3600.0;
                        if (totalHours <= maxHours && totalHours >= minHours)
                        {
                            var fare = SimFareCalculator.CalculateFareWithoutSpecial(_cache, result.Path, this);
                            result.TotalMoney = fare.Total;
                            result.SpecialMoney = fare.Special;
                            return result;
                        }
                    }
                    return null;
                }));
            }

            Task.WaitAll(tasks.ToArray());
            foreach (var t in tasks)
            {
                var result = t.Result;
                if (result != null && result.Cost > longest.Cost)
                {
                    longest = result;
                    var unique = SimPrefixFilter.FilterUniquePrefixBlocksWithCount(longest.Path);
                    longest.Path = unique.FilteredList;
                    longest.Transfers -= unique.RemovedCount;
                }
            }
            return longest;
        }

        private SimPathResult? DoRandomDijkstra(string start, string end, SimSearchWeight weight,
                                                Dictionary<string, double> stationRandomWeight)
        {
            if (start == end)
            {
                if (_stationLines.TryGetValue(start, out var dirs) && dirs.Count > 0)
                    return new SimPathResult(0, 0, new List<string> { SimGraphKeyUtils.MakeNode(dirs.First(), start) });
                return new SimPathResult(0, 0, new List<string>());
            }

            if (!_stationLines.TryGetValue(start, out var startDirs) || startDirs.Count == 0 ||
                !_stationLines.TryGetValue(end, out var endDirs) || endDirs.Count == 0)
                return null;

            var dist = new Dictionary<string, double>();
            var prev = new Dictionary<string, string>();
            var visitedStations = new HashSet<string>();
            var heap = new PriorityQueue<SimNodeState, double>();
            var rand = new Random();

            foreach (string dir in startDirs)
            {
                string node1 = SimGraphKeyUtils.MakeNode(dir, start);
                dist[node1] = 0.0;
                heap.Enqueue(new SimNodeState(node1, 0.0), 0.0);
            }

            string? foundNode = null;
            while (heap.Count > 0)
            {
                var first = heap.Dequeue();
                heap.TryPeek(out var second, out _);
                SimNodeState cur;
                if (second != null && rand.NextDouble() < 0.5)
                {
                    heap.Dequeue();
                    heap.Enqueue(first, first.TotalCost);
                    cur = second;
                }
                else
                {
                    cur = first;
                }

                string curNode = cur.Node;
                double curCost = cur.TotalCost;
                if (dist.TryGetValue(curNode, out double existing) && existing < curCost - 1e-9)
                    continue;

                string[] parsed = SimGraphKeyUtils.ParseNode(curNode);
                string station = parsed[1];

                if (station == end)
                {
                    foundNode = curNode;
                    break;
                }

                if (!_transferGraph.TryGetValue(curNode, out var edges)) continue;

                foreach (var edge in edges)
                {
                    string neighborNode = edge.Node;
                    string[] neighborParsed = SimGraphKeyUtils.ParseNode(neighborNode);
                    string neighborStation = neighborParsed[1];

                    if (visitedStations.Contains(neighborStation)) continue;

                    if (ComputeEdgeCost(curNode, neighborNode, weight, _cache, false, 0) < 0)
                        continue;

                    double edgeCost = stationRandomWeight.TryGetValue(neighborStation, out double rw) ? rw : 0.0;
                    double newCost = curCost + edgeCost;
                    if (!dist.TryGetValue(neighborNode, out double oldCost) || newCost < oldCost - 1e-9)
                    {
                        dist[neighborNode] = newCost;
                        prev[neighborNode] = curNode;
                        heap.Enqueue(new SimNodeState(neighborNode, newCost), newCost);
                    }
                }

                visitedStations.Add(station);
            }

            if (foundNode == null) return null;

            var path = new List<string>();
            string node = foundNode;
            while (prev.TryGetValue(node, out string? p))
            {
                path.Add(node);
                node = p;
            }
            path.Add(node);
            path.Reverse();

            int transfers = CountTransfers(path);
            double totalTimeSec = SimTimeCalculator.ComputeTotalTime(_cache, path, this, weight, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            return new SimPathResult(transfers, totalTimeSec, path);
        }

        /// <summary>
        /// Box-Muller 变换生成标准正态分布随机数
        /// </summary>
        private static double NextGaussian(Random rand)
        {
            double u1 = 1.0 - rand.NextDouble();
            double u2 = 1.0 - rand.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        /// <summary>
        /// 获取图中所有站点名称集合。
        /// </summary>
        /// <returns>站点名称集合的副本</returns>
        public HashSet<string> GetAllStations() => new(_stationLines.Keys);
    }
}