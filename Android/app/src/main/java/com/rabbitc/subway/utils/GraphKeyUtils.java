package com.rabbitc.subway.utils;

import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.json.Direction;
import com.rabbitc.subway.data.json.Line;
import com.rabbitc.subway.data.json.MetroData;

import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * 图论键工具类。
 * <p>
 * 负责地铁线路图中有向边（方向键）和节点的构造与解析。
 * 方向键格式为 {@code lineId|start|end}，节点格式为 {@code dirKey|station}。
 * 提供键的生成、拆分、线路 ID 提取、方向同异性判断、换乘次数计算
 * 及重复方向检测等方法，供路径规划与图构建使用。
 * </p>
 *
 * @see DataCache
 */
public class GraphKeyUtils {
    /**
     * 构造方向键：lineId|start|end。
     *
     * @param lineId 线路ID
     * @param start  起点站名
     * @param end    终点站名
     * @return 方向键字符串
     */
    public static String makeDirKey(String lineId, String start, String end) {
        return lineId + "|" + start + "|" + end;
    }

    /**
     * 解析方向键，返回 [lineId, start, end]。
     *
     * @param dirKey 方向键
     * @return 字符串数组
     */
    public static String[] parseDirKey(String dirKey) {
        // 方向键的三个字段均由固定分隔符连接，线路 ID 和站名本身不含 '|'.
        return dirKey.split("\\|");
    }

    /**
     * 构造节点：dirKey|station。
     *
     * @param dirKey  方向键
     * @param station 站点名称
     * @return 节点字符串
     */
    public static String makeNode(String dirKey, String station) {
        return dirKey + "|" + station;
    }

    /**
     * 解析节点，返回 [dirKey, station]。
     * 使用 lastIndexOf 提高性能。
     *
     * @param node 节点字符串
     * @return 字符串数组 [dirKey, station]
     */
    public static String[] parseNode(String node) {
        // 站名可能包含分隔符样式字符，因此从末尾切分才能保留完整方向键。
        int idx = node.lastIndexOf('|');
        return new String[]{node.substring(0, idx), node.substring(idx + 1)};
    }

    /**
     * 从方向键中提取线路ID。
     *
     * @param dirKey 方向键
     * @return 线路ID，若解析失败返回 null
     */
    public static String getLineIdFromDirKey(String dirKey) {
        String[] parts = parseDirKey(dirKey);
        return parts.length > 0 ? parts[0] : null;
    }

    /**
     * 判断两个方向键是否表示同一线路的同一方向（包含正向和反向）。
     *
     * @param dir1 方向键1
     * @param dir2 方向键2
     * @return true 如果属于同一线路且方向相同或相反
     */
    public static boolean isEndEqual(String dir1, String dir2) {
        DataCache cache = DataCache.getInstance();
        String lineId1 = GraphKeyUtils.getLineIdFromDirKey(dir1);
        String lineId2 = GraphKeyUtils.getLineIdFromDirKey(dir2);
        if (!lineId1.equals(lineId2)) return false;

        MetroData metroData = cache.getMetroData();
        if (metroData == null || metroData.lines == null) return false;
        Line line = metroData.lines.get(lineId1);
        if (line == null) return false;
        List<Direction> dirList = line.direction;
        if (dirList == null) dirList = Collections.emptyList();

        String[] parts1 = parseDirKey(dir1);
        String[] parts2 = parseDirKey(dir2);
        if (parts1.length < 3 || parts2.length < 3) return false;
        String start1 = parts1[1], end1 = parts1[2];
        String start2 = parts2[1], end2 = parts2[2];
        boolean loop1 = false, loop2 = false;

        /* 环线没有确定的终点 */
        for (Direction dir : dirList) // 改为 Direction
        {
            boolean isLoop = dir.isLoop;
            String start = dir.start;
            String end = dir.end;
            if (start1.equals(start) && end1.equals(end) && isLoop) loop1 = true;
            if (start2.equals(start) && end2.equals(end) && isLoop) loop2 = true;
        }
        if (loop1 && loop2) return true;

        boolean isSameDir = end1.equals(end2);
        boolean isRevDir = start1.equals(end2) || start2.equals(end1);
        return isSameDir || isRevDir;
    }

    /**
     * 计算路径中的换乘次数（方向变化的次数）。
     *
     * @param path 路径节点列表
     * @return 换乘次数
     */
    public static int countTransfers(List<String> path) {
        // 图节点携带方向键，方向变化即可视为一次换乘（同站不同方向也适用）。
        int transfers = 0;
        for (int i = 0; i < path.size() - 1; i++) {
            String dir1 = parseNode(path.get(i))[0];
            String dir2 = parseNode(path.get(i + 1))[0];
            if (!dir1.equals(dir2)) transfers++;
        }
        return transfers;
    }

    /**
     * 判断两个站点列表是否为重复方向。
     * <p>
     * 依次检查以下四种情况，任一命中即视为重复：
     * <ol>
     *   <li>完全相同（同序）</li>
     *   <li>互为逆序（正向 ↔ 反向）</li>
     *   <li>站点集合相同（顺序不同但站点完全一致，含环线旋转情况）</li>
     *   <li>子集关系（一个方向的站点完全包含在另一个方向中）</li>
     * </ol>
     *
     * @param a      列表1
     * @param b      列表2
     * @param isLoop 是否为环线（保留参数以兼容调用方，实际不影响判断逻辑）
     * @return true 如果二者属于重复方向
     */
    public static boolean isDuplicateDirection(List<String> a, List<String> b, boolean isLoop) {
        if (a.isEmpty() || b.isEmpty()) return false;

        // 1. 完全相同（同序）
        if (a.equals(b)) return true;

        // 2. 互为逆序
        if (a.size() == b.size()) {
            boolean isReverse = true;
            for (int i = 0; i < a.size(); i++) {
                if (!a.get(i).equals(b.get(a.size() - 1 - i))) {
                    isReverse = false;
                    break;
                }
            }
            if (isReverse) return true;
        }

        // 3. 站点集合相同（不同顺序但站点完全一致，含环线旋转）
        Set<String> setA = new HashSet<>(a);
        Set<String> setB = new HashSet<>(b);
        if (setA.equals(setB)) return true;

        // 4. 子集关系：一个方向的站点完全包含在另一个方向中
        if (setA.containsAll(setB) || setB.containsAll(setA)) return true;

        return false;
    }
}
