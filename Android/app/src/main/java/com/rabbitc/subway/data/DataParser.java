package com.rabbitc.subway.data;

import com.rabbitc.subway.data.json.*;
import com.rabbitc.subway.utils.GraphKeyUtils;
import com.rabbitc.subway.utils.StationUtils;
import com.google.gson.Gson;

import java.util.*;

/**
 * 地图数据解析器，使用 Gson 将原始 JSON 字符串解析为结构化数据并存入 {@link DataCache}。
 */
public final class DataParser {

    private static final String TAG = "DataParser";
    private static final Gson GSON = new Gson(); // 加上 static final

    private DataParser() {
        // 工具类禁止实例化
    }

    // ======================== 公共入口 ========================

    /**
     * 将缓存中的 JSON 解析为运行时模型，并构建路线规划索引。
     *
     * @throws Exception 解析失败时抛出
     */
    public static void parse() throws Exception {
        DataCache cache = DataCache.getInstance();
        String jsonData = cache.getJsonData();
        if (jsonData == null) {
            cache.clear();
            return;
        }

        // 1. 使用 Gson 解析整个 JSON 结构；后续步骤只处理内存对象，不再访问原始字符串。
        MetroData data = GSON.fromJson(jsonData, MetroData.class);
        cache.setMetroData(data);

        // 2. 基础运营参数：缺省值保证旧格式地图也能参与时间计算。
        String keyCity = data.keyCity != null ? data.keyCity : "未知";
        cache.setKeyCity(keyCity);
        if (data.operationParams != null) {
            cache.setAvgTransferTime(data.operationParams.innerTransferTime);
            cache.setAvgStopTime(data.operationParams.dwellTime);
            cache.setOutStationTransferTime(data.operationParams.outStationTransferTime);
            cache.setAvgEntryTime(data.operationParams.entryTime);
        } else {
            cache.setAvgTransferTime(120.0);
            cache.setAvgStopTime(45.0);
            cache.setOutStationTransferTime(300.0);
            cache.setAvgEntryTime(120.0);
        }

        // 3. 解析站点相关：先建立坐标和状态索引，后续图构建与页面查询都复用这些表。
        Map<String, double[]> stationCoords = new HashMap<>();
        Map<String, Map<String, String>> stationLineStatus = new HashMap<>();
        Map<String, Map<String, String>> transferStatus = new HashMap<>();
        Map<String, Double> specialTransferTimeMap = new HashMap<>();
        if (data.stations != null) {
            parseStations(data.stations, stationCoords, stationLineStatus, transferStatus, specialTransferTimeMap);
        }
        cache.setStationCoords(stationCoords);
        cache.setStationLineStatusMap(stationLineStatus);
        cache.setTransferStatusMap(transferStatus);
        cache.setSpecialTransferTimeMap(specialTransferTimeMap);

        // 4. 解析线路相关：方向键是路径图的主键，同时保存线路级和方向级属性。
        Map<String, List<String>> dirStations = new HashMap<>();
        Map<String, String> lineNameMap = new HashMap<>();
        Map<String, String> lineColorMap = new HashMap<>();
        Map<String, String> lineCityMap = new HashMap<>();
        Map<String, Boolean> hasLoopMap = new HashMap<>();
        Map<String, Double> lineTotalLengthMap = new HashMap<>();
        Map<String, Boolean> lineSpecialMap = new HashMap<>();
        Map<String, Boolean> lineHasFastMap = new HashMap<>();
        Map<String, Integer> lineMaxSpeedMap = new HashMap<>();
        Map<String, String> dirDirectionTypeMap = new HashMap<>();
        Map<String, Boolean> dirFutureMap = new HashMap<>();
        Map<String, Double> lineBrakeMap = new HashMap<>();
        Map<String, Double> stationStopTimeMap = new HashMap<>();
        Map<String, String> lineFormationMap = new HashMap<>();
        Map<String, Double> linePowerKwMap = new HashMap<>();
        Map<String, Double> lineTotalLengthWithoutFutureMap = new HashMap<>();
        Map<String, Boolean> hasFutureMap = new HashMap<>();
        Map<String, Boolean> lineAllFutureMap = new HashMap<>();
        Map<String, Boolean> dirLoopMap = new HashMap<>();

        if (data.lines != null) {
            parseLines(data.lines, keyCity, dirStations, lineNameMap, lineColorMap,
                    lineCityMap, hasLoopMap, lineTotalLengthMap, lineSpecialMap,
                    lineHasFastMap, lineMaxSpeedMap, dirDirectionTypeMap,
                    dirFutureMap, lineBrakeMap, stationStopTimeMap, lineFormationMap, linePowerKwMap,
                    lineTotalLengthWithoutFutureMap, hasFutureMap, lineAllFutureMap, dirLoopMap);
        }

        // 5. 限速规则：把 JSON DTO 转成运行时模型，避免算法层依赖 Gson 映射类。
        if (data.speedLimits != null) {
            List<com.rabbitc.subway.data.parse.SpeedLimit> speedLimits = new ArrayList<>();
            for (SpeedLimit sl : data.speedLimits) {
                com.rabbitc.subway.data.parse.SpeedLimit cacheSl = new com.rabbitc.subway.data.parse.SpeedLimit();
                cacheSl.directionKey = sl.directionKey;
                cacheSl.fromStation = sl.fromStation;
                cacheSl.toStation = sl.toStation;
                cacheSl.speedKmh = sl.speedKmh;
                speedLimits.add(cacheSl);
            }
            cache.setSpeedLimits(speedLimits);
        }

        // 6. 存入线路与基础属性到缓存；完成后 MetroGraph 才能安全构建索引。
        cache.setStationStopTimeMap(stationStopTimeMap);
        cache.setDirDirectionTypeMap(dirDirectionTypeMap);
        cache.setDirStations(dirStations);
        cache.setLineNameMap(lineNameMap);
        cache.setLineColorMap(lineColorMap);
        cache.setLineCityMap(lineCityMap);
        cache.setHasLoopMap(hasLoopMap);
        cache.setLineTotalLengthMap(lineTotalLengthMap);
        cache.setLineSpecialMap(lineSpecialMap);
        cache.setLineHasFastMap(lineHasFastMap);
        cache.setLineMaxSpeedMap(lineMaxSpeedMap);
        cache.setLineBrakeMap(lineBrakeMap);
        cache.setLineFormationMap(lineFormationMap);
        cache.setLinePowerKwMap(linePowerKwMap);
        cache.setDirFutureMap(dirFutureMap);
        cache.setAllFutureMap(lineAllFutureMap);
        cache.setDirLoopMap(dirLoopMap);

        // 7. 计算分段运营：通过连通分量识别同一线路中的不连续区段。
        Map<String, Boolean> lineSegmentedMap = new HashMap<>();
        for (String lineId : lineNameMap.keySet()) {
            lineSegmentedMap.put(lineId, isLineSegmented(lineId, dirStations));
        }
        cache.setLineSegmentedMap(lineSegmentedMap);

        // 8. 版本信息
        cache.setVersion(data.version != null ? data.version : "未知");
        cache.setUpdateDate(data.updateDate != null ? data.updateDate : "未知");

        // 9. 票价解析：普通分段、线性延伸和特殊线路规则统一交给缓存保存。
        parsePrice(data.price, cache);

        // 10. 拥挤规则 (crowd_rules)：保留原始时段和倍率，查询时再按出发时间匹配。
        if (data.crowdRules != null) {
            List<com.rabbitc.subway.data.parse.CrowdRule> rules = new ArrayList<>();
            for (CrowdRule cr : data.crowdRules) {
                com.rabbitc.subway.data.parse.CrowdRule rule = new com.rabbitc.subway.data.parse.CrowdRule();
                rule.directionKey = cr.directionKey;
                rule.startTime = cr.startTime;
                rule.endTime = cr.endTime;
                rule.factor = cr.factor;
                rule.days = cr.days;
                rules.add(rule);
            }
            cache.setCrowdRules(rules);
        }

        // 11. 行车间隔 (headway_rules)：用于估算站台候车时间。
        if (data.headwayRules != null) {
            List<com.rabbitc.subway.data.parse.HeadwayRule> rules = new ArrayList<>();
            for (HeadwayRule hr : data.headwayRules) {
                com.rabbitc.subway.data.parse.HeadwayRule rule = new com.rabbitc.subway.data.parse.HeadwayRule();
                rule.lineId = hr.lineId;
                rule.startTime = hr.startTime;
                rule.endTime = hr.endTime;
                rule.intervalSeconds = hr.intervalSeconds;
                rule.days = hr.days;
                rules.add(rule);
            }
            cache.setHeadwayRules(rules);
        }

        // 12. 运营时间 (open_times)：路径算法据此跳过未营业线路。
        if (data.openTimes != null) {
            List<com.rabbitc.subway.data.parse.OpenTimeRule> openTimes = new ArrayList<>();
            for (OpenTimeRule ot : data.openTimes) {
                com.rabbitc.subway.data.parse.OpenTimeRule rule = new com.rabbitc.subway.data.parse.OpenTimeRule();
                rule.lineId = ot.lineId;
                rule.startTime = ot.startTime;
                rule.endTime = ot.endTime;
                rule.days = ot.days;
                openTimes.add(rule);
            }
            cache.setOpenTimes(openTimes);
        }

        // 13. 虚拟换乘 (virtual_transfers)：把站外或跨站换乘映射为图上的额外边。
        if (data.virtualTransfers != null) {
            List<com.rabbitc.subway.data.parse.VirtualTransfer> virtualTransfers = new ArrayList<>();
            for (VirtualTransfer vt : data.virtualTransfers) {
                com.rabbitc.subway.data.parse.VirtualTransfer cacheVt = new com.rabbitc.subway.data.parse.VirtualTransfer();
                cacheVt.stationA = vt.stationA;
                cacheVt.stationB = vt.stationB;
                cacheVt.timeMinutes = vt.timeMinutes;
                cacheVt.securityCheck = vt.securityCheck;
                cacheVt.viaDirections = vt.viaDirections;
                virtualTransfers.add(cacheVt);
            }
            cache.setVirtualTransfers(virtualTransfers);
        }

        // 14. 设置其他信息
        cache.setHasFutureMap(hasFutureMap);
        cache.setLineTotalLengthMapWithoutFuture(lineTotalLengthWithoutFutureMap);

        // 15. 标记缓存有效
        cache.setCacheValid(true);
    }

