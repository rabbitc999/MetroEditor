package com.rabbitc.subway.algroithm;

import android.util.Pair;

import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.FareSegmentDetail;
import com.rabbitc.subway.data.FareSegmentInfo;
import com.rabbitc.subway.data.parse.SpecialLineRule;
import com.rabbitc.subway.data.parse.Surcharge;
import com.rabbitc.subway.data.parse.FareSegment;
import com.rabbitc.subway.data.parse.LinearExtension;
import com.rabbitc.subway.utils.GraphKeyUtils;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;

/**
 * 票价计算工具类，支持普通线路、特殊线路（固定票价、附加费）的计价。
 * 新增支持完整票价配置（defaultFare + segments + linear_extension）、线路组（use_lines）。
 * 提供分段明细计算功能。
 */
public class FareCalculator {

    /**
     * 分析路径中所有普通段（排除特殊线路），返回每段的明细。
     * 此方法为旧版兼容，实际使用已内部重构。
     *
     * @param path       完整路径节点列表（来自 PathResult.path）
     * @param metroGraph MetroGraph 实例（用于获取距离和线路信息）
     * @return 普通段明细列表（如果路径为空或仅包含特殊段，则返回空列表）
     * @deprecated 推荐使用 {@link #calculateFareDetails(List, MetroGraph, boolean)}
     */
    @Deprecated
    public static Pair<Double, Double> calculateFareWithSpecial(List<String> path, MetroGraph metroGraph) {
        if (path == null || path.size() < 2) {
            DataCache cache = DataCache.getInstance();
            return new Pair<>(cache.getDefaultFare(), 0.0);
        }
        List<FareSegmentDetail> details = calculateFareDetails(path, metroGraph, false);
        double total = 0.0, special = 0.0;
        for (FareSegmentDetail d : details) {
            total += d.totalFare;
            if (d.isSpecial) {
                special += d.totalFare;
            } else {
                special += d.surcharge;
            }
        }
        return new Pair<>(total, special);
    }

    /**
     * 计算路径的总票价，排除所有特殊线路（即全部按普通计价）。
     *
     * @param path       路径节点列表
     * @param metroGraph MetroGraph 实例
     * @return Pair<总票价, 附加费>（附加费为特殊线路的额外费用，这里恒为0）
     */
    public static Pair<Double, Double> calculateFareWithoutSpecial(List<String> path, MetroGraph metroGraph) {
        if (path == null || path.size() < 2) {
            DataCache cache = DataCache.getInstance();
            return new Pair<>(cache.getDefaultFare(), 0.0);
        }
        List<FareSegmentDetail> details = calculateFareDetails(path, metroGraph, true);
        double total = 0.0;
        for (FareSegmentDetail d : details) {
            total += d.totalFare;
        }
        return new Pair<>(total, 0.0);
    }

    /**
     * 计算路径的票价分段详情（简化重构版）
     *
     * @param path          路径节点列表
     * @param metroGraph    地铁图
     * @param excludeSpecial 是否排除特殊线路（全部按普通计价）
     * @return 票价分段详情列表
     */
    public static List<FareSegmentDetail> calculateFareDetails(List<String> path,
                                                               MetroGraph metroGraph,
                                                               boolean excludeSpecial) {
        if (path == null || path.size() < 2) {
            return Collections.emptyList();
        }

        // 先按连续方向拆段；普通线路按累计距离计价，特殊线路独立结算。
        // 先拆分为方向连续段；换乘点会成为下一段的起点。
        List<FareSegmentInfo> segments = buildSegments(path, metroGraph);
        if (segments.isEmpty()) {
            return Collections.emptyList();
        }

        DataCache cache = DataCache.getInstance();
        Map<String, String> lineNameMap = cache.getLineNameMap();

        // 普通段累积器
        NormalAccumulator normal = new NormalAccumulator();
        List<FareSegmentDetail> details = new ArrayList<>();

        // 逐段处理，normal 累积器负责合并相邻普通段，减少碎片化明细。
        for (FareSegmentInfo seg : segments) {
            if (excludeSpecial) {
                // 全部按普通段累加
                normal.accumulate(seg);
                continue;
            }

            SpecialLineRule rule = findSpecialRule(seg.lineId);
            if (rule != null) {
                // 存在特殊规则，判断类型
                // 完整特殊票价会截断普通段累加，确保同一路径中的两种计价规则互不污染。
                if (hasCompleteFareConfig(rule)) {
                    // 完整票价配置或统一票价 → 结算普通段，单独处理特殊段
                    details.addAll(normal.flush(lineNameMap));
                    details.add(createSpecialDetail(seg, getSpecialFare(seg, rule), 0, lineNameMap));
                    continue;
                }

                // 只有附加费的线路仍按普通里程计价，再额外生成一条附加费明细。
                if (rule.surcharge != null && !rule.surcharge.isEmpty()) {
                    // 区间附加费 → 普通段继续累加，但单独记录特殊段的附加费
                    normal.accumulate(seg);
                    details.add(createSpecialDetail(seg, 0, calcSurchargeForSegment(seg, rule), lineNameMap));
                    continue;
                }
            }

            // 无特殊规则或未匹配：继续合并到当前普通段。
            normal.accumulate(seg);
        }

        // 结算最后剩余的普通段
        details.addAll(normal.flush(lineNameMap));
        return details;
    }

