package com.rabbitc.subway.data;

import java.util.List;

/**
 * 线路图例项，用于在地图或线路列表中显示线路信息。
 * <p>
 * 包含线路ID、显示名称、颜色、标签和长度。
 * </p>
 */
public class LineLegendItem {

    /** 线路ID（如 "line_1"） */
    public String lineId;

    /** 线路显示名称（如 "1号线"） */
    public String lineName;

    /** 线路颜色列表（支持渐变色线路） */
    public List<Integer> colors;

    /** 线路标签列表（如 "环线"、"有快车"） */
    public List<String> tags;

    /** 线路长度字符串（如 "32.5 km"） */
    public String lengthStr;

    /** 创建一个线路图例项。 */
    public LineLegendItem(String lineId, String lineName, List<Integer> colors,
                          List<String> tags, String lengthStr) {
        this.lineId = lineId;
        this.lineName = lineName;
        this.colors = colors;
        this.tags = tags;
        this.lengthStr = lengthStr;
    }
}
