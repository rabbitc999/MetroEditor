package com.rabbitc.subway.data.parse;

/**
 * 区间附加费，由 {@code DataParser} 从原始 JSON 解析得到，表示两个节点之间的额外费用。
 * 存储于 {@code DataCache} 中供运行时票价计算使用。
 */
public class Surcharge {
    /** 起始节点标识 */
    public String node_a;
    /** 结束节点标识 */
    public String node_b;
    /** 附加费金额（元） */
    public double price;
}