    // ---------- 辅助方法（原已存在，此处仅示意） ----------
    private static boolean hasCompleteFareConfig(SpecialLineRule rule) {
        return rule.defaultFare != null
                || (rule.segments != null && !rule.segments.isEmpty())
                || rule.all != null;
    }

    private static double getSpecialFare(FareSegmentInfo seg, SpecialLineRule rule) {
        if (rule.defaultFare != null || (rule.segments != null && !rule.segments.isEmpty())) {
            return calcFareFromSpecialConfig(seg.distance, rule);
        }
        if (rule.all != null) {
            return rule.all;
        }
        return 0.0; // 不会走到这里
    }

    // ---------- 辅助内部类：普通段累积器 ----------
    private static class NormalAccumulator {
        private double totalDist = 0.0;
        private double totalSurcharge = 0.0;
        private String lineId = null;
        private String start = null;
        private String end = null;

        /** 累积一个普通段 */
        void accumulate(FareSegmentInfo seg) {
            totalDist += seg.distance;
            if (lineId == null) {
                lineId = seg.lineId;
                start = seg.stations.get(0);
                end = seg.stations.get(seg.stations.size() - 1);
            } else {
                end = seg.stations.get(seg.stations.size() - 1);
            }
        }

        /** 结算当前累积的普通段，清空状态 */
        List<FareSegmentDetail> flush(Map<String, String> lineNameMap) {
            if (lineId == null) {  // 无累积
                return Collections.emptyList();
            }
            List<FareSegmentDetail> result = finishNormalSegment(
                    lineId, start, end, totalDist, totalSurcharge, lineNameMap);
            // 重置
            totalDist = 0.0;
            totalSurcharge = 0.0;
            lineId = null;
            start = null;
            end = null;
            return result;
        }
    }

    // ---------- 辅助方法：结算普通段 ----------
    private static List<FareSegmentDetail> finishNormalSegment(String lineId, String start, String end,
                                                               double dist, double surcharge,
                                                               Map<String, String> lineNameMap) {
        List<FareSegmentDetail> list = new ArrayList<>();
        if (dist <= 0 || lineId == null) return list;

        double baseFare = calcBaseFare(dist);
        double total = baseFare + surcharge;
        String displayName = lineNameMap.getOrDefault(lineId, lineId);

        FareSegmentDetail detail = new FareSegmentDetail();
        detail.lineId = lineId;
        detail.lineDisplayName = displayName;
        detail.startStation = start;
        detail.endStation = end;
        detail.stations = null;   // 普通段不保留中间站
        detail.distance = dist;
        detail.isSpecial = false;
        detail.baseFare = baseFare;
        detail.surcharge = surcharge;
        detail.totalFare = total;
        list.add(detail);
        return list;
    }

    // ---------- 辅助方法：创建特殊段明细 ----------
    private static FareSegmentDetail createSpecialDetail(FareSegmentInfo seg, double fare,
                                                         double surcharge,
                                                         Map<String, String> lineNameMap) {
        FareSegmentDetail detail = new FareSegmentDetail();
        detail.lineId = seg.lineId;
        detail.lineDisplayName = lineNameMap.getOrDefault(seg.lineId, seg.lineId);
        detail.startStation = seg.stations.get(0);
        detail.endStation = seg.stations.get(seg.stations.size() - 1);
        detail.stations = seg.stations;
        detail.distance = seg.distance;
        detail.isSpecial = true;
        detail.baseFare = fare;
        detail.surcharge = surcharge;
        detail.totalFare = fare + surcharge;
        return detail;
    }

    // ---------- 票价累加器（内部类） ----------
    static class FareAccumulator {
        double totalDistance = 0.0;
        double specialFare = 0.0;
        double specialSurcharge = 0.0;
    }

    // 此方法保留但不直接用于分段明细，原功能由 calculateFareDetails 替代
    static FareAccumulator accumulateFare(List<FareSegmentInfo> segments) {
        FareAccumulator acc = new FareAccumulator();
        for (FareSegmentInfo seg : segments) {
            SpecialLineRule rule = findSpecialRule(seg.lineId);
            if (rule != null) {
                // 完整票价配置
                if (rule.defaultFare != null || (rule.segments != null && !rule.segments.isEmpty())) {
                    double fare = calcFareFromSpecialConfig(seg.distance, rule);
                    acc.specialFare += fare;
                    continue;
                }
                // all
                if (rule.all != null) {
                    acc.specialFare += rule.all;
                    continue;
                }
                // surcharge
                if (rule.surcharge != null && !rule.surcharge.isEmpty()) {
                    acc.specialSurcharge += calcSurchargeForSegment(seg, rule);
                    acc.totalDistance += seg.distance;
                    continue;
                }
            }
            // 普通线路
            acc.totalDistance += seg.distance;
        }
        return acc;
    }

