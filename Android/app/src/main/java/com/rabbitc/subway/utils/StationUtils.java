package com.rabbitc.subway.utils;

import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.json.Direction;
import com.rabbitc.subway.data.json.DirectionStation;
import com.rabbitc.subway.data.json.Line;
import com.rabbitc.subway.data.json.MetroData;
import com.google.gson.Gson;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * 站点状态工具类。
 * <p>
 * 提供站点线路状态查询、换乘状态判断、换乘时间计算、特殊线路过滤
 * 及未来/已开通站点列表获取等功能。所有数据均来源于
 * {@link DataCache} 全局缓存单例。
 * </p>
 *
 * @see DataCache
 * @see GraphKeyUtils
 */
public class StationUtils {
    /**
     * 获取某个站点在某条线路上的状态（如 "operating", "pending" 等）。
     *
     * @param station 站点名称
     * @param lineId  线路ID
     * @return 状态字符串，默认为 "operating"
     */
    public static String getStationLineStatus(String station, String lineId) {
        DataCache cache = DataCache.getInstance();
        Map<String, Map<String, String>> map = cache.getStationLineStatusMap();
        if (map == null) return "operating";
        Map<String, String> lineMap = map.get(station);
        if (lineMap == null) return "operating";
        return lineMap.getOrDefault(lineId, "operating");
    }

    /**
     * 判断状态是否表示线路/站点已关闭或不可用。
     *
     * @param status 状态字符串
     * @return true 表示关闭/不可用
     */
    public static boolean isClosed(String status) {
        return !(status == null || status.equals("operating") || status.equals("out_station"));
    }

    /**
     * 判断是否为出站换乘状态。
     *
     * @param status 状态字符串
     * @return true 表示出站换乘
     */
    public static boolean isOutStation(String status) {
        return "out_station".equals(status);
    }

    /**
     * 获取两个方向键之间的换乘状态（站内换乘/出站换乘/关闭等）。
     *
     * @param station 换乘站名
     * @param dirKey1 方向键1
     * @param dirKey2 方向键2
     * @return 换乘状态字符串，默认为 "operating"
     */
    public static String getTransferStatus(String station, String dirKey1, String dirKey2) {
        DataCache cache = DataCache.getInstance();
        Map<String, Map<String, String>> map = cache.getTransferStatusMap();
        if (map == null) return "operating";
        Map<String, String> transferMap = map.get(station);
        if (transferMap == null) return "operating";
        String line1 = GraphKeyUtils.getLineIdFromDirKey(dirKey1);
        String line2 = GraphKeyUtils.getLineIdFromDirKey(dirKey2);
        if (line1 == null || line2 == null) return "operating";
        String key = line1.compareTo(line2) < 0 ? line1 + "|" + line2 : line2 + "|" + line1;
        return transferMap.getOrDefault(key, "operating");
    }

    /**
     * 获取站点在两线路间的换乘状态（简化版）。
     *
     * @param stationName 站点名称
     * @param lineA       线路A ID
     * @param lineB       线路B ID
     * @return 换乘状态字符串，默认为 "operating"
     */
    public static String getTransferStatusForStation(String stationName, String lineA, String lineB) {
        DataCache cache = DataCache.getInstance();
        Map<String, Map<String, String>> transferStatusMap = cache.getTransferStatusMap();
        if (transferStatusMap == null) return "operating";
        Map<String, String> stationTransfers = transferStatusMap.get(stationName);
        if (stationTransfers == null) return "operating";
        String key = lineA.compareTo(lineB) < 0 ? lineA + "|" + lineB : lineB + "|" + lineA;
        return stationTransfers.getOrDefault(key, "operating");
    }

    /**
     * 判断某个方向键是否属于特殊线路（如机场线等单独计费线路）。
     *
     * @param dirKey 方向键
     * @return true 表示特殊线路
     */
    public static boolean isSpecialDirKey(String dirKey) {
        DataCache cache = DataCache.getInstance();
        String lineId = GraphKeyUtils.getLineIdFromDirKey(dirKey);
        if (lineId == null) return false;
        Map<String, Boolean> map = cache.getLineSpecialMap();
        return map != null && map.getOrDefault(lineId, false);
    }

    /**
     * 从方向键集合中过滤掉特殊线路的方向键。
     *
     * @param dirKeys 方向键集合
     * @return 过滤后的集合
     */
    public static Set<String> filterOutSpecialDirs(Set<String> dirKeys) {
        if (dirKeys == null || dirKeys.isEmpty()) return Collections.emptySet();
        Set<String> result = new HashSet<>();
        for (String dk : dirKeys) {
            if (!isSpecialDirKey(dk)) {
                result.add(dk);
            }
        }
        return result;
    }

