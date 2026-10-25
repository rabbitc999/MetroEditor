package com.rabbitc.subway.data;

/**
 * 换乘线路信息，用于显示某站点可换乘的其他线路。
 * <p>
 * 包含线路ID、显示名称、颜色及换乘状态。
 * </p>
 */
public class TransferLineInfo {

    /** 线路ID（如 "line_1"） */
    public String lineId;

    /** 线路显示名称（如 "1号线" 或 "机场线"） */
    public String displayName;

    /** 线路颜色（ARGB 值） */
    public int color;

    /**
     * 换乘状态，取值：
     * <ul>
     *     <li>"operating" - 正常换乘</li>
     *     <li>"pending" - 暂缓开通</li>
     *     <li>"under_construction" - 建设中</li>
     *     <li>"planned" - 规划中</li>
     *     <li>"out_station" - 出站换乘</li>
     * </ul>
     */
    public String status;

    /** 创建一个用于站点详情展示的换乘线路对象。 */
    public TransferLineInfo(String lineId, String displayName, int color, String status) {
        this.lineId = lineId;
        this.displayName = displayName;
        this.color = color;
        this.status = status;
    }
}
