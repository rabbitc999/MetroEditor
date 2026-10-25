package com.rabbitc.subway.algroithm.graph;

import java.util.List;

/**
 * 表示路径中一个连续的方向段，包含方向键和该段内的站点列表（按顺序）。
 */
public class Segment {
    /** 连续方向的唯一键（通常包含线路和方向端点）。 */
    public String dirKey;
    /** 该方向段覆盖的站点，按乘车顺序排列。 */
    public List<String> stations;

    /** 创建一段连续方向的站点序列。 */
    public Segment(String dirKey, List<String> stations) {
        this.dirKey = dirKey;
        this.stations = stations;
    }
}