    // ======================== Gson 对象转 DataCache 核心转换 ========================

    /**
     * 解析站点数据，填充站点坐标表、站点线路状态、换乘状态和特殊换乘时间。
     *
     * <p>站点坐标以经纬度数��� {@code double[]{lon, lat}} 存储；
     * 换乘状态同时处理字符串格式（纯状态）和对象格式（状态+特殊时间），
     * 特殊换乘时间会同时创建正向和反向键以支持双向查询。</p>
     *
     * @param stations               JSON 解析出的站点集合（站点名 → Station）
     * @param stationCoords          输出：站点坐标表（站点名 → [经度, 纬度]）
     * @param stationLineStatus      输出：站点线路状态表（站点名 → (线路ID → 状态)）
     * @param transferStatus         输出：换乘状态表（站点名 → (换乘键 → 状态)）
     * @param specialTransferTimeMap 输出：站点特殊换乘时间表（站点名|换乘键 → 时间）
     */
    private static void parseStations(Map<String, Station> stations,
                                      Map<String, double[]> stationCoords,
                                      Map<String, Map<String, String>> stationLineStatus,
                                      Map<String, Map<String, String>> transferStatus,
                                      Map<String, Double> specialTransferTimeMap) {
        for (Map.Entry<String, Station> entry : stations.entrySet()) {
            String name = entry.getKey();
            Station st = entry.getValue();

            // 坐标
            stationCoords.put(name, new double[]{st.lon, st.lat});

            // 站点线路状态
            if (st.lines != null) {
                Map<String, String> lineStatusMap = new HashMap<>();
                for (StationLine sl : st.lines) {
                    lineStatusMap.put(sl.id, sl.status != null ? sl.status : "operating");
                }
                stationLineStatus.put(name, lineStatusMap);
            }

            // 换乘状态 + 特殊时间（注意 JSON 中是字符串或对象的变种）
            if (st.transfers != null) {
                Map<String, String> transferMap = new HashMap<>();
                for (Map.Entry<String, Object> transEntry : st.transfers.entrySet()) {
                    String key = transEntry.getKey();
                    Object value = transEntry.getValue();
                    String status = "operating";
                    double time = 0;

                    // Gson 遇到 JSONObject 会解析为 Map
                    if (value instanceof Map) {
                        Map<?, ?> valMap = (Map<?, ?>) value;
                        Object statusObj = valMap.get("status");
                        if (statusObj instanceof String) status = (String) statusObj;

                        Object timeObj = valMap.get("time");
                        if (timeObj instanceof Double) {
                            time = (Double) timeObj;
                            if (time > 0) {
                                specialTransferTimeMap.put(name + "|" + key, time);
                                // 添加反向键
                                String[] rev = key.split("\\|");
                                if (rev.length >= 2) {
                                    String revKey = rev[1] + "|" + rev[0];
                                    specialTransferTimeMap.put(name + "|" + revKey, time);
                                }
                            }
                        }
                    } else if (value instanceof String) {
                        status = (String) value;
                    }
                    transferMap.put(key, status);
                }
                transferStatus.put(name, transferMap);
            }
        }
    }

