package com.rabbitc.subway.data;

import android.content.Context;
import android.content.SharedPreferences;

import com.rabbitc.subway.utils.GraphKeyUtils;
import com.rabbitc.subway.algroithm.FareCalculator;
import com.rabbitc.subway.algroithm.MetroGraph;

import java.util.List;

/**
 * 多维度搜索权重配置，用于地铁路径规划中各因素的加权计算。
 *
 * <p>搜索时使用的权重包括：距离、换乘次数、时间、站点数，以及可选的拥挤度。
 * 票价权重仅用于最终路径的综合价值计算（{@link #computeValue}），不参与路径搜索过程。</p>
 *
 * <p>预置方案：</p>
 * <ul>
 *   <li>{@link #getShortestWeight()} — 最短距离搜索（仅距离权重为 1）</li>
 *   <li>{@link #fromContext(Context)} — 从 SharedPreferences 读取用户自定义权重</li>
 * </ul>
 *
 * @see com.rabbitc.subway.algroithm.MetroGraph
 */
public class SearchWeight {
    /** 距离权重（公里数），值越大越偏向短距离路径 */
    public final double distanceWeight;
    /** 换乘权重（换乘次数），值越大越偏向少换乘路径 */
    public final double transferWeight;
    /** 票价权重，仅用于最终价值计算，搜索时不使用 */
    public final double moneyWeight;
    /** 时间权重（分钟数），搜索时使用，值越大越偏向省时路径 */
    public final double timeWeight;
    /** 站点数权重，值越大越偏向少站点路径 */
    public final double stationWeight;
    /** 拥挤度权重，值越大越偏向避开拥挤路段 */
    public double crowdWeight;

    /**
     * 构造包含拥挤度权重的完整搜索权重配置。
     *
     * @param distanceWeight 距离权重
     * @param transferWeight 换乘权重
     * @param moneyWeight    票价权重（仅用于最终分值计算）
     * @param timeWeight     时间权重
     * @param stationWeight  站点数权重
     * @param crowdWeight    拥挤度权重
     */
    // 构造方法需相应调整
    public SearchWeight(double distanceWeight, double transferWeight, double moneyWeight, double timeWeight, double stationWeight, double crowdWeight) {
        this.moneyWeight = moneyWeight;
        this.transferWeight = transferWeight;
        this.timeWeight = timeWeight;
        this.distanceWeight = distanceWeight;
        this.stationWeight = stationWeight;
        this.crowdWeight = crowdWeight;
    }

    /**
     * 构造不含拥挤度权重的搜索权重配置（拥挤度默认为 0）。
     *
     * @param distanceWeight 距离权重
     * @param transferWeight 换乘权重
     * @param moneyWeight    票价权重（仅用于最终分值计算）
     * @param timeWeight     时间权重
     * @param stationWeight  站点数权重
     */
    public SearchWeight(double distanceWeight, double transferWeight,
                        double moneyWeight, double timeWeight, double stationWeight) {
        this.distanceWeight = distanceWeight;
        this.transferWeight = transferWeight;
        this.moneyWeight = moneyWeight;
        this.timeWeight = timeWeight;
        this.stationWeight = stationWeight;
    }

    /**
     * 创建最短距离搜索权重（仅考虑距离，距离权重为 1，其余为 0）。
     *
     * @return 最短距离搜索权重实例
     */
    public static SearchWeight getShortestWeight()
    {
        return new SearchWeight(1, 0, Double.POSITIVE_INFINITY, 0, 0);
    }

    /**
     * 从 SharedPreferences 中读取用户自定义的路径搜索权重。
     *
     * <p>从 {@code subway_weights} 偏好文件中读取各权重值，默认值与预置方案相符。</p>
     *
     * @param context Android 上下文
     * @return 用户自定义的搜索权重实例
     */
    // 从 SharedPreferences 构建完整权重
    public static SearchWeight fromContext(Context context) {
        SharedPreferences prefs = context.getSharedPreferences("subway_weights", Context.MODE_PRIVATE);
        float length = prefs.getFloat("length_weight", 0.02f);
        float transfer = prefs.getFloat("transfer_weight", 0.5f);
        float money = prefs.getFloat("money_weight", 0.2f);
        float time = prefs.getFloat("time_weight", 0.01f);
        float station = prefs.getFloat("station_weight", 0.1f);
        float crowd = prefs.getFloat("crowd_weight", 0.1f);
        return new SearchWeight(length, transfer, money, time, station, crowd);
    }

    /**
     * 将当前权重保存到 SharedPreferences，作为“上一个权重”供下次使用。
     */
    public void saveToContext(Context context) {
        SharedPreferences prefs = context.getSharedPreferences("subway_weights", Context.MODE_PRIVATE);
        prefs.edit()
                .putFloat("length_weight", (float) distanceWeight)
                .putFloat("transfer_weight", (float) transferWeight)
                .putFloat("money_weight", (float) moneyWeight)
                .putFloat("time_weight", (float) timeWeight)
                .putFloat("station_weight", (float) stationWeight)
                .putFloat("crowd_weight", (float) crowdWeight)
                .apply();
    }

    /**
     * 综合价值计算（包含所有因子）
     * @param path       用户自定义权重搜索出的路径（用于计算距离、换乘、时间、站数）
     * @param shortest   最短距离路径（仅用于票价计算）
     * @param graph      MetroGraph 实例
     * @return 综合价值（数值越小越好）
     */
    public double computeValue(List<String> path, List<String> shortest, double time, MetroGraph graph) {
        if (path == null || path.size() < 2) return Double.MAX_VALUE;

        // 1. 当前路径的指标
        double distance = graph.computePathDistance(path);
        int transfers = GraphKeyUtils.countTransfers(path);
        int stationCount = graph.getUniqueStationCount(path);

        // 2. 票价：基于最短距离路径（如果 shortest 有效）
        double fare = 0.0;
        if (shortest != null) {
            double rawFare = FareCalculator.calculateFareWithoutSpecial(shortest, graph).first;
            // 确保票价非负
            fare = Math.max(0, rawFare);
        }

        // 3. 综合价值
        return distance * distanceWeight
                + transfers * transferWeight
                + fare * moneyWeight
                + time * timeWeight / 60
                + stationCount * stationWeight;
    }
}