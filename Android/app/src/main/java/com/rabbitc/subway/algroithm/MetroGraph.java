package com.rabbitc.subway.algroithm;

import android.util.Log;
import android.util.Pair;

import androidx.annotation.NonNull;

import com.rabbitc.subway.algroithm.graph.Edge;
import com.rabbitc.subway.algroithm.graph.NodeState;
import com.rabbitc.subway.algroithm.graph.Segment;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.PathResult;
import com.rabbitc.subway.data.RouteSegment;
import com.rabbitc.subway.data.SearchWeight;
import com.rabbitc.subway.data.json.Direction;
import com.rabbitc.subway.data.json.DirectionStation;
import com.rabbitc.subway.data.json.Line;
import com.rabbitc.subway.data.json.MetroData;
import com.rabbitc.subway.data.json.Station;
import com.rabbitc.subway.data.json.StationLine;
import com.rabbitc.subway.utils.GraphKeyUtils;
import com.rabbitc.subway.utils.PrefixFilter;
import com.rabbitc.subway.utils.StationUtils;

import java.util.*;
import java.util.concurrent.Callable;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.ThreadLocalRandom;

/**
 * 地铁线路图的核心数据结构与路径规划算法。
 * <p>
 * 支持距离、换乘、时间、站数等权重，自动处理方向显示。
 * </p>
 */
public class MetroGraph {

    // ==================== 图结构 ====================
    private final Map<String, List<Edge>> transferGraph = new HashMap<>();
    private final Map<String, Set<String>> stationLines = new HashMap<>();
    private final Map<String, Map<String, Map<String, Double>>> lineAdjDist = new HashMap<>();

    // ==================== 辅助映射 ====================
    private final Map<String, List<String>> dirStations = new HashMap<>();
    private final Map<String, String> lineNameMap = new HashMap<>();
    private final Map<String, String> lineCityMap = new HashMap<>();
    private final Map<String, Boolean> hasLoopMap = new HashMap<>();
    private final Map<String, String> dirDisplayNameMap = new HashMap<>();
    private String keyCity;

    // ==================== 构建图（公共入口） ====================
    /**
     * 从 Gson POJO 数据构建地铁图。
     *
     * @param metroData 解析后的数据对象（包含 lines 和 stations）
     * @param keyCity   当前城市的名称，用于判断是否显示城市前缀
     * @return 所有站点名称的集合
     */
    public Set<String> buildGraphs(MetroData metroData, String keyCity) {
        this.keyCity = keyCity;

        if (metroData.lines == null) {
            throw new IllegalArgumentException("数据中缺少 LINE 字段");
        }

        // 第一阶段：解析线路方向
        parseLines(metroData.lines);

        // 第二阶段：解析站点，建立换乘边
        if (metroData.stations == null) {
            throw new IllegalArgumentException("数据中缺少 STATION 字段");
        }
        parseStations(metroData.stations);

        // 后处理：构建方向显示名称
        buildDirDisplayNames();

        return stationLines.keySet();
    }

    // ==================== 第一阶段：解析线路方向 ====================
    private void parseLines(Map<String, Line> linesMap) {
        for (Map.Entry<String, Line> entry : linesMap.entrySet()) {
            String lineId = entry.getKey();
            Line line = entry.getValue();

            // 线路基本信息
            String lineName = (line.name != null) ? line.name : lineId;
            lineNameMap.put(lineId, lineName);

            String city = (line.city != null) ? line.city : "广州";
            lineCityMap.put(lineId, city);

            List<Direction> directions = line.direction;
            if (directions == null || directions.isEmpty()) continue;

            // 遍历每个方向
            for (Direction dir : directions) {
                // 跳过快车方向、区间车及未来线路
                if (dir.isFast) continue;
                if (dir.isFuture) continue;

                String start = dir.start;
                String end = dir.end;
                boolean isLoop = dir.isLoop;
                String dirKey = GraphKeyUtils.makeDirKey(lineId, start, end);
                hasLoopMap.put(lineId, isLoop);

                // 解析站点列表
                List<DirectionStation> stationList = dir.station;
                if (stationList == null || stationList.isEmpty()) continue;

                List<String> stationNames = new ArrayList<>();
                Map<String, Map<String, Double>> adj = new HashMap<>();
                lineAdjDist.put(dirKey, adj);
                dirStations.put(dirKey, stationNames);

                // 遍历站点，建立相邻距离
                for (int i = 0; i < stationList.size(); i++) {
                    DirectionStation ds = stationList.get(i);
                    String name = ds.name;
                    if (name == null) continue;
                    stationNames.add(name);

                    if (i < stationList.size() - 1) {
                        String nxt = stationList.get(i + 1).name;
                        // Gson 直接解析 Double，不再需要 getDoubleSafely
                        if (ds.next != null) {
                            double dist = ds.next;
                            adj.computeIfAbsent(name, k -> new HashMap<>()).put(nxt, dist);
                            adj.computeIfAbsent(nxt, k -> new HashMap<>()).put(name, dist);
                        }
                    }
                }

                // 环线闭合边
                if (isLoop && stationList.size() > 1) {
                    String first = stationNames.get(0);
                    String last = stationNames.get(stationNames.size() - 1);
                    DirectionStation lastDs = stationList.get(stationList.size() - 1);

                    // 直接使用 Gson 解析出的 Double，非 Number 不再需要强转
                    if (lastDs.next != null) {
                        double dist = lastDs.next;
                        adj.computeIfAbsent(last, k -> new HashMap<>()).put(first, dist);
                        adj.computeIfAbsent(first, k -> new HashMap<>()).put(last, dist);
                    }
                }

                // 添加同线路相邻站点边（构建 transferGraph）
                for (int i = 0; i < stationNames.size() - 1; i++) {
                    String cur = stationNames.get(i);
                    String nxt = stationNames.get(i + 1);
                    addEdge(dirKey, cur, nxt);
                }
                if (isLoop && stationNames.size() > 1) {
                    String first = stationNames.get(0);
                    String last = stationNames.get(stationNames.size() - 1);
                    addEdge(dirKey, last, first);
                }
            }
        }
    }