    /**
     * 计算单个分段是否触发附加费，若有则返回附加费金额，否则返回 0。
     */
    private static double calcSurchargeForSegment(FareSegmentInfo seg, SpecialLineRule rule) {
        double maxPrice = 0.0;
        for (Surcharge sur : rule.surcharge) {
            if (seg.stations.get(0).equals(sur.node_a) && seg.stations.get(seg.stations.size() - 1).equals(sur.node_b)) {
                return sur.price;
            }
            if (maxPrice < sur.price) { maxPrice = sur.price; }
        }
        return maxPrice;
    }

    // ---------- 分段构建 ----------
    /**
     * 将路径按方向连续分为若干段，每段包含线路ID、站点列表和总距离。
     */
    public static List<FareSegmentInfo> buildSegments(List<String> path, MetroGraph metroGraph) {
        List<FareSegmentInfo> segments = new ArrayList<>();
        String curDirKey = null;
        List<String> curStations = new ArrayList<>();

        for (String node : path) {
            String[] parts = GraphKeyUtils.parseNode(node);
            String dirKey = parts[0];
            String station = parts[1];

            if (curDirKey == null) {
                curDirKey = dirKey;
                curStations.add(station);
            } else if (curDirKey.equals(dirKey)) {
                curStations.add(station);
            } else {
                if (!curStations.isEmpty()) {
                    segments.add(createSegment(curDirKey, curStations, metroGraph));
                }
                curDirKey = dirKey;
                curStations = new ArrayList<>();
                curStations.add(station);
            }
        }
        if (!curStations.isEmpty()) {
            segments.add(createSegment(curDirKey, curStations, metroGraph));
        }
        return segments;
    }

    private static FareSegmentInfo createSegment(String dirKey, List<String> stations, MetroGraph metroGraph) {
        FareSegmentInfo seg = new FareSegmentInfo();
        seg.dirKey = dirKey;
        seg.stations = stations;
        seg.lineId = GraphKeyUtils.getLineIdFromDirKey(dirKey);

        double dist = 0.0;
        for (int i = 0; i < stations.size() - 1; i++) {
            double d = metroGraph.getDistanceInDirection(dirKey, stations.get(i), stations.get(i + 1));
            if (d > 0) dist += d;
        }
        seg.distance = dist;
        return seg;
    }

    // ---------- 基础票价计算 ----------
    /**
     * 根据总距离（km）计算基础票价（元）。
     * 使用阶梯规则或线性扩展，均从 DataCache 中获取。
     */
    public static double calcBaseFare(double distance) {
        if (distance <= 0) return 0.0;

        DataCache cache = DataCache.getInstance();

        // 1. 检查是否落在阶梯区间内
        List<FareSegment> segments = cache.getFareSegments();
        if (segments != null) {
            for (FareSegment fs : segments) {
                if (distance >= fs.rangeStart && distance < fs.rangeEnd) {
                    return fs.fare;
                }
            }
        }

        // 2. 检查是否超过阈值，使用线性扩展
        LinearExtension le = cache.getLinearExtension();
        if (le != null && distance >= le.threshold) {
            return le.baseFare + (distance - le.threshold) * le.ratePerKm;
        }

        // 3. 默认票价
        return cache.getDefaultFare();
    }

    /**
     * 根据 SpecialLineRule 中的完整票价配置计算票价。
     * @param distance 总距离（km）
     * @param rule 特殊规则
     * @return 票价（元）
     */
    private static double calcFareFromSpecialConfig(double distance, SpecialLineRule rule) {
        if (distance <= 0) return 0;
        double startPrice = rule.defaultFare != null ? rule.defaultFare : 2.0;
        List<FareSegment> segments = rule.segments;
        if (segments != null && !segments.isEmpty()) {
            double maxFare = 0;
            for (FareSegment fs : segments) {
                if (distance >= fs.rangeStart && distance < fs.rangeEnd) {
                    return fs.fare;
                }
                if (fs.fare > maxFare) {
                    maxFare = fs.fare;
                }
            }
            // 没有匹配到任何分段，按最高票价计算
            return maxFare;
        }
        // 无分段，使用 defaultFare 作为固定票价
        return startPrice;
    }

    // ---------- 特殊线路规则查找 ----------
    /**
     * 根据线路ID查找特殊计价规则，若不存在则返回 null。
     * 优先匹配 useLines 线路组，其次匹配 lineId。
     */
    private static SpecialLineRule findSpecialRule(String lineId) {
        DataCache cache = DataCache.getInstance();
        List<SpecialLineRule> rules = cache.getSpecialLineRules();
        if (rules == null) return null;
        // 优先匹配 useLines
        for (SpecialLineRule r : rules) {
            if (r.useLines != null && r.useLines.contains(lineId)) {
                return r;
            }
        }
        // 回退到 lineId 精确匹配
        for (SpecialLineRule r : rules) {
            if (lineId.equals(r.lineId)) {
                return r;
            }
        }
        return null;
    }
}