    /**
     * 解析线路数据，填充方向站点列表、线路属性表以及方向级属性表。
     *
     * <p>遍历每条线路的所有方向，提取以下信息：</p>
     * <ul>
     *   <li>方向站点列表（方向键 → 站点名列表）</li>
     *   <li>线路级属性：名称、颜色、城市、环线、快车、最大速度等</li>
     *   <li>方向级属性：方向类型、未来标记、环线标记等</li>
     *   <li>线路距离：通过去重边计算总长度（含/不含未来方向）</li>
     * </ul>
     *
     * @param lines                          JSON 解析出的线路集合（线路ID → Line）
     * @param keyCity                        默认城市（key_city）
     * @param dirStations                    输出：方向站点表（方向键 → 站点名列表）
     * @param lineNameMap                    输出：线路显示名称表（线路ID → 名称）
     * @param lineColorMap                   输出：线路颜色���（线路ID → #RRGGBB）
     * @param lineCityMap                    输出：线路所属城市表（线路ID → 城市名）
     * @param hasLoopMap                     输出：线路是否有环线（线路ID → boolean）
     * @param lineTotalLengthMap             输出：线路总长度表（线路ID → 公里数）
     * @param lineSpecialMap                 输出：线路是否为特殊线路（线路ID → boolean）
     * @param lineHasFastMap                 输出：线路是否有快车（线路ID → boolean）
     * @param lineMaxSpeedMap                输出：线路最高速度表（线路ID → km/h）
     * @param dirDirectionTypeMap            输出：方向类型表（方向键 → 类型字符串）
     * @param dirFutureMap                   输出：方向未来状态表（方向键 → isFuture）
     * @param lineBrakeMap                   输出：线路制动加速度表（线路ID → m/s²）
     * @param stationStopTimeMap             输出：站点停站时间表（站点名|方向键 → 秒）
     * @param lineFormationMap               输出：线路编组表（线路ID → 编组类型如"6B"）
     * @param linePowerKwMap                 输出：线路功率表（线路ID → kW）
     * @param lineTotalLengthWithoutFutureMap 输出：不含未来站点的线路长度表（线路ID → 公里数）
     * @param hasFutureMap                   输出：线路是否包含未来方向（线路ID → boolean）
     * @param lineAllFutureMap               输出：线路是否全为未来方向（线路ID → boolean）
     * @param dirLoopMap                     输出：方向环线标记（方向键 → isLoop）
     */
    private static void parseLines(Map<String, Line> lines, String keyCity,
                                   Map<String, List<String>> dirStations,
                                   Map<String, String> lineNameMap,
                                   Map<String, String> lineColorMap,
                                   Map<String, String> lineCityMap,
                                   Map<String, Boolean> hasLoopMap,
                                   Map<String, Double> lineTotalLengthMap,
                                   Map<String, Boolean> lineSpecialMap,
                                   Map<String, Boolean> lineHasFastMap,
                                   Map<String, Integer> lineMaxSpeedMap,
                                   Map<String, String> dirDirectionTypeMap,
                                   Map<String, Boolean> dirFutureMap,
                                   Map<String, Double> lineBrakeMap,
                                   Map<String, Double> stationStopTimeMap,
                                   Map<String, String> lineFormationMap,
                                   Map<String, Double> linePowerKwMap,
                                   Map<String, Double> lineTotalLengthWithoutFutureMap,
                                   Map<String, Boolean> hasFutureMap,
                                   Map<String, Boolean> lineAllFutureMap,
                                   Map<String, Boolean> dirLoopMap) {
        for (Map.Entry<String, Line> lineEntry : lines.entrySet()) {
            String lineId = lineEntry.getKey();
            Line line = lineEntry.getValue();
            boolean allFuture = true;

            // 性能参数
            if (line.powerKw > 0) linePowerKwMap.put(lineId, line.powerKw);
            if (line.brake > 0) lineBrakeMap.put(lineId, line.brake);

            String city = line.city != null ? line.city : keyCity;
            String baseName = line.name != null ? line.name : lineId;
            String color = line.color != null ? line.color : "#999999";
            if (!color.startsWith("#")) color = "#" + color;
            String displayName = city.equals(keyCity) ? baseName : city + baseName;

            lineMaxSpeedMap.put(lineId, line.maxspeed);
            lineNameMap.put(lineId, displayName);
            lineColorMap.put(lineId, color);
            lineCityMap.put(lineId, city);

            lineSpecialMap.put(lineId, line.special);
            if (line.formation != null && !line.formation.isEmpty()) {
                lineFormationMap.put(lineId, line.formation);
            }

            // ---- 方向遍历 & 长度计算合并 ----
            if (line.direction != null) {
                boolean hasLoop = false;
                boolean hasFast = false;
                boolean hasFuture = false;

                // 用于去重边（总长度 & 不含未来方向长度）
                Set<String> edgeSetAll = new HashSet<>();
                Set<String> edgeSetWithoutFuture = new HashSet<>();
                double totalLengthAll = 0.0;
                double totalLengthWithoutFuture = 0.0;

                for (Direction dir : line.direction) {
                    if (dir.isFast) {
                        hasFast = true;
                        continue; // 快车不计入长度
                    }

                    String start = dir.start;
                    String end = dir.end;
                    String dirKey = GraphKeyUtils.makeDirKey(lineId, start, end);

                    // 未来标记
                    if (dir.isFuture) hasFuture = true;
                    else allFuture = false;
                    dirFutureMap.put(dirKey, dir.isFuture);

                    if (dir.isLoop) {
                        hasLoop = true;
                        dirLoopMap.put(dirKey, true);
                        if (dir.directionType != null && !dir.directionType.isEmpty()) {
                            dirDirectionTypeMap.put(dirKey, dir.directionType);
                        }
                    } else {
                        dirLoopMap.put(dirKey, false);
                    }

                    // 站点列表 & 停站时间 & 距离累加
                    List<String> stationNames = new ArrayList<>();
                    if (dir.station != null) {
                        int size = dir.station.size();
                        for (int i = 0; i < size; i++) {
                            DirectionStation ds = dir.station.get(i);
                            String name = ds.name;
                            stationNames.add(name);
                            if (ds.dwellTime != null && ds.dwellTime > 0) {
                                stationStopTimeMap.put(name + "|" + dirKey, ds.dwellTime);
                            }

                            // 计算边长（去重）
                            if (i < size - 1) {
                                String nextName = dir.station.get(i + 1).name;
                                double dist = ds.next != null ? ds.next : 0.0;
                                String key = name.compareTo(nextName) < 0 ?
                                        name + "|" + nextName : nextName + "|" + name;
                                if (edgeSetAll.add(key)) {
                                    totalLengthAll += dist;
                                }
                                if (!dir.isFuture && edgeSetWithoutFuture.add(key)) {
                                    totalLengthWithoutFuture += dist;
                                }
                            } else if (dir.isLoop) {
                                // 环线闭合边
                                String firstName = dir.station.get(0).name;
                                double dist = ds.next != null ? ds.next : 0.0;
                                String key = name.compareTo(firstName) < 0 ?
                                        name + "|" + firstName : firstName + "|" + name;
                                if (edgeSetAll.add(key)) {
                                    totalLengthAll += dist;
                                }
                                if (!dir.isFuture && edgeSetWithoutFuture.add(key)) {
                                    totalLengthWithoutFuture += dist;
                                }
                            }
                        }
                    }
                    dirStations.put(dirKey, stationNames);
                }

                // 线路级属性
                hasLoopMap.put(lineId, hasLoop);
                lineHasFastMap.put(lineId, hasFast);
                lineAllFutureMap.put(lineId, allFuture);

                // 存储长度结果
                lineTotalLengthMap.put(lineId, totalLengthAll);
                lineTotalLengthWithoutFutureMap.put(lineId, totalLengthWithoutFuture);
                hasFutureMap.put(lineId, hasFuture);
            }
        }
    }

