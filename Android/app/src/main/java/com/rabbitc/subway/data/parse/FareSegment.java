package com.rabbitc.subway.data.parse;

// ========================= 内部数据类 =========================
/**
 * 票价分段，由 {@code DataParser} 从原始 JSON 解析得到，表示某段距离区间内的票价。
 * 存储于 {@code DataCache} 中供运行时票价计算使用。
 */
public class FareSegment {
    /** 区间起始里程（公里） */
    public double rangeStart;
    /** 区间结束里程（公里） */
    public double rangeEnd;
    /** 该区间内的票价（元） */
    public double fare;
}
