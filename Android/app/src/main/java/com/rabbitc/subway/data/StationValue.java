package com.rabbitc.subway.data;

/**
 * 批量查询结果的数据封装，用于在列表视图中展示每个站点的综合指标。
 * <p>
 * 包含从起点到该站点的距离、换乘次数、综合价值、票价、时间等信息。
 * </p>
 */
public class StationValue {

    /** 站点名称 */
    public String station;

    /** 从起点到该站点的距离（km） */
    public double distance;

    /** 从起点到该站点的换乘次数 */
    public int transfers;

    /** 综合价值评分（数值越小越优） */
    public double value;

    /** 乘车路线描述（如 "乘坐1号线 → 换乘2号线"） */
    public String routeDesc;

    /** 总票价（元） */
    public double fare;

    /** 特殊线路附加费（元） */
    public double specialFare;

    /** 总时间（分钟） */
    public double time;

    /** 经过的站点总数（含起点和终点） */
    public int stationCount;

    /** 创建一个批量查询结果项。 */
    public StationValue(String station, double distance, int transfers, double value,
                        String routeDesc, double fare, double specialFare,
                        double time, int stationCount) {
        this.station = station;
        this.distance = distance;
        this.transfers = transfers;
        this.value = value;
        this.routeDesc = routeDesc;
        this.fare = fare;
        this.specialFare = specialFare;
        this.time = time;
        this.stationCount = stationCount;
    }
}
