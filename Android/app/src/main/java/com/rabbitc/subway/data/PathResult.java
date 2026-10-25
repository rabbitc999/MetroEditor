package com.rabbitc.subway.data;

import java.util.List;

/**
 * 路径规划结果，包含换乘次数和路径节点列表。
 * <p>
 * 由 {@link com.rabbitc.subway.algroithm.MetroGraph} 返回。
 * </p>
 */
public class PathResult {

    /**
     * 换乘次数，-1 表示路径不可达。
     * <p>
     * 注意：此换乘次数是路径中方向变化的次数，
     * 与 {@link com.rabbitc.subway.utils.GraphKeyUtils#countTransfers(List)} 一致。
     * </p>
     */
    public int transfers;

    /** 路径节点列表，每个节点格式为 "dirKey|station" */
    public List<String> path;

    /** 按搜索权重计算出的路径总代价。 */
    public double cost;

    /** 普通线路累计票价，未计算时保持默认值。 */
    public double totalMoney;

    /** 特殊线路或附加规则产生的票价。 */
    public double specialMoney;

    /** 创建一条路径结果。 */
    public PathResult(int transfers, double cost, List<String> path) {
        this.transfers = transfers;
        this.path = path;
        this.cost = cost;
    }
}