    // ==================== 第二阶段：解析站点，建立换乘边 ====================
    private void parseStations(Map<String, Station> stationsMap) {
        for (Map.Entry<String, Station> stEntry : stationsMap.entrySet()) {
            String stationName = stEntry.getKey();
            Station stInfo = stEntry.getValue();

            List<StationLine> lines = stInfo.lines;
            if (lines == null) continue;

            // 收集该站点所有有效方向键（排除已关闭的线路）
            Set<String> dirKeys = new HashSet<>();
            for (StationLine lineEntry : lines) {
                String id = lineEntry.id;
                String start = lineEntry.start;
                String end = lineEntry.end;
                String status = StationUtils.getStationLineStatus(stationName, id);
                if (StationUtils.isClosed(status)) continue;
                dirKeys.add(GraphKeyUtils.makeDirKey(id, start, end));
            }

            // 建立换乘边（不同方向之间）
            List<String> dirKeyList = new ArrayList<>(dirKeys);
            for (int i = 0; i < dirKeyList.size(); i++) {
                for (int j = i + 1; j < dirKeyList.size(); j++) {
                    String dk1 = dirKeyList.get(i);
                    String dk2 = dirKeyList.get(j);
                    String transferStatus = StationUtils.getTransferStatus(stationName, dk1, dk2);
                    if (StationUtils.isClosed(transferStatus)) continue;
                    String n1 = GraphKeyUtils.makeNode(dk1, stationName);
                    String n2 = GraphKeyUtils.makeNode(dk2, stationName);
                    addEdgeToGraph(n1, n2, 1);
                }
            }

            // 记录站点与方向的关系
            for (String dk : dirKeys) {
                stationLines.computeIfAbsent(stationName, k -> new HashSet<>()).add(dk);
            }
        }
    }

    // ==================== 后处理：构建方向显示名称 ====================
    private void buildDirDisplayNames() {
        for (String dirKey : dirStations.keySet()) {
            String lineId = GraphKeyUtils.getLineIdFromDirKey(dirKey);
            String city = lineCityMap.get(lineId);
            String lineName = lineNameMap.get(lineId);
            dirDisplayNameMap.put(dirKey, buildDisplayName(city, lineName));
        }
    }

    private String buildDisplayName(String city, String lineName) {
        if (city.equals(keyCity)) {
            return lineName;
        } else {
            return city + lineName;
        }
    }

    // ==================== 图边添加辅助方法 ====================
    private void addEdge(String dirKey, String from, String to) {
        String n1 = GraphKeyUtils.makeNode(dirKey, from);
        String n2 = GraphKeyUtils.makeNode(dirKey, to);
        addEdgeToGraph(n1, n2, 0); // weight=0 表示同线路内边

        stationLines.computeIfAbsent(from, k -> new HashSet<>()).add(dirKey);
        stationLines.computeIfAbsent(to, k -> new HashSet<>()).add(dirKey);
    }

    private void addEdgeToGraph(String n1, String n2, int weight) {
        transferGraph.computeIfAbsent(n1, k -> new ArrayList<>()).add(new Edge(n2, weight));
        transferGraph.computeIfAbsent(n2, k -> new ArrayList<>()).add(new Edge(n1, weight));
    }

