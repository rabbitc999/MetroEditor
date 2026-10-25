package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

/**
 * 地铁运营参数，定义路网全局的默认时间参数。
 *
 * <p>这些参数在路径规划中用于估算总行程时间：</p>
 * <ul>
 *   <li><b>站内换乘</b> — 同一站点内从一个方向到另一个方向的平均步行时间</li>
 *   <li><b>停站</b> — 列车在站点停靠的平均耗时</li>
 *   <li><b>出站换乘</b> — 需要出站后再次进站的换乘所需额外时间</li>
 *   <li><b>进站</b> — 从地铁入口到站台的平均耗时</li>
 * </ul>
 *
 * <p>当 JSON 中缺少 {@code operation_params} 节点时，{@link com.rabbitc.subway.data.DataParser}
 * 会使用默认值：换乘 120 秒、停站 45 秒、出站换乘 300 秒、进站 120 秒。</p>
 *
 * @see com.rabbitc.subway.data.DataParser#parse()
 */
public class OperationParams {
    /** 站内换乘时间（秒） */
    @SerializedName("inner_transfer_time")
    public double innerTransferTime;
    /** 平均停站时间（秒） */
    @SerializedName("dwell_time")
    public double dwellTime;
    /** 出站换乘时间（秒），用于站外换乘通道的时间估算 */
    @SerializedName("out_station_transfer_time")
    public double outStationTransferTime;
    /** 进站时间（秒），从地铁入口步行到站台的平均耗时 */
    @SerializedName("entry_time")
    public double entryTime;
}