    /**
     * 获取两个方向键在指定站点的换乘时间（秒）。
     * <p>
     * 优先级：特殊换乘时间 > 出站换乘时间 > 普通换乘时间。
     * </p>
     *
     * @param station 换乘站名
     * @param dirKey1 方向键1（格式：lineId|start|end）
     * @param dirKey2 方向键2（格式：lineId|start|end）
     * @return 换乘时间（秒），若换乘状态为关闭则返回 0（调用方应自行检查）
     */
    public static double getTransferTime(String station, String dirKey1, String dirKey2) {
        // 1. 提取线路ID
        String line1 = GraphKeyUtils.getLineIdFromDirKey(dirKey1);
        String line2 = GraphKeyUtils.getLineIdFromDirKey(dirKey2);
        if (line1 == null || line2 == null) {
            return DataCache.getInstance().getAvgTransferTime();
        }

        // 2. 构建排序键
        String fullKey1 = station + "|" + line1 + "|" + line2;
        String fullKey2 = station + "|" + line2 + "|" + line1;

        // 3. 查询特殊换乘时间
        DataCache cache = DataCache.getInstance();
        Map<String, Double> specialMap = cache.getSpecialTransferTimeMap();
        if (specialMap != null && specialMap.containsKey(fullKey1) || specialMap.containsKey(fullKey2)) {
            double specialTime = specialMap.getOrDefault(fullKey1, 0.0);
            if (specialTime > 0) {
                return specialTime;
            }
            specialTime = specialMap.getOrDefault(fullKey2, 0.0);
            if (specialTime > 0) {
                return specialTime;
            }
        }

        // 4. 无特殊时间，根据换乘状态返回默认值
        String status = getTransferStatus(station, dirKey1, dirKey2);
        if ("out_station".equals(status)) {
            return cache.getOutStationTransferTime();
        } else {
            return cache.getAvgTransferTime();
        }
    }

    /**
     * 获取当前缓存中的所有站点名称列表（排序后）。
     *
     * @return 站点名称列表
     */
    public static List<String> getStationList() {
        DataCache cache = DataCache.getInstance();
        if (cache.getStationCoords() == null) {
            return Collections.emptyList();
        }
        List<String> list = new ArrayList<>(cache.getStationCoords().keySet());
        Collections.sort(list);
        return list;
    }

    /**
     * 判断站点是否为未来开通的站点。
     * <p>
     * 满足以下任一条件即为未来站点：
     * <ul>
     *     <li>该站点经过的所有方向键均为 is_future</li>
     *     <li>该站点经过的所有线路在该站的状态均为 pending（暂缓开通）</li>
     * </ul>
     * </p>
     *
     * @param station 站点名称
     * @return true 表示该站点为未来站点
     */
    public static boolean isFutureStation(String station) {
        DataCache cache = DataCache.getInstance();
        Map<String, List<String>> dirStations = cache.getDirStations();
        Map<String, Boolean> dirFutureMap = cache.getDirFutureMap();
        if (dirFutureMap == null || dirStations == null) return false;

        // 获取该站点经过的所有方向键
        Set<String> dirKeys = new HashSet<>();
        for (Map.Entry<String, List<String>> entry : dirStations.entrySet()) {
            if (entry.getValue().contains(station)) {
                dirKeys.add(entry.getKey());
            }
        }
        if (dirKeys.isEmpty()) return false;

        // 1. 检查所有方向是否均为 is_future
        boolean allFuture = true;
        for (String dirKey : dirKeys) {
            if (!dirFutureMap.getOrDefault(dirKey, false)) {
                allFuture = false;
                break;
            }
        }
        if (allFuture) return true;

        // 2. 检查该站点经过的所有线路在该站的状态是否均为 pending
        Map<String, Map<String, String>> stationLineStatusMap = cache.getStationLineStatusMap();
        if (stationLineStatusMap == null) return false;
        Map<String, String> lineStatusMap = stationLineStatusMap.get(station);
        if (lineStatusMap == null) return false;

        // 获取经过的线路ID集合（从方向键中提取线路ID）
        Set<String> lineIds = new HashSet<>();
        for (String dirKey : dirKeys) {
            String lineId = GraphKeyUtils.getLineIdFromDirKey(dirKey);
            if (lineId != null) lineIds.add(lineId);
        }
        if (lineIds.isEmpty()) return false;

        // 检查每条线路在该站的状态是否均为 pending
        for (String lineId : lineIds) {
            String status = lineStatusMap.get(lineId);
            // 如果状态不是 pending，则返回 false
            if (!"pending".equals(status)) {
                return false;
            }
        }
        return true; // 所有线路均为 pending
    }

    /**
     * 判断给定方向键是否为未来（未开通）方向。
     *
     * @param dirKey 方向键（格式：lineId|start|end）
     * @return true 表示该方向为未来方向
     * @see DataCache#getDirFutureMap()
     */
    public static boolean isFutureDirection(String dirKey) {
        DataCache cache = DataCache.getInstance();
        Map<String, Boolean> dirFutureMap = cache.getDirFutureMap();
        return !dirFutureMap.getOrDefault(dirKey, false);
    }

    /**
     * 获取“当前已开通”的站点列表
     */
    public static List<String> getActiveStationList() {
        List<String> all = getStationList();
        List<String> active = new ArrayList<>();
        for (String station : all) {
            if (!isFutureStation(station)) {
                active.add(station);
            }
        }
        return active;
    }
}