    // ==================== 路径查找（公共入口） ====================
    /**
     * 根据权重查找起点到终点的综合最优路径。
     *
     * @param start 起点站名
     * @param end 终点站名
     * @param weight 搜索权重
     * @param travelTimeMillis 出发时间，单位为毫秒时间戳
     * @return 路径结果；不可达时 transfers 为 -1
     */
    public PathResult findTransferPath(String start, String end, SearchWeight weight, long travelTimeMillis) {
        SearchWeight shortestWeight = SearchWeight.getShortestWeight();

        // 并行执行三种候选：排除特殊线路、允许特殊线路，以及用于计费基准的最短距离路径。
        CompletableFuture<PathResult> future1 = CompletableFuture.supplyAsync(() ->
                findTransferPathInternal(start, end, weight, true, travelTimeMillis));
        CompletableFuture<PathResult> future2 = CompletableFuture.supplyAsync(() ->
                findTransferPathInternal(start, end, weight, false, travelTimeMillis));
        CompletableFuture<PathResult> futureShortest = CompletableFuture.supplyAsync(() ->
                findTransferPathInternal(start, end, shortestWeight, true, travelTimeMillis));

        // join 会等待后台搜索完成；调用方应在工作线程调用此入口，避免阻塞主线程。
        PathResult result1 = future1.join();
        PathResult result2 = future2.join();
        PathResult shortest = futureShortest.join();  // 计费一般基于最小路径
        PrefixFilter.FilterResult unique1 = PrefixFilter.filterUniquePrefixBlocksWithCount(result1.path);
        PrefixFilter.FilterResult unique2 = PrefixFilter.filterUniquePrefixBlocksWithCount(result2.path);
        PrefixFilter.FilterResult unique3 = PrefixFilter.filterUniquePrefixBlocksWithCount(shortest.path);
        result1.path = unique1.getFilteredList();
        result2.path = unique2.getFilteredList();
        shortest.path = unique3.getFilteredList();
        result1.transfers -= unique1.getRemovedCount();
        result2.transfers -= unique2.getRemovedCount();
        shortest.transfers -= unique3.getRemovedCount();
        // 票价基于清理后的路径计算，避免重复方向节点造成重复计费。
        Pair<Double, Double> fare1 = FareCalculator.calculateFareWithoutSpecial(shortest.path, this);
        Pair<Double, Double> fare2 = FareCalculator.calculateFareWithSpecial(result2.path, this);

        double cost1 = fare1.first * weight.moneyWeight + result1.cost;
        double cost2 = fare2.first * weight.moneyWeight + result2.cost;

        result1.totalMoney = fare1.first;
        result1.specialMoney = fare1.second;
        result2.totalMoney = fare2.first;
        result2.specialMoney = fare2.second;

        if (weight.moneyWeight == Double.POSITIVE_INFINITY && result1.transfers != -1) {
            result1.totalMoney = fare1.first;
            result1.specialMoney = 0;
            return result1;
        } else if (result1.transfers == -1) {
            return result2;
        } else {
            result1.cost = cost1;
            result2.cost = cost2;

            return cost1 < cost2 ? result1 : result2;
        }
    }

    /**
     * 查找从 start 到 end 的最佳路径，根据权重参数优化。
     * 若排除特殊线路（如机场线）失败，则自动重试允许特殊线路。
     */
    public PathResult findTransferPath(String start, String end, SearchWeight weight) {
        return findTransferPath(start, end, weight, System.currentTimeMillis());
    }


