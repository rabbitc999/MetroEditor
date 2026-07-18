using System;
using System.Collections.Generic;
using System.Linq;
using MetroEditor.MetroLang.DataModel;
using Microsoft.Win32;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.PathFinding
{
    /// <summary>
    /// 地铁路网图结构（核心路径计算引擎）
    /// 职责：构建内存中的地铁路网拓扑，执行路径搜索和结果分段
    /// 核心特性：
    /// 1. 支持多城市、多线路、复杂换乘关系
    /// 2. 集成物理运行时间、换乘等待时间、票价计算
    /// 3. 智能处理未来线路、支线、环线等特殊场景
    /// 4. 提供调试日志和性能监控
    /// </summary>
    /// <remarks>
    /// 图模型设计：
    /// - 节点：格式为"方向键|车站名"，表示特定方向上的车站
    /// - 边：分为两类：
    ///   a) 运行边：同方向相邻车站间的连接（权重为运行代价）
    ///   b) 换乘边：同一车站不同方向间的连接（权重为换乘代价）
    /// 
    /// 搜索算法：改进的Dijkstra算法（使用SortedSet作为优先队列）
    /// 代价计算：综合距离、时间、票价、换乘次数等多维度因素
    /// </remarks>
    public class MetroGraph
    {
        #region 调试支持
#if DEBUG
        /// <summary>调试开关：控制是否输出详细日志</summary>
        private static bool DebugEnabled = false;

        /// <summary>调试日志输出方法</summary>
        /// <param name="msg">日志消息</param>
        private static void DebugLog(string msg)
        {
            if (DebugEnabled) System.Diagnostics.Debug.WriteLine("[MetroGraph] " + msg);
        }
#else
        // Release版本中为空实现，避免性能损耗
        private static void DebugLog(string msg) { }
#endif
        #endregion

        #region 核心数据字段
        /// <summary>解析后的路网业务数据（只读）</summary>
        private readonly ParsedData _data;

        /// <summary>邻接表表示的图结构：节点→边列表</summary>
        private readonly Dictionary<string, List<Edge>> _graph = new();

        /// <summary>方向键到车站序列的映射</summary>
        private readonly Dictionary<string, List<string>> _dirStations = new();

        /// <summary>方向键到相邻距离映射的映射（车站对→距离）</summary>
        private readonly Dictionary<string, Dictionary<string, double>> _adjDist = new();

        /// <summary>线路ID到显示名称的映射</summary>
        private readonly Dictionary<string, string> _lineNameMap = new();

        /// <summary>线路ID到是否环线的映射</summary>
        private readonly Dictionary<string, bool> _hasLoopMap = new();

        /// <summary>方向键到是否支线的映射</summary>
        private readonly Dictionary<string, bool> _dirIsBranchMap = new();

        /// <summary>车站到所属方向键集合的映射</summary>
        private readonly Dictionary<string, HashSet<string>> _stationLines = new();

        /// <summary>
        /// 内部方向Key
        /// </summary>
        private readonly Dictionary<string, DirectionInfo> _externalKeyToDirInfo = new();
        #endregion

        #region 主线/支线辅助字段（用于分段展示）
        /// <summary>线路ID到主线车站集合的映射</summary>
        private readonly Dictionary<string, HashSet<string>> _lineMainStations = new();

        /// <summary>线路ID到支线独有车站集合的映射</summary>
        private readonly Dictionary<string, HashSet<string>> _lineBranchOnlyStations = new();

        /// <summary>线路ID到支线终点站列表的映射</summary>
        private readonly Dictionary<string, List<string>> _lineBranchEnds = new();

        /// <summary>方向键到显示名称的映射（含昵称、支线标识等）</summary>
        private readonly Dictionary<string, string> _dirDisplayNameMap = new();
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化地铁路网图
        /// </summary>
        /// <param name="data">解析后的路网业务数据</param>
        /// <remarks>
        /// 初始化过程：
        /// 1. 构建运行边（同方向相邻车站）
        /// 2. 构建换乘边（同一车站不同方向）
        /// 3. 建立主线/支线辅助映射
        /// 4. 生成方向显示名称
        /// </remarks>
        public MetroGraph(ParsedData data)
        {
            _data = data;
            BuildGraph();
        }
        #endregion

        #region 图构建核心逻辑
        /// <summary>
        /// 构建完整的地铁路网图
        /// </summary>
        private void BuildGraph()
        {
            var keyCity = _data.MainCity;
            DebugLog($"开始构建地铁图，主城市: {keyCity}, 线路数: {_data.Lines.Count}");

            // ===== 步骤1：构建运行边（同方向相邻车站）=====
            // 遍历所有方向定义，过滤未来线路和快车线路
            foreach (var dirKv in _data.Directions)
            {
                var dir = dirKv.Value;
                // 跳过未来线路和快车线路（不参与常规路径计算）
                if (dir.IsFuture || dir.Attrs.Contains("FAST")) continue;

                var lineId = dir.LineId;
                var dirId = dirKv.Key; // 方向内部ID（如"L1_0"）

                // 验证线路是否存在
                if (!_data.Lines.TryGetValue(lineId, out var lineInfo)) continue;

                // 生成线路显示名称（跨城市时添加城市前缀）
                var city = lineInfo.City ?? _data.MainCity;
                var baseName = lineInfo.Name ?? lineId;
                _lineNameMap[lineId] = city == _data.MainCity ? baseName : city + baseName;

                // 生成方向键（格式：线路ID|起点站|终点站）
                var dirKey = GraphKeyUtils.MakeDirKey(lineId, dir.Stations.First(), dir.Stations.Last());
                DebugLog($"  方向 ID: {dirId}, 方向键: {dirKey}, 站点数: {dir.Stations.Count}");

                // 在生成 dirKey 之后，添加：
                var externalKey = GraphKeyUtils.MakeDirKey(lineId, dir.Stations.First(), dir.Stations.Last());
                _externalKeyToDirInfo[externalKey] = dir;  // dir 是 DirectionInfo

                // 存储方向到车站序列的映射
                _dirStations[dirKey] = dir.Stations.ToList();
                // 标记是否为支线
                _dirIsBranchMap[dirKey] = dir.Attrs.Contains("BRANCH");
                // 标记是否为环线
                if (dir.Attrs.Contains("LOOP"))
                    _hasLoopMap[lineId] = true;

                // ===== 构建相邻距离映射 =====
                // 从距离数据中获取该方向的站间距
                if (_data.Distances.TryGetValue(dirId, out var distList) && distList.Count > 0)
                {
                    DebugLog($"    距离列表长度: {distList.Count}");
                    var adj = new Dictionary<string, double>();
                    var stations = dir.Stations;

                    // 构建相邻车站对的距离映射（双向）
                    for (int i = 0; i < distList.Count && i < stations.Count - 1; i++)
                    {
                        double distance = distList[i];
                        if (distance > 0 && distance < 1e30) // 有效距离阈值
                        {
                            var from = stations[i];
                            var to = stations[i + 1];
                            // 存储双向距离（A→B和B→A相同）
                            adj[from + "|" + to] = distance;
                            adj[to + "|" + from] = distance;
                        }
                    }

                    // 环线特殊处理：添加首尾闭合边
                    if (dir.Attrs.Contains("LOOP") && stations.Count > 1 && distList.Count >= stations.Count)
                    {
                        double lastDist = distList[distList.Count - 1];
                        if (lastDist > 0 && lastDist < 1e30)
                        {
                            var first = stations.First();
                            var last = stations.Last();
                            adj[first + "|" + last] = lastDist;
                            adj[last + "|" + first] = lastDist;
                        }
                    }
                    _adjDist[dirKey] = adj;
                }
                else
                {
                    DebugLog($"    距离列表: 未找到或为空 (dirId={dirId})");
                }

                // ===== 添加运行边 =====
                // 遍历方向内所有相邻车站对，添加双向边
                var stationsList = dir.Stations;
                for (int i = 0; i < stationsList.Count - 1; i++)
                {
                    string from = stationsList[i];
                    string to = stationsList[i + 1];
                    // 跳过暂缓/关闭车站
                    if (!IsStationPendingOnLine(from, lineId) && !IsStationPendingOnLine(to, lineId))
                        AddEdge(dirKey, from, to);
                }

                // 环线特殊处理：添加首尾闭合边
                if (dir.Attrs.Contains("LOOP") && stationsList.Count > 1)
                {
                    string first = stationsList.First();
                    string last = stationsList.Last();
                    if (!IsStationPendingOnLine(first, lineId) && !IsStationPendingOnLine(last, lineId))
                        AddEdge(dirKey, last, first);
                }
            }

            // ===== 步骤2：构建换乘边（同一车站不同方向）=====
            DebugLog("开始构建换乘边...");
            foreach (var stationKv in _data.Stations)
            {
                var station = stationKv.Key;

                // 获取该车站所有非未来、非快车的方向
                var directionsAtStation = _data.Directions.Values
                    .Where(d => d.Stations.Contains(station) && !d.IsFuture && !d.Attrs.Contains("FAST"))
                    .Select(d => GraphKeyUtils.MakeDirKey(d.LineId, d.Stations.First(), d.Stations.Last()))
                    .Distinct()
                    .ToList();

                // 过滤掉车站在对应线路上为暂缓/关闭的方向
                var validDirections = directionsAtStation
                    .Where(dk => !IsStationPendingOnLine(station, GraphKeyUtils.GetLineIdFromDirKey(dk)))
                    .ToList();

                // 存储车站到有效方向集合的映射
                _stationLines[station] = new HashSet<string>(validDirections);
                DebugLog($"  站点 {station}, 方向数: {validDirections.Count}");

                // 在所有有效方向间建立换乘边（全连接）
                for (int i = 0; i < validDirections.Count; i++)
                {
                    for (int j = i + 1; j < validDirections.Count; j++)
                    {
                        var dirKey1 = validDirections[i];
                        var dirKey2 = validDirections[j];

                        // 检查换乘状态（是否关闭）
                        var status = StationUtils.GetTransferStatus(station, dirKey1, dirKey2, _data);
                        if (StationUtils.IsClosed(status)) continue;

                        // 创建双向换乘边
                        string node1 = GraphKeyUtils.MakeNode(dirKey1, station);
                        string node2 = GraphKeyUtils.MakeNode(dirKey2, station);
                        AddGraphEdge(node1, node2, 1); // 换乘边权重固定为1（实际代价在搜索时计算）
                    }
                }
            }

            DebugLog($"图构建完成，节点数: {_graph.Count}, 边总数: {_graph.Sum(kv => kv.Value.Count) / 2}");

            // ===== 步骤3：构建辅助映射（用于结果分段展示）=====
            BuildBranchHelpers();
            BuildDisplayNames();
        }

        /// <summary>
        /// 检查车站在特定线路上是否处于暂缓/关闭状态
        /// </summary>
        /// <param name="station">车站名称</param>
        /// <param name="lineId">线路ID</param>
        /// <returns>是否暂缓/关闭</returns>
        private bool IsStationPendingOnLine(string station, string lineId)
        {
            return _data.StationStatus.TryGetValue((station, lineId), out var status) &&
                   (status == "pending" || status == "closed" || status == "disable");
        }

        /// <summary>
        /// 查询两个车站间的运行距离
        /// </summary>
        /// <param name="dirKey">方向键</param>
        /// <param name="from">起点站</param>
        /// <param name="to">终点站</param>
        /// <returns>距离（千米），未找到返回-1</returns>
        private double GetDistance(string dirKey, string from, string to)
        {
            if (_adjDist.TryGetValue(dirKey, out var adj))
            {
                // 尝试正向查询
                if (adj.TryGetValue(from + "|" + to, out double distance))
                {
                    DebugLog($"  查询距离: {dirKey}, {from}->{to}, 结果: {distance:F4}");
                    return distance;
                }
                // 尝试反向查询
                if (adj.TryGetValue(to + "|" + from, out distance))
                {
                    DebugLog($"  查询距离(反向): {dirKey}, {to}->{from}, 结果: {distance:F4}");
                    return distance;
                }
            }
            DebugLog($"  查询距离: {dirKey}, {from}->{to}, 未找到");
            return -1;
        }

        /// <summary>
        /// 添加运行边（同方向相邻车站）
        /// </summary>
        /// <param name="dirKey">方向键</param>
        /// <param name="from">起点站</param>
        /// <param name="to">终点站</param>
        private void AddEdge(string dirKey, string from, string to)
        {
            string node1 = GraphKeyUtils.MakeNode(dirKey, from);
            string node2 = GraphKeyUtils.MakeNode(dirKey, to);
            AddGraphEdge(node1, node2, 0); // 运行边权重为0（实际代价在搜索时计算）
        }

        /// <summary>
        /// 添加图边（底层方法，创建双向边）
        /// </summary>
        /// <param name="node1">起始节点</param>
        /// <param name="node2">目标节点</param>
        /// <param name="weight">边权重（预留参数，当前未使用）</param>
        private void AddGraphEdge(string node1, string node2, int weight)
        {
            // 确保节点存在于邻接表中
            if (!_graph.TryGetValue(node1, out var list1)) _graph[node1] = list1 = new List<Edge>();
            if (!_graph.TryGetValue(node2, out var list2)) _graph[node2] = list2 = new List<Edge>();

            // 添加双向边
            list1.Add(new Edge(node2, weight));
            list2.Add(new Edge(node1, weight));
        }
        #endregion

        #region 主线/支线辅助映射构建
        /// <summary>
        /// 构建主线/支线辅助映射，用于结果分段展示
        /// </summary>
        /// <remarks>
        /// 核心逻辑：
        /// 1. 区分主线方向和支线方向
        /// 2. 主线车站集合 = 所有主线方向车站的并集
        /// 3. 支线独有车站集合 = 支线车站 - 主线车站
        /// 4. 支线终点站集合 = 各支线方向的终点站
        /// </remarks>
        private void BuildBranchHelpers()
        {
            DebugLog("构建主线/支线辅助映射...");

            // 按线路分组，区分主线和支线方向
            var lineMainDirections = new Dictionary<string, List<string>>();
            var lineBranchDirections = new Dictionary<string, List<string>>();

            foreach (var kv in _dirIsBranchMap)
            {
                var dirKey = kv.Key;
                var isBranch = kv.Value;
                var lineId = GraphKeyUtils.GetLineIdFromDirKey(dirKey);

                if (isBranch)
                {
                    if (!lineBranchDirections.TryGetValue(lineId, out var list))
                        lineBranchDirections[lineId] = list = new List<string>();
                    list.Add(dirKey);
                }
                else
                {
                    if (!lineMainDirections.TryGetValue(lineId, out var list))
                        lineMainDirections[lineId] = list = new List<string>();
                    list.Add(dirKey);
                }
            }

            // 构建主线车站集合（所有主线方向车站的并集）
            foreach (var kv in lineMainDirections)
            {
                var set = new HashSet<string>();
                foreach (var dirKey in kv.Value)
                    if (_dirStations.TryGetValue(dirKey, out var stations))
                        set.UnionWith(stations);
                _lineMainStations[kv.Key] = set;
            }

            // 构建支线车站集合
            var lineBranchStations = new Dictionary<string, HashSet<string>>();
            foreach (var kv in lineBranchDirections)
            {
                var set = new HashSet<string>();
                foreach (var dirKey in kv.Value)
                    if (_dirStations.TryGetValue(dirKey, out var stations))
                        set.UnionWith(stations);
                lineBranchStations[kv.Key] = set;
            }

            // 计算支线独有车站（支线车站 - 主线车站）
            foreach (var lineId in _lineMainStations.Keys)
            {
                var mainStations = _lineMainStations[lineId];
                var branchStations = lineBranchStations.TryGetValue(lineId, out var bs) ? bs : new HashSet<string>();
                var branchOnly = new HashSet<string>(branchStations);
                branchOnly.ExceptWith(mainStations); // 差集运算
                _lineBranchOnlyStations[lineId] = branchOnly;
            }

            // 提取支线终点站（各支线方向的终点站）
            foreach (var kv in lineBranchDirections)
            {
                var ends = new List<string>();
                foreach (var dirKey in kv.Value)
                {
                    var parts = GraphKeyUtils.ParseDirKey(dirKey);
                    ends.Add(parts[2]); // 方向键第三部分为终点站
                }
                _lineBranchEnds[kv.Key] = ends.Distinct().ToList();
            }

            DebugLog($"主线/支线辅助映射构建完成");
        }

        /// <summary>
        /// 构建方向显示名称映射
        /// </summary>
        /// <remarks>
        /// 显示名称生成规则：
        /// 1. 优先使用方向昵称（Alias）
        /// 2. 无昵称时：线路名称 + （支线标识）
        /// 3. 跨城市线路添加城市前缀
        /// </remarks>
        private void BuildDisplayNames()
        {
            // 构建方向键到昵称的映射（处理重复键，取第一个）
            var dirNickNameMap = _data.Directions
                .GroupBy(kv => GraphKeyUtils.MakeDirKey(
                    kv.Value.LineId,
                    kv.Value.Stations.FirstOrDefault(),
                    kv.Value.Stations.LastOrDefault()))
                .ToDictionary(g => g.Key, g => g.First().Value.Alias ?? "");

            foreach (var dirKey in _dirStations.Keys)
            {
                // 尝试获取方向昵称
                string nick = dirNickNameMap.TryGetValue(dirKey, out var n) && !string.IsNullOrEmpty(n) ? n : null;

                if (!string.IsNullOrEmpty(nick))
                {
                    // 使用昵称作为显示名称
                    _dirDisplayNameMap[dirKey] = nick;
                }
                else
                {
                    // 生成默认显示名称
                    var lineId = GraphKeyUtils.GetLineIdFromDirKey(dirKey);
                    var city = _data.Lines.TryGetValue(lineId, out var li) ? li.City : _data.MainCity;
                    var baseName = _data.Lines.TryGetValue(lineId, out li) ? li.Name : lineId;
                    var isBranch = _dirIsBranchMap.TryGetValue(dirKey, out var br) && br;

                    // 跨城市线路添加城市前缀，支线添加标识
                    _dirDisplayNameMap[dirKey] = (city == _data.MainCity ? baseName : city + baseName) +
                                                 (isBranch ? "(支线)" : "");
                }
            }
        }
        #endregion

        #region 核心路径搜索算法
        /// <summary>
        /// 查找最优路径（主入口方法）
        /// </summary>
        /// <param name="start">起点站</param>
        /// <param name="end">终点站</param>
        /// <param name="weight">搜索权重配置</param>
        /// <param name="excludeSpecial">是否排除特殊线路（如机场线）</param>
        /// <returns>路径结果</returns>
        /// <remarks>
        /// 搜索策略：
        /// 1. 执行两次搜索：排除特殊线路 vs 允许特殊线路
        /// 2. 同时计算最短路径（纯距离）作为基准
        /// 3. 分别计算两种路径的票价
        /// 4. 综合代价比较：票价×票价权重 + 搜索代价
        /// 5. 选择综合代价更小的路径
        /// 
        /// 设计考虑：特殊线路（如机场线）票价高但速度快，需要权衡
        /// </remarks>
        public PathResult FindPath(string start, string end, SearchWeight weight, bool excludeSpecial = true)
        {
            DebugLog($"========== 查找路径: {start} -> {end} ==========");
            DebugLog($"权重: L={weight.LengthWeight}, T={weight.TransferWeight}, M={weight.MoneyWeight}, Time={weight.TimeWeight}, S={weight.StationWeight}");
            DebugLog($"excludeSpecial = {excludeSpecial}");

            // 创建纯距离搜索权重（用于基准比较）
            var shortestWeight = SearchWeight.Shortest;

            // 执行三次搜索
            var resultExcludingSpecial = FindPathInternal(start, end, weight, excludeSpecial);
            DebugLog($"路径1 (排除特殊) Transfers={resultExcludingSpecial.Transfers}, Cost={resultExcludingSpecial.Cost:F4}, Path.Count={resultExcludingSpecial.Path?.Count ?? 0}");

            var resultIncludingSpecial = FindPathInternal(start, end, weight, false);
            DebugLog($"路径2 (允许特殊) Transfers={resultIncludingSpecial.Transfers}, Cost={resultIncludingSpecial.Cost:F4}, Path.Count={resultIncludingSpecial.Path?.Count ?? 0}");

            var shortestPath = FindPathInternal(start, end, shortestWeight, excludeSpecial);
            DebugLog($"最短路径 Transfers={shortestPath.Transfers}, Cost={shortestPath.Cost:F4}, Path.Count={shortestPath.Path?.Count ?? 0}");

            // 计算两种路径的实际票价
            var fareExcludingSpecial = FareCalculator.CalcFareWithoutSpecial(shortestPath.Path, this);
            var fareIncludingSpecial = FareCalculator.CalcFareWithSpecial(resultIncludingSpecial.Path, this);
            DebugLog($"票价1 (普通): {fareExcludingSpecial.TotalMoney:F2}, 特殊: {fareExcludingSpecial.SpecialMoney:F2}");
            DebugLog($"票价2 (含特殊): {fareIncludingSpecial.TotalMoney:F2}, 特殊: {fareIncludingSpecial.SpecialMoney:F2}");

            // 计算综合代价（票价×票价权重 + 搜索代价）
            double costExcludingSpecial = fareExcludingSpecial.TotalMoney * weight.MoneyWeight + resultExcludingSpecial.Cost;
            double costIncludingSpecial = fareIncludingSpecial.TotalMoney * weight.MoneyWeight + resultIncludingSpecial.Cost;

            // 填充票价信息
            resultExcludingSpecial.TotalMoney = fareExcludingSpecial.TotalMoney;
            resultExcludingSpecial.SpecialMoney = fareExcludingSpecial.SpecialMoney;
            resultExcludingSpecial.Cost = costExcludingSpecial;

            resultIncludingSpecial.TotalMoney = fareIncludingSpecial.TotalMoney;
            resultIncludingSpecial.SpecialMoney = fareIncludingSpecial.SpecialMoney;
            resultIncludingSpecial.Cost = costIncludingSpecial;

            // 选择最优路径
            PathResult finalResult;
            if (resultExcludingSpecial.Transfers == -1)
            {
                // 排除特殊线路时无可达路径，使用包含特殊线路的路径
                finalResult = resultIncludingSpecial;
                DebugLog($"路径1不可达，使用路径2");
            }
            else
            {
                // 选择综合代价更小的路径
                finalResult = costExcludingSpecial < costIncludingSpecial ? resultExcludingSpecial : resultIncludingSpecial;
                DebugLog($"选择路径{(costExcludingSpecial < costIncludingSpecial ? "1" : "2")}, 综合代价: {(costExcludingSpecial < costIncludingSpecial ? costExcludingSpecial : costIncludingSpecial):F4}");
            }

            // 输出最终结果摘要
            DebugLog($"最终结果: Transfers={finalResult.Transfers}, Cost={finalResult.Cost:F4}, TotalMoney={finalResult.TotalMoney:F2}");
            if (finalResult.Path != null && finalResult.Path.Any())
                DebugLog($"路径节点示例: {string.Join(" -> ", finalResult.Path.Take(5))}{(finalResult.Path.Count > 5 ? "..." : "")}");

            return finalResult;
        }

        /// <summary>
        /// 内部路径搜索实现（改进的Dijkstra算法）
        /// </summary>
        /// <param name="start">起点站</param>
        /// <param name="end">终点站</param>
        /// <param name="weight">搜索权重配置</param>
        /// <param name="excludeSpecial">是否排除特殊线路</param>
        /// <returns>路径结果</returns>
        private PathResult FindPathInternal(string start, string end, SearchWeight weight, bool excludeSpecial)
        {
            DebugLog($"  --- 内部搜索: {start} -> {end}, excludeSpecial={excludeSpecial} ---");

            // 起点终点相同时的特殊处理
            if (start == end)
            {
                var directions = _stationLines.TryGetValue(start, out var dirs) ? dirs : new HashSet<string>();
                var filteredDirections = excludeSpecial ? FilterSpecialDirs(directions) : directions;

                if (filteredDirections.Any())
                {
                    var firstDirection = filteredDirections.First();
                    DebugLog($"  起点=终点，返回起始方向 {firstDirection}");
                    return new PathResult(0, 0, new List<string> { GraphKeyUtils.MakeNode(firstDirection, start) });
                }
                DebugLog($"  起点=终点，但无可用方向");
                return new PathResult(0, 0, new List<string>());
            }

            // 获取起点和终点的有效方向集合
            var startDirections = _stationLines.TryGetValue(start, out var sd) ? sd : new HashSet<string>();
            var endDirections = _stationLines.TryGetValue(end, out var ed) ? ed : new HashSet<string>();

            DebugLog($"  起点方向数: {startDirections.Count}, 终点方向数: {endDirections.Count}");
            if (startDirections.Any())
                DebugLog($"  起点方向示例: {string.Join(", ", startDirections.Take(3))}");
            if (endDirections.Any())
                DebugLog($"  终点方向示例: {string.Join(", ", endDirections.Take(3))}");

            // 检查起点或终点是否有可用方向
            if (!startDirections.Any() || !endDirections.Any())
            {
                DebugLog($"  起点或终点无方向，返回 -1");
                return new PathResult(-1, 0, new List<string>());
            }

            // 排除特殊线路方向
            if (excludeSpecial)
            {
                startDirections = FilterSpecialDirs(startDirections);
                endDirections = FilterSpecialDirs(endDirections);
                DebugLog($"  过滤特殊后，起点方向数: {startDirections.Count}, 终点方向数: {endDirections.Count}");

                if (!startDirections.Any() || !endDirections.Any())
                {
                    DebugLog($"  过滤后无方向，返回 -1");
                    return new PathResult(-1, 0, new List<string>());
                }
            }

            // 初始化Dijkstra算法数据结构
            var distance = new Dictionary<string, double>(); // 节点到起点的代价
            var previous = new Dictionary<string, string>(); // 前驱节点（用于路径重建）

            // 使用SortedSet作为优先队列（按代价排序）
            var priorityQueue = new SortedSet<NodeState>(
                Comparer<NodeState>.Create((a, b) =>
                    a.Cost.CompareTo(b.Cost) != 0 ? a.Cost.CompareTo(b.Cost) :
                    string.Compare(a.Node, b.Node, StringComparison.Ordinal)));

            // 初始化起点节点
            int initCount = 0;
            foreach (var direction in startDirections)
            {
                // 跳过暂缓/关闭车站所在方向
                if (IsStationPendingOnLine(start, GraphKeyUtils.GetLineIdFromDirKey(direction))) continue;

                var node = GraphKeyUtils.MakeNode(direction, start);
                distance[node] = 0;
                priorityQueue.Add(new NodeState(node, 0));
                initCount++;
            }
            DebugLog($"  初始化堆，节点数: {initCount}");

            // Dijkstra主循环
            string foundNode = null;
            int expandCount = 0;
            while (priorityQueue.Any())
            {
                // 取出代价最小的节点
                var current = priorityQueue.First();
                priorityQueue.Remove(current);
                expandCount++;

                // 定期输出进度日志
                if (expandCount % 500 == 0)
                    DebugLog($"  扩展节点数: {expandCount}, 当前堆大小: {priorityQueue.Count}");

                // 跳过已过时的节点（已有更小代价）
                if (distance.TryGetValue(current.Node, out var dist) && dist < current.Cost - 1e-9)
                    continue;

                // 检查是否到达终点站
                var parts = GraphKeyUtils.ParseNode(current.Node);
                var station = parts[1];
                if (station == end)
                {
                    var dirKey = parts[0];
                    // 排除特殊线路检查
                    if (excludeSpecial && IsSpecialDirKey(dirKey)) continue;
                    // 检查终点站是否暂缓/关闭
                    if (IsStationPendingOnLine(end, GraphKeyUtils.GetLineIdFromDirKey(dirKey))) continue;

                    foundNode = current.Node;
                    DebugLog($"  找到终点 {end}，节点 {foundNode}，代价 {distance[foundNode]:F4}，扩展节点数: {expandCount}");
                    break;
                }

                // 遍历当前节点的所有邻居
                if (!_graph.TryGetValue(current.Node, out var edges)) continue;
                foreach (var edge in edges)
                {
                    // 计算边的代价
                    double edgeCost = ComputeEdgeCost(current.Node, edge.Node, weight, excludeSpecial);
                    if (edgeCost < 0) continue; // 无效边

                    double newCost = current.Cost + edgeCost;

                    // 松弛操作：如果找到更小代价，更新并记录
                    if (!distance.TryGetValue(edge.Node, out var oldCost) || newCost < oldCost - 1e-9)
                    {
                        distance[edge.Node] = newCost;
                        previous[edge.Node] = current.Node;
                        priorityQueue.Add(new NodeState(edge.Node, newCost));
                    }
                }
            }

            // 未找到路径
            if (foundNode == null)
            {
                DebugLog($"  未找到路径，扩展节点数: {expandCount}");
                return new PathResult(-1, 0, new List<string>());
            }

            // 重建路径
            var path = new List<string>();
            var currentNode = foundNode;
            while (previous.ContainsKey(currentNode))
            {
                path.Add(currentNode);
                currentNode = previous[currentNode];
            }
            path.Add(currentNode);
            path.Reverse(); // 反转得到从起点到终点的路径

            int transfers = CountTransfers(path);
            DebugLog($"  路径构建完成，长度: {path.Count}, 换乘次数: {transfers}");
            return new PathResult(transfers, distance[foundNode], path);
        }

        /// <summary>
        /// 计算边的代价（核心代价函数）
        /// </summary>
        /// <param name="fromNode">起始节点</param>
        /// <param name="toNode">目标节点</param>
        /// <param name="weight">搜索权重配置</param>
        /// <param name="excludeSpecial">是否排除特殊线路</param>
        /// <returns>边的综合代价，-1表示无效边</returns>
        /// <remarks>
        /// 代价计算分两种情况：
        /// 1. 换乘边（不同方向）：
        ///    代价 = 换乘权重 + 时间权重×换乘时间 + 车站权重×0.15
        /// 2. 运行边（同方向）：
        ///    代价 = 距离权重×距离 + 时间权重×运行时间 + 车站权重×1
        /// 
        /// 设计考虑：
        /// - 换乘时间包含候车、步行等，由StationUtils计算
        /// - 运行时间基于物理模型，由TimeCalculator计算
        /// - 车站权重系数不同：换乘时较小（0.15），运行时较大（1）
        /// </remarks>
        private double ComputeEdgeCost(string fromNode, string toNode, SearchWeight weight, bool excludeSpecial)
        {
            var fromParsed = GraphKeyUtils.ParseNode(fromNode);
            var toParsed = GraphKeyUtils.ParseNode(toNode);
            var fromDir = fromParsed[0];
            var toDir = toParsed[0];
            var fromStation = fromParsed[1];
            var toStation = toParsed[1];

            // ===== 换乘边 =====
            if (fromDir != toDir)
            {
                // 检查换乘状态
                var status = StationUtils.GetTransferStatus(fromStation, fromDir, toDir, _data);
                if (StationUtils.IsClosed(status)) return -1; // 换乘关闭

                // 计算换乘时间（包含候车、步行等）
                double transferTime = StationUtils.GetInnerTransferTime(fromStation, fromDir, toDir, _data);

                // 计算综合代价
                double currentCost = weight.TransferWeight +
                             weight.TimeWeight * transferTime +
                             0.15 * weight.StationWeight;

                DebugLog($"    换乘边: {fromStation} {fromDir}->{toDir}, status={status}, transferTime={transferTime:F2}s, cost={currentCost:F4}");
                return currentCost;
            }

            // ===== 运行边 =====
            // 排除特殊线路检查
            if (excludeSpecial && IsSpecialDirKey(fromDir)) return -1;

            // 获取区间距离
            double distance = GetDistance(fromDir, fromStation, toStation);
            if (distance < 0) return -1; // 距离无效

            // 获取线路信息
            var lineId = GraphKeyUtils.GetLineIdFromDirKey(fromDir);
            int maxSpeed = _data.Lines.TryGetValue(lineId, out var li) ? (int)(li.Maxspeed ?? 80) : 80;

            // 计算运行时间（基于物理模型）
            double segmentTime = TimeCalculator.ComputeSegmentTime(
                distance, maxSpeed, lineId, fromDir, fromStation, toStation, _data);

            // 计算综合代价
            double cost = weight.LengthWeight * distance +
                         weight.TimeWeight * segmentTime +
                         weight.StationWeight * 1;

            DebugLog($"    同向边: {fromStation}->{toStation}, dist={distance:F4}, maxSpeed={maxSpeed}, segTime={segmentTime:F4}, cost={cost:F4}");
            return cost;
        }

        /// <summary>
        /// 过滤特殊线路方向
        /// </summary>
        /// <param name="directions">方向键集合</param>
        /// <returns>过滤后的方向键集合</returns>
        private HashSet<string> FilterSpecialDirs(HashSet<string> directions)
        {
            var result = new HashSet<string>();
            foreach (var direction in directions)
                if (!IsSpecialDirKey(direction))
                    result.Add(direction);
            return result;
        }

        /// <summary>
        /// 判断方向键是否属于特殊线路
        /// </summary>
        /// <param name="dirKey">方向键</param>
        /// <returns>是否特殊线路</returns>
        private bool IsSpecialDirKey(string dirKey)
        {
            var lineId = GraphKeyUtils.GetLineIdFromDirKey(dirKey);
            return _data.SpecialFareRules?.Any(rule => rule.LineId == lineId) ?? false;
        }

        /// <summary>
        /// 统计路径中的换乘次数
        /// </summary>
        /// <param name="path">路径节点序列</param>
        /// <returns>换乘次数</returns>
        private int CountTransfers(List<string> path)
        {
            int count = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                var dir1 = GraphKeyUtils.ParseNode(path[i])[0];
                var dir2 = GraphKeyUtils.ParseNode(path[i + 1])[0];
                if (dir1 != dir2) count++;
            }
            return count;
        }
        #endregion

        #region 路径分段（用于UI展示）
        /// <summary>
        /// 将路径节点序列转换为行程分段
        /// </summary>
        /// <param name="path">路径节点序列</param>
        /// <returns>行程分段列表</returns>
        /// <remarks>
        /// 分段逻辑：
        /// 1. 将路径按方向键分组，每组为一个分段
        /// 2. 过滤未来方向（不参与常规展示）
        /// 3. 为每个分段构建RouteSegment对象
        /// 4. 识别换乘类型（站内/出站）
        /// 5. 生成方向显示名称（环线特殊处理）
        /// </remarks>
        public List<RouteSegment> GetRouteSegments(List<string> path)
        {
            var segments = new List<RouteSegment>();
            if (path == null || path.Count < 2) return segments;

            // ===== 步骤1：将路径拆分为连续同方向的原始分段 =====
            var rawSegments = new List<Segment>();
            var first = GraphKeyUtils.ParseNode(path[0]);
            string currentDir = first[0];
            var currentStations = new List<string> { first[1] };

            for (int i = 1; i < path.Count; i++)
            {
                var parsed = GraphKeyUtils.ParseNode(path[i]);
                string dir = parsed[0];
                string station = parsed[1];

                if (dir == currentDir)
                    currentStations.Add(station); // 同方向，添加车站
                else
                {
                    // 方向改变，完成当前分段
                    rawSegments.Add(new Segment(currentDir, currentStations));
                    currentDir = dir;
                    currentStations = new List<string> { station };
                }
            }
            rawSegments.Add(new Segment(currentDir, currentStations));

            // ===== 步骤2：遍历原始分段，过滤未来方向并构建展示分段 =====
            Segment lastValidSegment = null; // 上一个有效分段（非未来）
            for (int idx = 0; idx < rawSegments.Count; idx++)
            {
                var segment = rawSegments[idx];
                var dirKey = segment.DirKey;
                var stations = segment.Stations;
                if (!stations.Any()) continue;

                // 检查是否为未来方向
                bool isFuture = false;
                foreach (var kv in _data.Directions)
                {
                    var detectDirInfo = kv.Value;
                    if (detectDirInfo.LineId != GraphKeyUtils.GetLineIdFromDirKey(dirKey)) continue;

                    // 匹配方向定义
                    if (detectDirInfo.Stations.FirstOrDefault() == stations.First() &&
                        detectDirInfo.Stations.LastOrDefault() == stations.Last())
                    {
                        if (detectDirInfo.IsFuture)
                        {
                            isFuture = true;
                            break;
                        }
                    }
                }
                if (isFuture) continue; // 跳过未来方向

                // ===== 构建有效分段 =====
                string startStation = stations.First();
                string endStation = stations.Last();

                // 判断换乘类型（基于上一个有效分段）
                bool isOutStationTransfer = false;
                if (lastValidSegment != null)
                {
                    var prevDirKey = lastValidSegment.DirKey;
                    var status = StationUtils.GetTransferStatus(startStation, prevDirKey, dirKey, _data);
                    if (status == "out_station") isOutStationTransfer = true;
                }

                // 获取线路信息
                var lineId = GraphKeyUtils.GetLineIdFromDirKey(dirKey);
                string lineName;
                if (_externalKeyToDirInfo.TryGetValue(dirKey, out var dirInfoA) && !string.IsNullOrEmpty(dirInfoA.Alias))
                {
                    lineName = dirInfoA.Alias; // 用方向别名作为线路名
                }
                else
                {
                    lineName = _data.Lines.TryGetValue(lineId, out var li) ? li.Name : lineId;
                }

                // 生成方向显示名称（环线特殊处理）
                var directionDisplay = BuildDirectionDisplay(lineId, dirKey, stations);
                bool isBranch = _dirIsBranchMap.TryGetValue(dirKey, out var br) && br;
                string nickColor = _externalKeyToDirInfo.TryGetValue(dirKey, out var dirInfo) ? dirInfo.AliasColor : null;

                // 创建行程分段对象
                segments.Add(new RouteSegment(
                    startStation, lineId, lineName,
                    directionDisplay, endStation, stations.Count - 1,
                    isBranch, nickColor, isOutStationTransfer, dirKey
                )
                {
                    Stations = new List<string>(stations) // 复制车站序列
                });

                // 更新上一个有效分段
                lastValidSegment = segment;
            }

            return segments;
        }

        /// <summary>
        /// 构建方向显示名称
        /// </summary>
        /// <param name="lineId">线路ID</param>
        /// <param name="dirKey">方向键</param>
        /// <param name="stations">车站序列</param>
        /// <returns>方向显示名称</returns>
        /// <remarks>
        /// 环线特殊处理：
        /// - 内环：显示"X站（内环）"
        /// - 外环：显示"X站（外环）"
        /// 非环线：直接显示终点站名称
        /// </remarks>
        private string BuildDirectionDisplay(string lineId, string dirKey, List<string> stations)
        {
            // 环线处理
            if (_hasLoopMap.TryGetValue(lineId, out var isLoop) && isLoop)
            {
                var dirType = _data.Directions.TryGetValue(dirKey, out var dirInfo) ? dirInfo.DirectionType : "";
                if (dirType == "inner") return stations.Count > 1 ? stations[1] + "（内环）" : "";
                if (dirType == "outer") return stations.Count > 1 ? stations[1] + "（外环）" : "";
                return GetTerminalStation(lineId, dirKey);
            }

            // 非环线：直接使用当前分段的实际终点
            return GetTerminalStation(lineId, dirKey);
        }

        /// <summary>
        /// 获取方向的终点站（列车实际开往方向）
        /// </summary>
        /// <param name="lineId">线路ID</param>
        /// <param name="dirKey">方向键</param>
        /// <returns>终点站名称</returns>
        /// <remarks>
        /// 支持两种方向键格式：
        /// 1. 外部格式（推荐）：如"L1|北京西站|广州南站"，直接解析终点
        /// 2. 内部格式（兼容）：如"L1_0"，从Directions字典查找
        /// 
        /// 特殊处理：返回最后一个非暂缓/非关闭的车站
        /// </remarks>
        public string GetTerminalStation(string lineId, string dirKey)
        {
            ParsedData data = _data;
            if (data == null || string.IsNullOrEmpty(dirKey))
                return null;

            // 尝试从Directions字典获取（支持内部ID）
            if (data.Directions.TryGetValue(dirKey, out var dirInfo))
            {
                return GetLastOperatingStation(data, lineId, dirInfo.Stations);
            }

            // 尝试解析外部键格式（如"L1|北京西站|广州南站"）
            if (dirKey.Contains('|'))
            {
                var parts = dirKey.Split('|');
                if (parts.Length >= 3)
                {
                    // 从Directions中查找匹配起终点的方向
                    foreach (var kv in data.Directions)
                    {
                        var d = kv.Value;
                        if (d.LineId == lineId && d.Stations.Count > 0 &&
                            d.Stations.First() == parts[1] &&
                            d.Stations.Last() == parts[2])
                        {
                            return GetLastOperatingStation(data, lineId, d.Stations);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取最后一个运营的车站（从末尾向前查找第一个非暂缓车站）
        /// </summary>
        /// <param name="data">路网数据</param>
        /// <param name="lineId">线路ID</param>
        /// <param name="stations">车站序列</param>
        /// <returns>最后一个运营车站</returns>
        private static string GetLastOperatingStation(ParsedData data, string lineId, List<string> stations)
        {
            if (stations == null || stations.Count == 0)
                return null;

            // 从后向前遍历，找到第一个非暂缓车站
            for (int i = stations.Count - 1; i >= 0; i--)
            {
                string station = stations[i];
                if (!IsStationPending(data, station, lineId))
                {
                    return station;
                }
            }

            // 全部暂缓则返回最后一个（兜底逻辑）
            return stations.Last();
        }

        /// <summary>
        /// 检查车站在某条线路上是否为暂缓/关闭状态
        /// </summary>
        /// <param name="data">路网数据</param>
        /// <param name="station">车站名称</param>
        /// <param name="lineId">线路ID</param>
        /// <returns>是否暂缓/关闭</returns>
        private static bool IsStationPending(ParsedData data, string station, string lineId)
        {
            if (data.StationStatus.TryGetValue((station, lineId), out string status))
            {
                return status == "pending" || status == "closed" || status == "disable";
            }
            return false;
        }
        #endregion

        #region 辅助查询方法
        /// <summary>
        /// 计算路径总长度
        /// </summary>
        /// <param name="path">路径节点序列</param>
        /// <returns>总长度（千米）</returns>
        public double ComputePathDistance(List<string> path)
        {
            double total = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                var p1 = GraphKeyUtils.ParseNode(path[i]);
                var p2 = GraphKeyUtils.ParseNode(path[i + 1]);
                if (p1[0] != p2[0]) continue; // 跳过换乘边
                double d = GetDistance(p1[0], p1[1], p2[1]);
                if (d > 0) total += d;
            }
            return total;
        }

        /// <summary>
        /// 获取方向内两站间的距离
        /// </summary>
        /// <param name="dirKey">方向键</param>
        /// <param name="from">起点站</param>
        /// <param name="to">终点站</param>
        /// <returns>距离（千米），未找到返回-1</returns>
        public double GetDistanceInDirection(string dirKey, string from, string to)
            => GetDistance(dirKey, from, to);
        #endregion

        #region 内部数据结构
        /// <summary>图边结构</summary>
        private class Edge
        {
            public string Node { get; }
            public int Weight { get; }
            public Edge(string node, int weight) { Node = node; Weight = weight; }
        }

        /// <summary>优先队列节点状态</summary>
        private class NodeState
        {
            public string Node { get; }
            public double Cost { get; }
            public NodeState(string node, double cost) { Node = node; Cost = cost; }
        }

        /// <summary>临时分段结构（用于内部处理）</summary>
        private class Segment
        {
            public string DirKey { get; }
            public List<string> Stations { get; }
            public Segment(string dirKey, List<string> stations) { DirKey = dirKey; Stations = stations; }
        }
        #endregion
    }
}