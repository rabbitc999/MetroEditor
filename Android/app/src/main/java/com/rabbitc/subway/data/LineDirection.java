package com.rabbitc.subway.data;

import java.util.List;

/**
 * 线路方向信息，表示某条线路的一个行驶方向。
 * <p>
 * 包含该方向的起终点、沿途站点及未来规划标记。
 * </p>
 */
public class LineDirection {

    /** 方向起点站名 */
    public String start;

    /** 方向终点站名 */
    public String end;

    /** 沿途站点列表（按顺序） */
    public List<String> stations;

    /** 是否为规划线路 */
    public boolean isFuture;

    /** 线路ID */
    public String lineID;

    /** 创建一个线路方向模型。 */
    public LineDirection(String lineID, String start, String end, List<String> stations,
                         boolean isFuture) {
        this.lineID = lineID;
        this.start = start;
        this.end = end;
        this.stations = stations;
        this.isFuture = isFuture;
    }
}