    // ==================== 内部 Dijkstra 实现 ====================
    PathResult findTransferPathInternal(String start, String end, SearchWeight weight, boolean excludeSpecial, long travelTimeMillis) {
        if (start.equals(end)) {
            Set<String> dirs = stationLines.get(start);
            if (dirs != null && !dirs.isEmpty()) {
                Set<String> filtered = excludeSpecial ? StationUtils.filterOutSpecialDirs(dirs) : dirs;
                if (!filtered.isEmpty()) {
                    String firstDir = filtered.iterator().next();
                    return new PathResult(0, 0, Collections.singletonList(GraphKeyUtils.makeNode(firstDir, start)));
                }
            }
            return new PathResult(0, 0, Collections.emptyList());
        }

        Set<String> startDirs = stationLines.get(start);
        Set<String> endDirs = stationLines.get(end);
        if (startDirs == null || endDirs == null || startDirs.isEmpty() || endDirs.isEmpty()) {
            return new PathResult(-1, 0, Collections.emptyList());
        }

        if (excludeSpecial) {
            startDirs = StationUtils.filterOutSpecialDirs(startDirs);
            endDirs = StationUtils.filterOutSpecialDirs(endDirs);
            if (startDirs.isEmpty() || endDirs.isEmpty()) {
                return new PathResult(-1, 0, Collections.emptyList());
            }
        }

        Map<String, Double> distTotal = new HashMap<>();
        Map<String, String> prev = new HashMap<>();
        PriorityQueue<NodeState> heap = new PriorityQueue<>(Comparator.comparingDouble(ns -> ns.totalCost));

        // 初始化起点节点
        for (String dir : startDirs) {
            String lineId = GraphKeyUtils.getLineIdFromDirKey(dir);
            String status = StationUtils.getStationLineStatus(start, lineId);
            if (StationUtils.isClosed(status)) continue;
            if (StationUtils.isFutureStation(start)) continue;  // 新增
            String node = GraphKeyUtils.makeNode(dir, start);
            distTotal.put(node, 0.0);
            heap.add(new NodeState(node, 0.0));
        }

        DataCache cache = DataCache.getInstance();
        String foundNode = null;
        double costValue = 0.0;

        // Dijkstra 主循环：堆顶始终是当前累计代价最低的候选节点。
        while (!heap.isEmpty()) {
            NodeState cur = heap.poll();
            String curNode = cur.node;
            double curCost = cur.totalCost;
            if (distTotal.getOrDefault(curNode, Double.POSITIVE_INFINITY) < curCost - 1e-9) continue;

            String[] curParsed = GraphKeyUtils.parseNode(curNode);
            String station = curParsed[1];

            // 到达终点后仍需检查线路状态，关闭或特殊线路节点不能直接作为结果。
            // 到达终点
            if (station.equals(end)) {
                String dirKey = curParsed[0];
                if (excludeSpecial && StationUtils.isSpecialDirKey(dirKey)) continue;
                String lineId = GraphKeyUtils.getLineIdFromDirKey(dirKey);
                String status = StationUtils.getStationLineStatus(end, lineId);
                if (StationUtils.isClosed(status)) continue;
                foundNode = curNode;
                break;
            }

            List<Edge> edges = transferGraph.get(curNode);
            if (edges == null) continue;

            for (Edge edge : edges) {
                String neighbor = edge.node;
                double edgeCost = computeEdgeCost(curNode, neighbor, weight, cache, excludeSpecial, travelTimeMillis);
                if (edgeCost < 0) continue; // 不可通行

                double newCost = curCost + edgeCost;
                Double oldCost = distTotal.get(neighbor);
                if (oldCost == null || newCost < oldCost - 1e-9) {
                    costValue = newCost;
                    distTotal.put(neighbor, newCost);
                    prev.put(neighbor, curNode);
                    heap.add(new NodeState(neighbor, newCost));
                }
            }
        }

        if (foundNode == null) {
            return new PathResult(-1, 0, Collections.emptyList());
        }

        // 回溯路径
        List<String> path = new ArrayList<>();
        String curNode = foundNode;
        while (prev.containsKey(curNode)) {
            path.add(curNode);
            curNode = prev.get(curNode);
        }
        path.add(curNode);
        Collections.reverse(path);

        int transfers = countTransfers(path);
        return new PathResult(transfers, costValue, path);
    }

    // ==================== 边代价计算 ====================
    /**
     * 计算从 fromNode 到 toNode 的边代价（用于 Dijkstra 搜索）。
     * <p>
     * 对于换乘边（方向不同），会查询特殊换乘时间；若换乘状态为关闭，则返回 -1 表示不可通行。
     * </p>
     *
     * @param fromNode      当前节点（格式：dirKey|station）
     * @param toNode        目标节点（格式：dirKey|station）
     * @param weight        权重配置（包含换乘权重、时间权重等）
     * @param cache         DataCache 实例（用于获取线路最高速度等）
     * @param excludeSpecial 是否排除特殊线路（如机场线）
     * @return 边代价，若不可通行则返回 -1
     */
    private double computeEdgeCost(String fromNode, String toNode, SearchWeight weight,
                                   DataCache cache, boolean excludeSpecial, long travelTimeMillis) {
        String[] fromParsed = GraphKeyUtils.parseNode(fromNode);
        String[] toParsed = GraphKeyUtils.parseNode(toNode);
        String fromDir = fromParsed[0];
        String toDir = toParsed[0];
        String fromStation = fromParsed[1];
        String toStation = toParsed[1];

        // 如果是换乘边（不同方向）
        if (!GraphKeyUtils.isEndEqual(fromDir, toDir)) {
            // 换乘边不增加里程，只叠加换乘耗时、候车时间和换乘次数权重。
            String transferStatus = StationUtils.getTransferStatus(fromStation, fromDir, toDir);
            if (StationUtils.isClosed(transferStatus)) {
                return -1;
            }
            double transferTime = StationUtils.getTransferTime(fromStation, fromDir, toDir);
            String line2 = GraphKeyUtils.getLineIdFromDirKey(toDir);
            double waitTime = TimeCalculator.computeTransferWaitTime(toDir, travelTimeMillis, cache);
            return weight.transferWeight + weight.timeWeight * (transferTime + waitTime);
        }

        // 同一方向内的边
        // 同方向边必须在邻接距离表中存在，缺失距离视为不可通行而不是估算。
        if (excludeSpecial && StationUtils.isSpecialDirKey(fromDir)) {
            return -1;
        }

        Map<String, Map<String, Double>> adj = lineAdjDist.get(fromDir);
        if (adj == null) {
            return -1;
        }

        Double addDist = null;
        Map<String, Double> fromMap = adj.get(fromStation);
        if (fromMap != null) {
            addDist = fromMap.get(toStation);
        }
        if (addDist == null) {
            Map<String, Double> toMap = adj.get(toStation);
            if (toMap != null) {
                addDist = toMap.get(fromStation);
            }
        }
        if (addDist == null) {
            return -1;
        }

        String lineId = GraphKeyUtils.getLineIdFromDirKey(fromDir);
        int maxSpeed = cache.getLineMaxSpeedMap() != null ?
                cache.getLineMaxSpeedMap().getOrDefault(lineId, 80) : 80;

        // ***** 修改此处：传入方向键和起终点站名以支持限速 *****
        double segmentTime = TimeCalculator.computeSegmentTime(addDist, maxSpeed, lineId,
                fromDir, fromStation, toStation);


        // 基础代价（距离 + 时间 + 站数）
        double cost = weight.distanceWeight * addDist
                + weight.timeWeight * segmentTime
                + weight.stationWeight * 1;

        // ========== 新增拥挤度惩罚 ==========
        // 拥挤倍率只增加代价，不因低于 1 而奖励路线，避免算法偏向异常低倍率时段。
        if (weight.crowdWeight > 0) {
            double crowdFactor = TimeCalculator.findCrowdFactor(fromDir, travelTimeMillis);
            // 只惩罚拥挤（factor > 1），宽松不减少代价（保持中性）
            if (crowdFactor > 1.0) {
                cost += weight.crowdWeight * (crowdFactor - 1.0);
            }
        }

        return cost;
    }