    // ======================== 业务计算：分段运营判断 ========================

    /**
     * 判断线路是否为分段运营（即线路的站点组成多个不连通的连通分量）。
     *
     * <p>通过构建邻接表并运行 BFS 连通分量检测来实现。
     * 若存在超过一个连通分量，则该线路为分段运营。</p>
     *
     * @param lineId       线路ID
     * @param dirStations  ���有方向的站点列表
     * @return {@code true} 如果线路存在多个不连通的运营区段
     */
    private static boolean isLineSegmented(String lineId, Map<String, List<String>> dirStations) {
        Map<String, Set<String>> adj = new HashMap<>();

        for (Map.Entry<String, List<String>> entry : dirStations.entrySet()) {
            String dirKey = entry.getKey();
            String[] parts = GraphKeyUtils.parseDirKey(dirKey);
            if (!StationUtils.isFutureDirection(dirKey)) continue;
            if (!parts[0].equals(lineId)) continue;

            List<String> stations = entry.getValue();
            for (int i = 0; i < stations.size() - 1; i++) {
                String a = stations.get(i);
                String b = stations.get(i + 1);
                adj.computeIfAbsent(a, k -> new HashSet<>()).add(b);
                adj.computeIfAbsent(b, k -> new HashSet<>()).add(a);
            }

            // 环线闭合边
            String start = parts[1];
            String end = parts[2];
            if (start.equals(end) && stations.size() > 1) {
                String first = stations.get(0);
                String last = stations.get(stations.size() - 1);
                adj.computeIfAbsent(first, k -> new HashSet<>()).add(last);
                adj.computeIfAbsent(last, k -> new HashSet<>()).add(first);
            }
        }

        if (adj.isEmpty()) return false;

        Set<String> visited = new HashSet<>();
        int components = 0;
        for (String station : adj.keySet()) {
            if (!visited.contains(station)) {
                components++;
                if (components > 1) return true;
                Stack<String> stack = new Stack<>();
                stack.push(station);
                visited.add(station);
                while (!stack.isEmpty()) {
                    String cur = stack.pop();
                    for (String neighbor : adj.getOrDefault(cur, Collections.emptySet())) {
                        if (!visited.contains(neighbor)) {
                            visited.add(neighbor);
                            stack.push(neighbor);
                        }
                    }
                }
            }
        }
        return false;
    }

