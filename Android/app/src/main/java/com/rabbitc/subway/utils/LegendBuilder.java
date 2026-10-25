package com.rabbitc.subway.utils;

import android.graphics.Color;

import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.LineLegendItem;
import com.rabbitc.subway.data.json.Direction;
import com.rabbitc.subway.data.json.Line;
import com.rabbitc.subway.data.json.MetroData;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;

/**
 * 图例构建工具类。
 * <p>
 * 从 {@link DataCache} 中聚合线路方向、颜色、标签及里程数据，
 * 构建用于线路图例展示的 {@link LineLegendItem} 列表。
 * 支持按线路名称排序，并可过滤仅显示未来规划线路。
 * </p>
 *
 * @see DataCache
 * @see LineLegendItem
 * @see GraphKeyUtils
 * @see TextMatchUtils
 */
public class LegendBuilder {
    /**
     * 从 DataCache 构建线路图例数据。
     *
     * @param cache DataCache 实例
     * @return 图例项列表
     */
    public static List<LineLegendItem> buildLegendItems(DataCache cache, boolean exisitFutureOnly) {
        Map<String, List<String>> dirStations = cache.getDirStations();
        Map<String, String> lineNameMap = cache.getLineNameMap();
        Map<String, String> lineCityMap = cache.getLineCityMap();
        Map<String, String> lineColorMap = cache.getLineColorMap();
        String keyCity = cache.getKeyCity();

        // 按 lineId 分组所有方向键
        Map<String, Set<String>> lineIdToDirKeys = new HashMap<>();
        if (dirStations != null) {
            for (String dirKey : dirStations.keySet()) {
                String lineId = GraphKeyUtils.getLineIdFromDirKey(dirKey);
                lineIdToDirKeys.computeIfAbsent(lineId, k -> new HashSet<>()).add(dirKey);
            }
        }

        List<LineLegendItem> items = new ArrayList<>();
        for (Map.Entry<String, Set<String>> entry : lineIdToDirKeys.entrySet()) {
            String lineId = entry.getKey();

            // 收集标签
            List<String> tagsList = new ArrayList<>();
            Boolean isSpecial = cache.getLineSpecialMap() != null ? cache.getLineSpecialMap().get(lineId) : Boolean.FALSE;
            Boolean hasLoop = cache.getHasLoopMap() != null ? cache.getHasLoopMap().get(lineId) : Boolean.FALSE;
            Boolean isSegmented = cache.getLineSegmentedMap() != null ? cache.getLineSegmentedMap().get(lineId) : Boolean.FALSE;
            Boolean hasFast = cache.getLineHasFastMap() != null ? cache.getLineHasFastMap().get(lineId) : Boolean.FALSE;
            Boolean hasFuture = cache.getHasFutureMap() != null ? cache.getHasFutureMap().get(lineId) : Boolean.FALSE;

            // 确定显示名称
            String displayName = lineNameMap != null ? lineNameMap.get(lineId) : null;
            if (displayName == null || displayName.isEmpty()) {
                String city = lineCityMap != null ? lineCityMap.get(lineId) : "";
                String lineName = lineNameMap != null ? lineNameMap.get(lineId) : lineId;
                displayName = (city != null && !city.equals(keyCity)) ? city + lineName : lineName;
            }

            // 收集颜色
            Set<Integer> colorSet = new LinkedHashSet<>();
            String colorStr = lineColorMap != null ? lineColorMap.get(lineId) : null;
            int color = colorStr != null ? Color.parseColor(colorStr) : Color.parseColor("#999999");
            colorSet.add(color);
            List<Integer> colors = new ArrayList<>(colorSet);

            if (Boolean.TRUE.equals(isSpecial)) tagsList.add("单独计费");
            if (Boolean.TRUE.equals(hasLoop)) tagsList.add("环线");
            if (Boolean.TRUE.equals(isSegmented)) tagsList.add("分段运营");
            if (Boolean.TRUE.equals(hasFast)) tagsList.add("有快车");

            // 检测是否有小交路（非快车、非未来方向中站点数少于最大方向站点数）
            MetroData metroData = cache.getMetroData();
            if (metroData != null && metroData.lines != null) {
                Line lineInfo = metroData.lines.get(lineId);
                if (lineInfo != null && lineInfo.direction != null) {
                    int maxStn = 0;
                    for (Direction dir : lineInfo.direction) {
                        if (dir.isFast || dir.isFuture) continue;
                        int c = dir.station != null ? dir.station.size() : 0;
                        if (c > maxStn) maxStn = c;
                    }
                    for (Direction dir : lineInfo.direction) {
                        if (dir.isFast || dir.isFuture) continue;
                        int c = dir.station != null ? dir.station.size() : 0;
                        if (c < maxStn && c > 0) {
                            tagsList.add("有交路");
                            break;
                        }
                    }
                }
            }

            Double curLen = cache.getLineTotalLengthMapWithoutFuture().get(lineId);
            Double FutureLen = cache.getLineTotalLengthMap() != null ? cache.getLineTotalLengthMap().get(lineId) : null;
            String lengthStr = FutureLen != null ? String.format(Locale.CHINA, "已开通 %.1f km", FutureLen) : "";

            if (hasFuture) {
                if (curLen != null && curLen > 1e-3) {
                    lengthStr = String.format(
                            Locale.CHINA,
                            "已开通 %.1f km · 规划 %.1f km",
                            curLen,
                            FutureLen // 你原来的 lengthStr 这里换成 futureLen
                    );
                } else {
                    lengthStr = String.format(
                            Locale.CHINA,
                            "规划 %.1f km",
                            FutureLen
                    );
                }
            }

            if (exisitFutureOnly || curLen > 1e-3)
            {
                items.add(new LineLegendItem(lineId, displayName, colors, tagsList, lengthStr));
            }
        }

        // 按显示名称排序
        items.sort((a, b) -> TextMatchUtils.compareLineName(a.lineName, b.lineName));
        return items;
    }
}