    private int countTransfers(List<String> path) {
        int transfers = 0;
        for (int i = 0; i < path.size() - 1; i++) {
            String dir1 = GraphKeyUtils.parseNode(path.get(i))[0];
            String dir2 = GraphKeyUtils.parseNode(path.get(i + 1))[0];
            if (!dir1.equals(dir2)) transfers++;
        }
        return transfers;
    }

    // ==================== 路径信息查询 ====================
    /**
     * 计算路径中相邻站点区间的总距离，返回值单位为公里。
     */
    public double computePathDistance(List<String> path) {
        double total = 0.0;
        for (int i = 0; i < path.size() - 1; i++) {
            String[] p1 = GraphKeyUtils.parseNode(path.get(i));
            String[] p2 = GraphKeyUtils.parseNode(path.get(i + 1));
            String dir1 = p1[0], st1 = p1[1];
            String dir2 = p2[0], st2 = p2[1];
            if (st1.equals(st2) || !dir1.equals(dir2)) continue;
            Map<String, Map<String, Double>> adj = lineAdjDist.get(dir1);
            if (adj == null) continue;
            Double d = adj.getOrDefault(st1, Collections.emptyMap()).get(st2);
            if (d == null) d = adj.getOrDefault(st2, Collections.emptyMap()).get(st1);
            if (d != null) total += d;
        }
        return total;
    }

    /**
     * 返回路径中不重复的站点数量。
     */
    public int getUniqueStationCount(List<String> path) {
        if (path == null || path.isEmpty()) return 0;
        Set<String> stationSet = new HashSet<>();
        for (String node : path) {
            stationSet.add(GraphKeyUtils.parseNode(node)[1]);
        }
        return stationSet.size();
    }

    /**
     * 获取同一方向内两站之间的距离，若不存在则返回 -1。
     */
    public double getDistanceInDirection(String dirKey, String from, String to) {
        Map<String, Map<String, Double>> adj = lineAdjDist.get(dirKey);
        if (adj == null) return -1;
        Double d = adj.getOrDefault(from, Collections.emptyMap()).get(to);
        if (d == null) d = adj.getOrDefault(to, Collections.emptyMap()).get(from);
        return d != null ? d : -1;
    }

