package com.rabbitc.subway.data.parse;

/**
 * 区间限速规则，由 {@code DataParser} 从原始 JSON 解析得到，表示某方向上两个车站之间的列车限速。
 * 存储于 {@code DataCache} 中供运行时行程时间计算使用。
 */
public class SpeedLimit {
    /** 方向标识键 */
    public String directionKey;
    /** 限速区间起始车站名称 */
    public String fromStation;
    /** 限速区间结束车站名称 */
    public String toStation;
    /** 限速值（公里/小时） */
    public int speedKmh;
}
