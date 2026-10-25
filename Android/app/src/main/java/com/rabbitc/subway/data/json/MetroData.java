package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;
import java.util.List;
import java.util.Map;

/**
 * 根 JSON 对象，是地铁地图数据文件的顶层结构。
 *
 * <p>使用 Gson 从 JSON 反序列化，字段名通过 {@link SerializedName} 注解映射到
 * JSON 中的下划线命名。该类包含了地铁地图的所有基础数据：</p>
 *
 * <h3>元信息</h3>
 * <ul>
 *   <li>{@link #keyCity} — 主要城市名称，如 "北京"</li>
 *   <li>{@link #author} — 数据作者</li>
 *   <li>{@link #version} — 数据版本号</li>
 *   <li>{@link #createDate} — 数据创建日期</li>
 *   <li>{@link #updateDate} — 数据最后更新日期</li>
 * </ul>
 *
 * <h3>运营数据</h3>
 * <ul>
 *   <li>{@link #operationParams} — 运营参数（换乘时间、停站时间、进站时间等）</li>
 *   <li>{@link #stations} — 所有站点集合（站点名 → Station）</li>
 *   <li>{@link #lines} — 所有线路集合（线路ID → Line）</li>
 *   <li>{@link #depots} — 车辆段列表</li>
 * </ul>
 *
 * <h3>票价数据</h3>
 * <ul>
 *   <li>{@link #price} — 票价体系（阶梯区间、线性扩展、特殊线路规则、票���封顶）</li>
 * </ul>
 *
 * <h3>运行规则</h3>
 * <ul>
 *   <li>{@link #speedLimits} — 限速规则列表</li>
 *   <li>{@link #headwayRules} — 行车间隔规则列表</li>
 *   <li>{@link #crowdRules} — 拥挤度规则列表</li>
 *   <li>{@link #openTimes} — 运营时间规则列表</li>
 * </ul>
 *
 * <h3>附加数据</h3>
 * <ul>
 *   <li>{@link #virtualTransfers} — 虚拟换乘列表（站外换乘通道）</li>
 *   <li>{@link #stationModes} — 站点模式列表</li>
 *   <li>{@link #labels} — 标签列表</li>
 *   <li>{@link #facilities} — 站点设施信息</li>
 *   <li>{@link #promotions} — 优惠活动信息</li>
 * </ul>
 *
 * @see OperationParams
 * @see Station
 * @see Line
 * @see Price
 */
public class MetroData {
    /** 主要城市名称，如 "北京"、"上海" */
    @SerializedName("key_city")
    public String keyCity;
    /** 数据作者名称 */
    public String author;
    /** 数据版本号 */
    public String version;
    /** 数据创建日期 */
    @SerializedName("create_date")
    public String createDate;
    /** 数据最后更新日期 */
    @SerializedName("update_date")
    public String updateDate;
    /** 运营参数（换乘时间、停站时间、进站时间等） */
    @SerializedName("operation_params")
    public OperationParams operationParams;
    /** 票价体系（包含阶梯区间、线性扩展、特殊线路规则、票价封顶） */
    public Price price;
    /** 所有站点集合，键为站点名，值为车站详细信息 */
    public Map<String, Station> stations;
    /** 所有线路集合，键为线路ID，值为线路详细信息（含方向与站点） */
    public Map<String, Line> lines;
    /** 行车间隔规则列表，按时段定义发车间隔 */
    @SerializedName("headway_rules")
    public List<HeadwayRule> headwayRules;
    /** 站点模���列表 */
    @SerializedName("station_modes")
    public List<StationMode> stationModes;
    /** 拥挤度规则列表，按时段定义拥挤系数 */
    @SerializedName("crowd_rules")
    public List<CrowdRule> crowdRules;
    /** 运营时间规则列表，定义各线路的运营时段 */
    @SerializedName("open_times")
    public List<OpenTimeRule> openTimes;
    /** 车辆段列表 */
    public List<Depot> depots;
    /** 限速规则列表，定义特定区间的限速值 */
    @SerializedName("speed_limits")
    public List<SpeedLimit> speedLimits;
    /** 虚拟换乘列表（站外换乘通道，含步行时间与安检信息） */
    @SerializedName("virtual_transfers")
    public List<VirtualTransfer> virtualTransfers;
    /** 标签列表 */
    public List<Label> labels;
    /** 站点设施信息，键为站点名，值为设施详情 */
    public Map<String, StationFacilities> facilities;
    /** 优惠活动列表 */
    public List<Promotion> promotions;
}

