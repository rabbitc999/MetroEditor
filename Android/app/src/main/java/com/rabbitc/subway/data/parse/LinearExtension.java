package com.rabbitc.subway.data.parse;

/**
 * 线性延伸票价规则，由 {@code DataParser} 从原始 JSON 解析得到，表示超出分段计价范围后的线性计价方式。
 * 存储于 {@code DataCache} 中供运行时票价计算使用。
 */
public class LinearExtension {
    /** 线性计价起始里程阈值（公里），超过此里程后按线性规则计价 */
    public double threshold;
    /** 线性计价基础票价（元） */
    public double baseFare;
    /** 每公里加价费率（元/公里） */
    public double ratePerKm;
}