    // ==================== 获取行程分段（用于界面展示） ====================
    /**
     * 根据路径返回结构化的行程分段列表，自动判断方向。
     */
    public List<RouteSegment> getRouteSegments(List<String> path) {
        List<RouteSegment> segments = new ArrayList<>();
        if (path == null || path.size() < 2) return segments;

        // 1. 将路径拆分为连续同方向段；每次方向变化都代表一次换乘。
        List<Segment> rawSegments = new ArrayList<>();
        String[] firstParsed = GraphKeyUtils.parseNode(path.get(0));
        String curDir = firstParsed[0];
        List<String> curStations = new ArrayList<>();
        curStations.add(firstParsed[1]);

        for (int i = 1; i < path.size(); i++) {
            String[] parsed = GraphKeyUtils.parseNode(path.get(i));
            String dir = parsed[0];
            String st = parsed[1];
            if (dir.equals(curDir)) {
                curStations.add(st);
            } else {
                rawSegments.add(new Segment(curDir, curStations));
                curDir = dir;
                curStations = new ArrayList<>();
                curStations.add(st);
            }
        }
        rawSegments.add(new Segment(curDir, curStations));

        // 2. 转换为 RouteSegment
        for (int idx = 0; idx < rawSegments.size(); idx++) {
            Segment seg = rawSegments.get(idx);
            String dirKey = seg.dirKey;
            List<String> stations = seg.stations;
            if (stations.isEmpty()) continue;

            String startStation = stations.get(0);
            String endStation = stations.get(stations.size() - 1);

            // 判断是否为出站换乘，状态由站点换乘表提供。
            boolean isOutStationTransfer = false;
            if (idx > 0) {
                String prevDirKey = rawSegments.get(idx - 1).dirKey;
                String status = StationUtils.getTransferStatus(startStation, prevDirKey, dirKey);
                if ("out_station".equals(status)) {
                    isOutStationTransfer = true;
                }
            }

            String[] dirParts = GraphKeyUtils.parseDirKey(dirKey);
            String lineId = dirParts[0];
            String start = dirParts[1];
            String end = dirParts[2];

            // 判断实际行驶终点（考虑方向）；反向乘车时终点应显示为方向起点。
            List<String> stList = dirStations.get(dirKey);
            String actualEnd = end;
            if (stList != null && stations.size() >= 2) {
                String first = stations.get(0);
                String last = stations.get(stations.size() - 1);
                int idxFirst = stList.indexOf(first);
                int idxLast = stList.indexOf(last);
                if (idxFirst >= 0 && idxLast >= 0) {
                    actualEnd = (idxFirst < idxLast) ? end : start;
                }
            }

            // 构建方向显示文本
            String directionDisplay = buildDirectionDisplay(lineId, dirKey, stations, actualEnd);

            // 线路名称
            String lineName = dirDisplayNameMap.getOrDefault(dirKey, lineId);

            // 站数
            int stationCount = stations.size() - 1;

            segments.add(new RouteSegment(
                    startStation, lineId, lineName,
                    directionDisplay, endStation, stationCount,
                    isOutStationTransfer, dirKey
            ));
        }

        return segments;
    }

    private String buildDirectionDisplay(String lineId, String dirKey, List<String> stations,
                                         String actualEnd) {
        Boolean isLoop = hasLoopMap.get(lineId);
        if (isLoop != null && isLoop) {
            String dirType = DataCache.getInstance().getDirDirectionTypeMap().get(dirKey);
            if ("inner".equals(dirType)) {
                return stations.get(1) + "（内环）";
            } else if ("outer".equals(dirType)) {
                return stations.get(1) + "（外环）";
            } else {
                return actualEnd;
            }
        }

        // 如果终点是暂缓开通站，需要替换为最近的非暂缓站
        String status = StationUtils.getStationLineStatus(actualEnd, lineId);
        if (StationUtils.isClosed(status) || StationUtils.isFutureStation(actualEnd)) {
            // 从站点列表中向前找第一个非暂缓站
            List<String> stList = dirStations.get(dirKey);
            if (stList != null && !stList.isEmpty()) {
                // 确定方向：stations 的第一个和最后一个在 stList 中的索引
                int startIdx = stList.indexOf(stations.get(0));
                int endIdx = stList.indexOf(stations.get(stations.size() - 1));
                if (startIdx >= 0 && endIdx >= 0) {
                    // 根据方向遍历
                    int step = (startIdx < endIdx) ? -1 : 1; // 向前搜索（从终点往起点方向）
                    int idx = stList.size() - 1;
                    while (idx < stList.size() && idx >= 0) {
                        String candidate = stList.get(idx);
                        String candStatus = StationUtils.getStationLineStatus(candidate, lineId);
                        if (!StationUtils.isClosed(candStatus) && !StationUtils.isFutureStation(candidate)) {
                            return candidate;
                        }
                        idx += step; // 继续向前
                    }
                }
            }
        }

        return actualEnd;
    }

    // ==================== 格式化乘车指引 ====================
    /**
     * 生成自然语言的乘车指引。
     */
    public String formatRouteWithDirection(List<String> path) {
        List<RouteSegment> segments = getRouteSegments(path);
        if (segments.isEmpty()) return "不可达";
        if (path.size() == 1) return "无需乘坐地铁";

        List<String> descParts = new ArrayList<>();
        for (int idx = 0; idx < segments.size(); idx++) {
            RouteSegment seg = segments.get(idx);
            String part = buildRouteDescription(seg, idx);
            descParts.add(part);
        }
        return String.join("；", descParts);
    }

