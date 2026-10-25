package com.rabbitc.subway.data;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;

/**
 * 历史行程记录 — 记录一次路线查询的完整信息。
 *
 * <p>在 MainActivity 路线规划成功后自动保存，可在 TripHistoryActivity 中查看。</p>
 *
 * @see com.rabbitc.subway.utils.TripHistoryManager
 */
public class TripHistory implements Serializable {
    private static final long serialVersionUID = 1L;
    /** 城市名称 */
    public String city;
    /** 起点站 */
    public String startStation;
    /** 终点站 */
    public String endStation;
    /** 查询时间戳（毫秒） */
    public long timestamp;
    /** 总里程（公里） */
    public double distanceKm;
    /** 换乘次数 */
    public int transfers;
    /** 总票价（元） */
    public double fare;
    /** 总耗时（分钟） */
    public double timeMinutes;
    /** 途经站点序列（含换乘路径中的所有站名） */
    public List<String> stations;
    /** 完整路径节点（格式为 "dirKey|station"） */
    public List<String> pathNodes;
    /** 路径分段列表 */
    public List<RouteSegment> segments;
    /** 特殊线路票价（元） */
    public double specialMoney;
    /** 乘坐站数 */
    public int stationCount;
    /** 查询模式（如 "最短时间" / "最少换乘" / "最不拥挤"） */
    public String mode;
    /** 是否需要在起点或终点补充步行提示。 */
    public boolean walkSuggestion;

    /**
     * 创建行程记录。
     */
    public TripHistory() {
        city = "";
        stations = new ArrayList<>();
        pathNodes = new ArrayList<>();
        segments = new ArrayList<>();
    }

    /**
     * 创建行程记录。
     *
     * @param startStation 起点站
     * @param endStation   终点站
     * @param timestamp    查询时间戳（毫秒）
     */
    public TripHistory(String startStation, String endStation, long timestamp) {
        this.startStation = startStation;
        this.endStation = endStation;
        this.city = "";
        this.timestamp = timestamp;
        this.stations = new ArrayList<>();
        this.pathNodes = new ArrayList<>();
        this.segments = new ArrayList<>();
    }

    /**
     * 获取行程方向的显示文本（"A → B"）。
     */
    public String getRouteText() {
        return startStation + " → " + endStation;
    }

    /**
     * 获取耗时显示文本。
     */
    public String getTimeText() {
        return String.format("%.0f分钟", timeMinutes);
    }

    /**
     * 获取票价显示文本。
     */
    public String getFareText() {
        return String.format("%.0f元", fare);
    }

    /**
     * 判断两条行程是否为同一条（起终点 + 时间戳相同）。
     *
     * <p>用于双向同步合并时去重。</p>
     */
    public boolean sameAs(TripHistory other) {
        return other != null
                && timestamp == other.timestamp
                && startStation != null && startStation.equals(other.startStation)
                && endStation != null && endStation.equals(other.endStation);
    }
}
