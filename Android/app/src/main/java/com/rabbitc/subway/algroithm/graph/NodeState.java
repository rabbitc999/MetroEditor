package com.rabbitc.subway.algroithm.graph;

/**
 * 用于 Dijkstra 优先队列的节点状态，包含节点标识和当前累计代价。
 */
public class NodeState {
    /** 队列中待处理的图节点标识。 */
    public String node;
    /** 从起点到该节点的当前累计代价。 */
    public double totalCost;

    /** 创建一个带有累计代价的队列状态。 */
    public NodeState(String node, double totalCost) {
        this.node = node;
        this.totalCost = totalCost;
    }
}