    // ======================== 票价解析 ========================

    /**
     * 解析票价数据并填充缓存。
     *
     * <p>处理票价体系中的四个核心部分：</p>
     * <ul>
     *   <li><b>计价类型</b>（pricingType）：如 "distance" 按里程计价</li>
     *   <li><b>阶梯区间</b>（segments）：里程范围与对应的票价</li>
     *   <li><b>线性扩展</b>（linearExtension）：超出阶梯上限后的线性计价规则</li>
     *   <li><b>特殊线路规则</b>（specialLines）：特定线路的附加费或独立计价规则</li>
     * </ul>
     * <p>还会设置票价封顶（fareCap）和默认票价（defaultFare）。</p>
     *
     * @param price JSON 解析出的票价对象（可为 null）
     * @param cache 全局数据缓存实例
     */
    private static void parsePrice(Price price, DataCache cache) {
        if (price == null) {
            cache.setFareSegments(null);
            cache.setSpecialLineRules(null);
            cache.setDefaultFare(null);
            return;
        }

        cache.setPricingType(price.pricingType != null ? price.pricingType : "distance");

        // 阶梯区间
        if (price.segments != null) {
            List<com.rabbitc.subway.data.parse.FareSegment> segments = new ArrayList<>();
            for (FareSegment fs : price.segments) {
                com.rabbitc.subway.data.parse.FareSegment cacheFs = new com.rabbitc.subway.data.parse.FareSegment();
                cacheFs.rangeStart = fs.rangeStart;
                cacheFs.rangeEnd = fs.rangeEnd;
                cacheFs.fare = fs.fare;
                segments.add(cacheFs);
            }
            cache.setFareSegments(segments);
        }

        // 线性扩展
        if (price.linearExtension != null) {
            com.rabbitc.subway.data.parse.LinearExtension le = new com.rabbitc.subway.data.parse.LinearExtension();
            le.threshold = price.linearExtension.threshold;
            le.baseFare = price.linearExtension.baseFare;
            le.ratePerKm = price.linearExtension.ratePerKm;
            cache.setLinearExtension(le);
        }

        // 特殊线路规则
        if (price.specialLines != null) {
            List<com.rabbitc.subway.data.parse.SpecialLineRule> rules = new ArrayList<>();
            for (SpecialLineRule sp : price.specialLines) {
                com.rabbitc.subway.data.parse.SpecialLineRule rule = new com.rabbitc.subway.data.parse.SpecialLineRule();
                rule.lineId = sp.lineId;
                rule.useLines = sp.useLines;
                rule.all = sp.all;

                if (sp.surcharge != null) {
                    rule.surcharge = new ArrayList<>();
                    for (Surcharge sur : sp.surcharge) {
                        com.rabbitc.subway.data.parse.Surcharge s = new com.rabbitc.subway.data.parse.Surcharge();
                        s.node_a = sur.node_a;
                        s.node_b = sur.node_b;
                        s.price = sur.price;
                        rule.surcharge.add(s);
                    }
                }

                rule.defaultFare = sp.defaultFare;
                if (sp.segments != null) {
                    rule.segments = new ArrayList<>();
                    for (FareSegment fs : sp.segments) {
                        com.rabbitc.subway.data.parse.FareSegment cacheFs = new com.rabbitc.subway.data.parse.FareSegment();
                        cacheFs.rangeStart = fs.rangeStart;
                        cacheFs.rangeEnd = fs.rangeEnd;
                        cacheFs.fare = fs.fare;
                        rule.segments.add(cacheFs);
                    }
                }
                if (sp.linearExtension != null) {
                    com.rabbitc.subway.data.parse.LinearExtension le = new com.rabbitc.subway.data.parse.LinearExtension();
                    le.threshold = sp.linearExtension.threshold;
                    le.baseFare = sp.linearExtension.baseFare;
                    le.ratePerKm = sp.linearExtension.ratePerKm;
                    rule.linearExtension = le;
                }
                rules.add(rule);
            }
            cache.setSpecialLineRules(rules);
        }

        cache.setFareCap(price.fareCap);
        cache.setDefaultFare(price.defaultFare);
    }
}
