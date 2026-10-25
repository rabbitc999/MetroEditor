package com.rabbitc.subway.data.json;

/**
 * 区间附加费模型，定义两个站点之间附加的票价。
 * 用于特殊线路中特定区间段除基础票价外的额外收费。
 */
public class Surcharge {

    /** 区间起始站点标识符 */
    public String node_a;

    /** 区间终止站点标识符 */
    public String node_b;

    /** 该区间的附加费用金额 */
    public double price;
}
