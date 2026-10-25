package com.rabbitc.subway.data;

import java.io.Serializable;

/**
 * 行程分段信息，用于展示路径中每一段的具体细节。
 * <p>
 * 由 {@link com.rabbitc.subway.algroithm.MetroGraph#getRouteSegments(List)} 构建。
 * </p>
 */
public class RouteSegment implements Serializable {
    private static final long serialVersionUID = 1L;

    /** 该段起点站名 */
    public String startStation;

    /** 线路ID */
    public String lineId;

    /** 线路显示名称 */
    public String lineName;

    /** 行驶方向描述（如 "番禺广场" 或 "内环"） */
    public String direction;

    /** 该段终点站名 */
    public String endStation;

    /** 该段内经过的站点数（区间数） */
    public int stationCount;

    /** 方向键（lineId|start|end） */
    public String dirKey;

    /** 该段起点是否需要出站换乘（仅对非首段有效） */
    public boolean isOutStationTransfer;

    /** 创建一段已完成方向和站点聚合的路线展示模型。 */
    public RouteSegment(String startStation, String lineId, String lineName,
                        String direction, String endStation, int stationCount,
                        boolean isOutStationTransfer, String dirKey) {
        this.startStation = startStation;
        this.lineId = lineId;
        this.lineName = lineName;
        this.direction = direction;
        this.endStation = endStation;
        this.stationCount = stationCount;
        this.isOutStationTransfer = isOutStationTransfer;
        this.dirKey = dirKey;
    }
}
