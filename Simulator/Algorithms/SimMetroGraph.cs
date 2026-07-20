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
    /// 地铁线路图核心数据结构与路径规划算法（从 Android MetroGraph 转换）
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

        // ==================== 主支线相关映射 ====================
        private readonly Dictionary<string, bool> _dirIsBranchMap = new();
        private readonly Dictionary<string, HashSet<string>> _lineMainStationsMap = new();
        private readonly Dictionary<string, HashSet<string>> _lineBranchOnlyStationsMap = new();
        private readonly Dictionary<string, List<string>> _lineBranchEndsMap = new();
        private readonly SimDataCache _cache = new();

        public SimDataCache Cache { get { return _cache; } }

        public SimMetroGraph(SimDataCache simDataCache)
        {
            _cache = simDataCache;

            BuildGraphs();
        }

        // ==================== 构建图 ====================
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
                    if (dir.IsFast || dir.IsFuture) continue;

                    string start = dir.Start;
                    string end = dir.End;
                    bool isLoop = dir.IsLoop;
                    bool isBranch = dir.IsBranch;
                    string dirKey = SimGraphKeyUtils.MakeDirKey(lineId, start, end);
                    _hasLoopMap[lineId] = isLoop;
                    _dirIsBranchMap[dirKey] = isBranch;

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

        // ==================== 后处理：主线/支线辅助映射 ====================
        private void BuildDirDisplayNames()
        {
            // 1. 分组
            var lineMainDirs = new Dictionary<string, List<string>>();
            var lineBranchDirs = new Dictionary<string, List<string>>();
            foreach (var kv in _dirIsBranchMap)
            {
                string dirKey = kv.Key;
                bool isBranch = kv.Value;
                string lineId = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey)!;
                if (isBranch)
                {
                    if (!lineBranchDirs.ContainsKey(lineId)) lineBranchDirs[lineId] = new();
                    lineBranchDirs[lineId].Add(dirKey);
                }
                else
                {
                    if (!lineMainDirs.ContainsKey(lineId)) lineMainDirs[lineId] = new();
                    lineMainDirs[lineId].Add(dirKey);
                }
            }

            // 2. 主线站点集合
            _lineMainStationsMap.Clear();
            foreach (var kv in lineMainDirs)
            {
                string lineId = kv.Key;
                var mainStations = new HashSet<string>();
                foreach (string dk in kv.Value)
                {
                    if (_dirStations.TryGetValue(dk, out var sts))
                        mainStations.UnionWith(sts);
                }
                _lineMainStationsMap[lineId] = mainStations;
            }

            // 3. 支线站点集合
            var lineBranchStationsMap = new Dictionary<string, HashSet<string>>();
            foreach (var kv in lineBranchDirs)
            {
                string lineId = kv.Key;
                var branchStations = new HashSet<string>();
                foreach (string dk in kv.Value)
                {
                    if (_dirStations.TryGetValue(dk, out var sts))
                        branchStations.UnionWith(sts);
                }
                lineBranchStationsMap[lineId] = branchStations;
            }

            // 4. 支线独有站点
            _lineBranchOnlyStationsMap.Clear();
            foreach (var lineId in lineMainDirs.Keys)
            {
                var main = _lineMainStationsMap.TryGetValue(lineId, out var m) ? m : new HashSet<string>();
                var branch = lineBranchStationsMap.TryGetValue(lineId, out var b) ? b : new HashSet<string>();
                var branchOnly = new HashSet<string>(branch);
                branchOnly.ExceptWith(main);
                _lineBranchOnlyStationsMap[lineId] = branchOnly;
            }

            // 5. 支线终点
            _lineBranchEndsMap.Clear();
            foreach (var kv in lineBranchDirs)
            {
                string lineId = kv.Key;
                var ends = new HashSet<string>();
                foreach (string dk in kv.Value)
                {
                    string[] parts = SimGraphKeyUtils.ParseDirKey(dk);
                    if (parts.Length >= 3)
                        ends.Add(parts[2]);
                }
                _lineBranchEndsMap[lineId] = ends.ToList();
            }

            // 6. 方向显示名称
            var dirNickNameMap = _cache.DirNickNameMap;
            foreach (string dirKey in _dirStations.Keys)
            {
                if (dirNickNameMap.TryGetValue(dirKey, out string? nick) && !string.IsNullOrEmpty(nick))
                {
                    _dirDisplayNameMap[dirKey] = nick;
                }
                else if (_dirIsBranchMap.TryGetValue(dirKey, out bool isBranch) && isBranch)
                {
                    string lineId = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey)!;
                    string city = _lineCityMap[lineId];
                    string lineName = _lineNameMap[lineId];
                    _dirDisplayNameMap[dirKey] = BuildDisplayName(city, lineName) + "(支线)";
                }
                else
                {
                    string lineId = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey)!;
                    string city = _lineCityMap[lineId];
                    string lineName = _lineNameMap[lineId];
                    _dirDisplayNameMap[dirKey] = BuildDisplayName(city, lineName);
                }
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

        // ==================== 路径查找（公共入口） ====================
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

        public SimPathResult FindTransferPath(string start, string end, SimSearchWeight weight)
        {
            return FindTransferPath(start, end, weight, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        // ==================== 内部 Dijkstra ====================
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

            var distTotal = new Dictionary<string, double>();
            var prev = new Dictionary<string, string>();
            var heap = new PriorityQueue<SimNodeState, double>();

            foreach (string dir in startDirs)
            {
                string lineId = SimGraphKeyUtils.GetLineIdFromDirKey(dir)!;
                string status = SimStationUtils.GetStationLineStatus(_cache, start, lineId);
                if (SimStationUtils.IsClosed(status)) continue;
                if (SimStationUtils.IsFutureStation(_cache, start)) continue;
                string node1 = SimGraphKeyUtils.MakeNode(dir, start);
                distTotal[node1] = 0.0;
                heap.Enqueue(new SimNodeState(node1, 0.0), 0.0);
            }

            string? foundNode = null;

            while (heap.Count > 0)
            {
                var cur = heap.Dequeue();
                string curNode = cur.Node;
                double curCost = cur.TotalCost;
                if (distTotal.TryGetValue(curNode, out double existing) && existing < curCost - 1e-9)
                    continue;

                string[] curParsed = SimGraphKeyUtils.ParseNode(curNode);
                string station = curParsed[1];

                if (station == end)
                {
                    string dirKey = curParsed[0];
                    if (excludeSpecial && SimStationUtils.IsSpecialDirKey(_cache, dirKey)) continue;
                    string lineId = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey)!;
                    string status = SimStationUtils.GetStationLineStatus(_cache, end, lineId);
                    if (SimStationUtils.IsClosed(status)) continue;
                    foundNode = curNode;
                    break;
                }

                if (!_transferGraph.TryGetValue(curNode, out var edges)) continue;

                foreach (var edge in edges)
                {
                    string neighbor = edge.Node;
                    double edgeCost = ComputeEdgeCost(curNode, neighbor, weight, _cache, excludeSpecial, travelTimeMillis);
                    if (edgeCost < 0) continue;

                    double newCost = curCost + edgeCost;
                    if (!distTotal.TryGetValue(neighbor, out double oldCost) || newCost < oldCost - 1e-9)
                    {
                        distTotal[neighbor] = newCost;
                        prev[neighbor] = curNode;
                        heap.Enqueue(new SimNodeState(neighbor, newCost), newCost);
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

        // ==================== 边代价计算 ====================
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
                return weight.TransferWeight + weight.TimeWeight * (transferTime + waitTime);
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

            // 获取编组、功率、制动
            string formation = cache.LineFormationMap.TryGetValue(lineId, out string? f) ? f : "6B";
            double powerKw = cache.LinePowerKwMap.TryGetValue(lineId, out double p) ? p : 2000.0;
            double brake = cache.LineBrakeMap.TryGetValue(lineId, out double b) ? b : 0.85;

            double segmentTime = SimTrainTimeUtils.ComputeSegmentTime(
                addDist.Value, formation, maxSpeed, powerKw, brake);

            double cost = weight.DistanceWeight * addDist.Value
                        + weight.TimeWeight * segmentTime
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

        // ==================== 路径信息查询 ====================
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

        public int GetUniqueStationCount(List<string> path)
        {
            if (path == null || path.Count == 0) return 0;
            var stationSet = new HashSet<string>();
            foreach (string node in path)
                stationSet.Add(SimGraphKeyUtils.ParseNode(node)[1]);
            return stationSet.Count;
        }

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

        // ==================== 获取行程分段 ====================
        public List<SimRouteSegment> GetRouteSegments(List<string> path)
        {
            var segments = new List<SimRouteSegment>();
            if (path == null || path.Count < 2) return segments;

            // 拆分为连续同方向段
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

            var dirNickColorMap = _cache.DirNickColorMap;

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

                // 判断实际终点
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

                // 判断是否包含支线独有站点
                bool isBranchSegment = false;
                if (_lineBranchOnlyStationsMap.TryGetValue(lineId, out var branchOnly))
                {
                    foreach (string station in stations)
                    {
                        if (branchOnly.Contains(station))
                        {
                            isBranchSegment = true;
                            break;
                        }
                    }
                }

                string directionDisplay = BuildDirectionDisplay(lineId, dirKey, stations, actualEnd, isBranchSegment);
                string lineName = _dirDisplayNameMap.TryGetValue(dirKey, out string? name) ? name : lineId;
                int stationCount = stations.Count - 1;
                dirNickColorMap.TryGetValue(dirKey, out string? nickColor);

                segments.Add(new SimRouteSegment(
                    startStation, lineId, lineName,
                    directionDisplay, endStation, stationCount,
                    isBranchSegment, nickColor, isOutStationTransfer, dirKey
                ));
            }

            return segments;
        }

        private string BuildDirectionDisplay(string lineId, string dirKey, List<string> stations,
                                             string actualEnd, bool isBranchSegment)
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
            else if (isBranchSegment)
            {
                return actualEnd;
            }
            else
            {
                var branchEnds = _lineBranchEndsMap.TryGetValue(lineId, out var ends) ? ends : new List<string>();
                var allEnds = new List<string>(branchEnds) { actualEnd };
                var uniqueEnds = allEnds.Distinct()
                    .Where(e => IsActualEndOfSegment(_cache, stations[0], stations[^1], e))
                    .ToList();

                // 替换暂缓开通站
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

        // ==================== 格式化乘车指引 ====================
        public string FormatRouteWithDirection(List<string> path)
        {
            var segments = GetRouteSegments(path);
            if (segments.Count == 0) return "不可达";
            if (path.Count == 1) return "无需乘坐地铁";

            var descParts = new List<string>();
            for (int idx = 0; idx < segments.Count; idx++)
            {
                var seg = segments[idx];
                string part = BuildRouteDescription(seg, idx);
                descParts.Add(part);
            }
            return string.Join("；", descParts);
        }

        private string BuildRouteDescription(SimRouteSegment seg, int idx)
        {
            string lineDisplay = seg.LineName;
            string directionQuote = "\"" + seg.Direction + "\"";
            string endQuote = "\"" + seg.EndStation + "\"";
            if (idx == 0)
                return $"乘坐开往{directionQuote}的{lineDisplay}，{seg.StationCount}站后到达{endQuote}";
            else
            {
                string transferQuote = "\"" + seg.StartStation + "\"";
                string transferType = seg.IsOutStationTransfer ? "出站换乘" : "换乘";
                return $"再{transferQuote}{transferType}开往{directionQuote}的{lineDisplay}，{seg.StationCount}站后到达{endQuote}";
            }
        }

        // ==================== 随机路径 ====================
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
                // 随机化选择：从堆顶的两个候选节点中随机选一个
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
            // 使用 Box-Muller 算法，生成两个独立的标准正态变量
            double u1 = 1.0 - rand.NextDouble(); // 避免取到 0
            double u2 = 1.0 - rand.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        // ==================== 访问器 ====================
        public HashSet<string> GetAllStations() => new(_stationLines.Keys);
    }
}