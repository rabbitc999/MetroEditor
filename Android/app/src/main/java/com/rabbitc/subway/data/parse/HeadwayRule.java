package com.rabbitc.subway.data.parse;

import java.util.List;

/**
 * 列车发车间隔规则，由 {@code DataParser} 从原始 JSON 解析得到，表示某条线路在特定时间段和星期内的列车发车间隔。
 * 存储于 {@code DataCache} 中供运行时刻表计算使用。
 */
public class HeadwayRule {
    /** 线路标识 */
    public String lineId;
    /** 适用时间段的起始时间，格式为 "HH:mm" */
    public String startTime;
    /** 适用时间段的结束时间，格式为 "HH:mm" */
    public String endTime;
    /** 列车发车间隔（秒） */
    public int intervalSeconds;
    /** 适用的星期列表，如 ["Monday", "Tuesday"] */
    public List<String> days;
}
