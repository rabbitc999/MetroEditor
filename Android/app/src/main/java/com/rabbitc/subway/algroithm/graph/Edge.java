package com.rabbitc.subway.algroithm.graph;

/**
 * 图中的边，表示两个节点之间的连接。
 * <p>
 * weight 字段用于区分边类型：
 * - 0：表示同一线路内的相邻站点边
 * - 1：表示换乘边（不同线路之间的连接）
 * </p>
 */
public class Edge {
    /** 目标节点（格式：lineId|dirKey|station）。 */
    public String node;   // 目标节点（格式：lineId|dirKey|station）
    /** 边类型代价：0 表示同线相邻站，1 表示换乘连接。 */
    public int weight;    // 0 或 1

    /** 创建一条指向目标节点的图边。 */
    public Edge(String node, int weight) {
        this.node = node;
        this.weight = weight;
    }
}
