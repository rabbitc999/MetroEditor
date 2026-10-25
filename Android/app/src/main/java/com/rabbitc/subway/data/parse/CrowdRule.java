package com.rabbitc.subway.data.parse;

import java.util.List;

/**
 * 拥挤度规则，由 {@code DataParser} 从原始 JSON 解析得到，表示某方向在特定时间段和星期内的拥挤因子。
 * 存储于 {@code DataCache} 中供运行时行程时间修正使用。
 */
public class CrowdRule {
    /** 方向标识键 */
    public String directionKey;
    /** 适用时间段的起始时间，格式为 "HH:mm" */
    public String startTime;
    /** 适用时间段的结束时间，格式为 "HH:mm" */
    public String endTime;
    /** 拥挤因子，用于修正行程时间 */
    public double factor;
    /** 适用的星期列表 */
    public List<String> days;
}