    @NonNull
    private String buildRouteDescription(RouteSegment seg, int idx) {
        String lineDisplay = seg.lineName;
        String directionQuote = "\"" + seg.direction + "\"";
        String endQuote = "\"" + seg.endStation + "\"";
        if (idx == 0) {
            return "乘坐开往" + directionQuote + "的" + lineDisplay +
                    "，" + seg.stationCount + "站后到达" + endQuote;
        } else {
            String transferQuote = "\"" + seg.startStation + "\"";
            String transferType = seg.isOutStationTransfer ? "出站换乘" : "换乘";
            return "再" + transferQuote + transferType + "开往" + directionQuote +
                    "的" + lineDisplay + "，" + seg.stationCount + "站后到达" + endQuote;
        }
    }

    /**
     * 生成一条随机路径（基于高斯随机权重），并确保总时间不超过指定小时数。
     *
     * @param start       起点
     * @param end         终点
     * @param weight      权重配置
     * @param maxAttempts 最大尝试次数
     * @return 满足时间限制的随机路径，若多次失败则返回最短路径
     */
    public PathResult findRandomPath(String start, String end, SearchWeight weight, int maxAttempts) {
        PathResult shortestResult = findTransferPath(start, end, SearchWeight.getShortestWeight());
        double maxHours = 4; // 一般地铁有限时，这个不能突破
        double minHours = shortestResult.cost * 1.2 / 60.0;

        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            // 1. 基于当前时间 + attempt 生成种子，确保每次尝试不同
            Random rand = new Random();

            // 2. 为每个站点生成高斯随机权重
            List<String> sortedStations = new ArrayList<>(stationLines.keySet());
            Collections.sort(sortedStations);
            Map<String, Double> stationRandomWeight = new HashMap<>();
            for (String station : sortedStations) {
                stationRandomWeight.put(station, rand.nextGaussian());
            }

            // 3. 执行随机 Dijkstra（内部会使用这些权重）
            PathResult result = doRandomDijkstra(start, end, weight, stationRandomWeight);
            if (result != null && result.transfers != -1) {
                double totalHours = result.cost / 3600.0;
                if (totalHours <= maxHours && totalHours >= minHours) {
                    Pair<Double, Double> fare1 = FareCalculator.calculateFareWithoutSpecial(result.path, this);
                    result.totalMoney = fare1.first;
                    result.specialMoney = fare1.second;
                    PrefixFilter.FilterResult unique = PrefixFilter.filterUniquePrefixBlocksWithCount(result.path);
                    result.path = unique.getFilteredList();
                    result.transfers -= unique.getRemovedCount();
                    return result; // 时间合格，返回
                }
            }
        }

