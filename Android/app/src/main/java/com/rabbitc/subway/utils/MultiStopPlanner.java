package com.rabbitc.subway.utils;

import com.rabbitc.subway.data.DataCache;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * 多站点打卡路径排序：先按直线距离贪心，再做 2-opt 局部交换优化。
 */
public final class MultiStopPlanner {

    private MultiStopPlanner() {}

    /** 使用贪心排序和 2-opt 优化生成多站点访问顺序。 */
    public static List<String> planOrder(String start, String end, List<String> points) {
        // 先固定起点，再从未访问站点中选择最近者；这样可在站点较多时保持较低计算量。
        Map<String, double[]> coords = DataCache.getInstance().getStationCoords();
        List<String> ordered = new ArrayList<>();
        ordered.add(start);

        List<String> remaining = new ArrayList<>(points);
        Set<String> used = new HashSet<>();
        used.add(start);

        String current = start;
        while (!remaining.isEmpty()) {
            String next = null;
            double best = Double.MAX_VALUE;
            for (String candidate : remaining) {
                if (used.contains(candidate)) continue;
                double d = distance(coords, current, candidate);
                if (d < best) {
                    best = d;
                    next = candidate;
                }
            }
            if (next == null) break;
            ordered.add(next);
            used.add(next);
            remaining.remove(next);
            current = next;
        }

        ordered.add(end);
        // 贪心结果容易留下局部绕路，2-opt 只交换相邻中间点并在有收益时保留交换。
        optimize2Opt(ordered, coords);
        return ordered;
    }

    private static void optimize2Opt(List<String> order, Map<String, double[]> coords) {
        // 持续扫描直到一轮没有改进，保证每次交换都不会增加当前两条边的直线距离。
        boolean improved = true;
        while (improved) {
            improved = false;
            for (int i = 1; i < order.size() - 2; i++) {
                double before = segmentDistance(order, i - 1, i, coords)
                        + segmentDistance(order, i, i + 1, coords);
                swap(order, i, i + 1);
                double after = segmentDistance(order, i - 1, i, coords)
                        + segmentDistance(order, i, i + 1, coords);
                if (after < before) {
                    improved = true;
                } else {
                    swap(order, i, i + 1);
                }
            }
        }
    }

    private static double segmentDistance(List<String> order, int a, int b, Map<String, double[]> coords) {
        return distance(coords, order.get(a), order.get(b));
    }

    private static double distance(Map<String, double[]> coords, String a, String b) {
        // 任一站点缺少坐标时返回最大值，使其不会被贪心阶段优先选中。
        if (a == null || b == null || coords == null) return Double.MAX_VALUE;
        double[] ca = coords.get(a);
        double[] cb = coords.get(b);
        if (ca == null || cb == null) return Double.MAX_VALUE;
        return CoordinateUtils.haversineDistance(ca[0], ca[1], cb[0], cb[1]);
    }

    private static void swap(List<String> list, int a, int b) {
        String tmp = list.get(a);
        list.set(a, list.get(b));
        list.set(b, tmp);
    }
}
