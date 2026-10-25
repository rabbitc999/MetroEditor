package com.rabbitc.subway.data;

import java.util.List;

/**
 * 票价计算中的分段信息，表示路径中一段连续同方向的行驶区间。
 * <p>
 * 用于票价计算器 {@link com.rabbitc.subway.algroithm.FareCalculator}。
 * </p>
 */
public class FareSegmentInfo {

    /** 方向键（lineId|start|end） */
    public String dirKey;

    /** 线路ID */
    public String lineId;

    /** 该分段内的站点列表（按顺序） */
    public List<String> stations;

    /** 该分段的总距离（km） */
    public double distance;
}