        // 5. 降级：返回最短路径
        return shortestResult;
    }

    /**
     * 生成一条最长路径（随机近似算法，基于高斯随机权重），并确保总时间不超过指定小时数。
     *
     * @param start       起点
     * @param end         终点
     * @param weight      权重配置
     * @param maxAttempts 最大尝试次数
     * @return 满足时间限制的随机路径，若多次失败则返回最短路径
     */
    public PathResult findLongestPath(String start, String end, SearchWeight weight, int maxAttempts) {
        // 1. 先获取最短路径作为基准
        PathResult longestPath = findTransferPath(start, end, SearchWeight.getShortestWeight());
        double maxHours = 4.0; // 固定上限
        double minHours = longestPath.cost * 1.2 / 60.0; // 固定下限

        // 2. 准备任务列表（每个尝试独立）
        List<Callable<PathResult>> tasks = new ArrayList<>(maxAttempts);
        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            tasks.add(() -> {
                // 每个任务独立生成随机权重
                List<String> sortedStations = new ArrayList<>(stationLines.keySet());
                Collections.sort(sortedStations);
                Map<String, Double> stationRandomWeight = new HashMap<>();
                for (String station : sortedStations) {
                    // 使用 ThreadLocalRandom 避免竞争
                    stationRandomWeight.put(station, ThreadLocalRandom.current().nextGaussian());
                }

                // 执行随机 Dijkstra
                PathResult result = doRandomDijkstra(start, end, weight, stationRandomWeight);
                if (result != null && result.transfers != -1) {
                    double totalHours = result.cost / 3600.0;
                    if (totalHours <= maxHours && totalHours >= minHours) {
                        // 计算票价（与原逻辑一致）
                        Pair<Double, Double> fare = FareCalculator.calculateFareWithoutSpecial(result.path, this);
                        result.totalMoney = fare.first;
                        result.specialMoney = fare.second;
                        return result;
                    }
                }
                return null; // 不符合条件则返回 null
            });
        }

        // 3. 使用固定线程池并行执行
        ExecutorService executor = Executors.newFixedThreadPool(4);
        try {
            List<Future<PathResult>> futures = executor.invokeAll(tasks);
            // 4. 收集结果，更新最优路径
            for (Future<PathResult> future : futures) {
                PathResult result = future.get(); // 可能抛出异常，但这里假设任务不抛异常
                if (result != null && result.cost > longestPath.cost) {
                    longestPath = result;

                    PrefixFilter.FilterResult unique = PrefixFilter.filterUniquePrefixBlocksWithCount(longestPath.path);
                    longestPath.path = unique.getFilteredList();
                    longestPath.transfers -= unique.getRemovedCount();
                }
            }
        } catch (InterruptedException | ExecutionException e) {
            // 适当处理异常，例如记录日志，此处简化为重新抛出运行时异常
            Thread.currentThread().interrupt();
            throw new RuntimeException("Parallel execution failed", e);
        } finally {
            executor.shutdown();
        }

        // 5. 返回最优路径（若无更优则返回最短路径）
        return longestPath;
    }

    private PathResult doRandomDijkstra(String start, String end, SearchWeight weight,
                                        Map<String, Double> stationRandomWeight) {
        if (start.equals(end)) {
            Set<String> dirs = stationLines.get(start);
            if (dirs != null && !dirs.isEmpty()) {
                String firstDir = dirs.iterator().next();
                return new PathResult(0, 0, Collections.singletonList(GraphKeyUtils.makeNode(firstDir, start)));
            }
            return new PathResult(0, 0, Collections.emptyList());
        }

        Set<String> startDirs = stationLines.get(start);
        Set<String> endDirs = stationLines.get(end);
        if (startDirs == null || startDirs.isEmpty() || endDirs == null || endDirs.isEmpty()) {
            return null;
        }

        // ---------- Dijkstra 数据结构 ----------
        Map<String, Double> dist = new HashMap<>();
        Map<String, String> prev = new HashMap<>();
        Set<String> visitedStations = new HashSet<>();
        PriorityQueue<NodeState> heap = new PriorityQueue<>(Comparator.comparingDouble(ns -> ns.totalCost));

        for (String dir : startDirs) {
            String node = GraphKeyUtils.makeNode(dir, start);
            dist.put(node, 0.0);
            heap.add(new NodeState(node, 0.0));
        }

        // 为了偶尔引入随机选择，从堆顶取两个节点随机选一个（保留一点随机性）
        Random rand = new Random();

        String foundNode = null;
        while (!heap.isEmpty()) {
            // 随机化选择：从堆顶的两个候选节点中随机选一个
            NodeState first = heap.poll();
            NodeState second = heap.peek();
            NodeState cur;
            if (second != null && rand.nextBoolean()) {
                cur = heap.poll();
                heap.add(first);  // 将第一个放回
            } else {
                cur = first;
            }

            String curNode = cur.node;
            double curCost = cur.totalCost;
            if (dist.getOrDefault(curNode, Double.POSITIVE_INFINITY) < curCost - 1e-9) continue;

            String[] parsed = GraphKeyUtils.parseNode(curNode);
            String station = parsed[1];

            if (station.equals(end)) {
                foundNode = curNode;
                break;
            }

            List<Edge> edges = transferGraph.get(curNode);
            if (edges == null) continue;

            for (Edge edge : edges) {
                String neighborNode = edge.node;
                String[] neighborParsed = GraphKeyUtils.parseNode(neighborNode);
                String neighborStation = neighborParsed[1];

                // 跳过已访问站点
                if (visitedStations.contains(neighborStation)) continue;

                // 计算基础边代价（距离、时间、换乘等）
                if (computeEdgeCost(curNode, neighborNode, weight, DataCache.getInstance(), false, 0) < 0) continue;

                // 叠加随机权重（目标站点的高斯权重）
                double edgeCost = stationRandomWeight.getOrDefault(neighborStation, 0.0);

                double newCost = curCost + edgeCost;
                Double oldCost = dist.get(neighborNode);
                if (oldCost == null || newCost < oldCost - 1e-9) {
                    dist.put(neighborNode, newCost);
                    prev.put(neighborNode, curNode);
                    heap.add(new NodeState(neighborNode, newCost));
                }
            }

            visitedStations.add(station);
        }

        if (foundNode == null) return null;

        // 回溯路径
        List<String> path = new ArrayList<>();
        String node = foundNode;
        while (prev.containsKey(node)) {
            path.add(node);
            node = prev.get(node);
        }
        path.add(node);
        Collections.reverse(path);

        int transfers = countTransfers(path);
        double totalTimeSec = TimeCalculator.computeTotalTime(path, this, weight, 1783296000);
        return new PathResult(transfers, totalTimeSec, path);
    }

    // ==================== 访问器 ====================
    public Set<String> getAllStations() {
        return stationLines.keySet();
    }
}
