package com.rabbitc.subway.data;

import com.rabbitc.subway.data.json.MetroData;
import com.rabbitc.subway.data.parse.CrowdRule;
import com.rabbitc.subway.data.parse.FareSegment;
import com.rabbitc.subway.data.parse.HeadwayRule;
import com.rabbitc.subway.data.parse.LinearExtension;
import com.rabbitc.subway.data.parse.OpenTimeRule;
import com.rabbitc.subway.data.parse.SpecialLineRule;
import com.rabbitc.subway.data.parse.SpeedLimit;
import com.rabbitc.subway.data.parse.VirtualTransfer;

import java.util.List;
import java.util.Map;

/**
 * 全局数据缓存单例，用于存储原始 JSON 字符串及其解析后的结构化数据。
 * <p>
 * 采用线程安全的懒加载单例模式（静态内部类），所有数据通过 getter/setter 访问，
 * 关键写操作通过 {@code synchronized} 保证线程安全。
 * </p>
 */
public final class DataCache {

    // ========================= 单例实现 =========================

    private static final class Holder {
        private static final DataCache INSTANCE = new DataCache();
    }

    /** 返回进程内唯一的全局数据缓存实例。 */
    public static DataCache getInstance() {
        return Holder.INSTANCE;
    }

    private DataCache() {
        // 私有构造，防止外部实例化
    }

    // ========================= 原始数据 =========================
    private String jsonData;

    // ========================= 解析后的数据 =========================
    private Map<String, double[]> stationCoords;
    private Map<String, List<String>> dirStations;
    private Map<String, String> lineNameMap;
    private Map<String, String> lineColorMap;
    private Map<String, String> lineCityMap;
    private Map<String, Boolean> hasLoopMap;
    private String keyCity;
    private Map<String, Double> lineTotalLengthMap;
    private Map<String, Boolean> lineSpecialMap;
    private Map<String, Boolean> lineHasFastMap;
    private Map<String, Integer> lineMaxSpeedMap;
    private Map<String, Boolean> lineSegmentedMap;
    private Map<String, String> dirDirectionTypeMap;
    private Map<String, Map<String, String>> stationLineStatusMap;
    private Map<String, Map<String, String>> transferStatusMap;
    private Map<String, Double> specialTransferTimeMap;  // 站点特殊换乘时间
    private Map<String, Boolean> dirFutureMap;  // 方向键 -> is_future
    private Map<String, Boolean> dirLoopMap;  // 方向键 -> is_loop
    private Map<String, Boolean> hasFutureMap;  // 线路ID -> 是否有未来线路
    private Map<String, Boolean> allFutureMap;  // 线路ID -> 是否全为未来线路
    private Map<String, Double> lineBrakeMap;    // 线路ID -> 制动加速度 (m/s²)
    private Map<String, Double> stationStopTimeMap;  // 键: "站点名|方向键"，值: 停站时间（秒）

    private Map<String, Double> lineTotalLengthMapWithoutFuture;  // 不包含未来站点的距离信息
    private double avgTransferTime = 120.0;        // 平均换乘时间（秒）
    private double avgStopTime = 45.0;             // 平均停站时间（秒）
    private double outStationTransferTime = 300.0; // 出站换乘平均时间（秒）
    private double avgEntryTime = 120.0;          // 进站平均时间（秒），默认 120.0 秒
    private String pricingType;
    private List<FareSegment> fareSegments;
    private LinearExtension linearExtension;
    private List<SpecialLineRule> specialLineRules;
    private Double defaultFare;
    private String version;
    private String updateDate;
    private List<StationValue> cachedBatchData;
    private String cachedStartStation;
    private List<SpeedLimit> speedLimits;
    private List<HeadwayRule> headwayRules;
    private List<CrowdRule> crowdRules;
    private Map<String, String> lineFormationMap;   // lineId -> 编组如 "6B"
    private Map<String, Double> linePowerKwMap;     // lineId -> 功率(kW)
    private double defaultBoardTime = 3.0;          // 最短上车时间（秒）
    private List<OpenTimeRule> openTimes;
    private List<VirtualTransfer> virtualTransfers;
    private double fareCap = 0.0;  // 票价封顶，0 表示无封顶
    private MetroData metroData = null;
    // ========================= 缓存有效性标记 =========================
    private volatile boolean cacheValid = false;

    // ========================= 清理与状态 =========================

    /** 清空原始数据、解析结果和计算缓存，并恢复默认参数。 */
    public synchronized void clear() {
        jsonData = null;
        stationCoords = null;
        dirStations = null;
        lineNameMap = null;
        lineColorMap = null;
        lineCityMap = null;
        hasLoopMap = null;
        keyCity = null;
        lineTotalLengthMap = null;
        lineSpecialMap = null;
        transferStatusMap = null;
        stationLineStatusMap = null;
        specialTransferTimeMap = null;
        avgTransferTime = 120.0;
        avgStopTime = 45.0;
        outStationTransferTime = 300.0;
        pricingType = null;
        fareSegments = null;
        linearExtension = null;
        specialLineRules = null;
        defaultFare = null;
        cachedBatchData = null;
        cachedStartStation = null;
        lineSegmentedMap = null;
        lineHasFastMap = null;
        lineMaxSpeedMap = null;
        dirDirectionTypeMap = null;
        dirFutureMap = null;
        lineTotalLengthMapWithoutFuture = null;
        hasFutureMap = null;
        avgEntryTime = 120.0;
        lineBrakeMap = null;
        stationStopTimeMap = null;
        defaultBoardTime = 3.0;
        headwayRules = null;
        crowdRules = null;
        lineFormationMap = null;
        linePowerKwMap = null;
        openTimes = null;
        virtualTransfers = null;
        allFutureMap = null;
        metroData = null;
        cacheValid = false;
    }

    /** 判断缓存是否具备可供页面和算法使用的完整原始数据。 */
    public boolean isCacheValid() {
        return cacheValid && jsonData != null;
    }

    // ========================= Getter / Setter =========================
    public synchronized String getJsonData() { return jsonData; }
    public synchronized void setJsonData(String jsonData) { this.jsonData = jsonData; }

    /**
     * 获取原始 MetroData 对象（Gson 解析后的完整 JSON 结构）。
     *
     * @return 原始地铁数据对象，可能为 null
     */
    public MetroData getMetroData() { return metroData; }
    public void setMetroData(MetroData metroData) { this.metroData = metroData; }
    /**
     * 获取不含未来站点的线路长度表。
     *
     * @return 线路ID → 不含未来方向的总里程（公里）
     */
    public Map<String, Double> getLineTotalLengthMapWithoutFuture() { return lineTotalLengthMapWithoutFuture; }
    public void setLineTotalLengthMapWithoutFuture(Map<String, Double> lineTotalLengthMapWithoutFuture) { this.lineTotalLengthMapWithoutFuture = lineTotalLengthMapWithoutFuture; }
    /**
     * 获取站点坐标表。
     *
     * @return 不可变映射：站点名 → double[]{经度, 纬度}
     */
    public synchronized Map<String, double[]> getStationCoords() { return stationCoords; }
    public synchronized void setStationCoords(Map<String, double[]> stationCoords) { this.stationCoords = stationCoords; }
    /**
     * 获取方向站点列表表。
     *
     * @return 方向键 → 该方向按顺序排列的站点名列表
     * @see com.rabbitc.subway.utils.GraphKeyUtils#makeDirKey(String, String, String)
     */
    public synchronized Map<String, List<String>> getDirStations() { return dirStations; }
    public synchronized void setDirStations(Map<String, List<String>> dirStations) { this.dirStations = dirStations; }
    /**
     * 获取线路显示名称表。
     *
     * @return 线路ID → 显示名称（非同城线路含城市前缀）
     */
    public synchronized Map<String, String> getLineNameMap() { return lineNameMap; }
    public synchronized void setLineNameMap(Map<String, String> lineNameMap) { this.lineNameMap = lineNameMap; }
    /**
     * 获取线路是否有未来方向状态表。
     *
     * @return 线路ID → 该线路是否包含未开通的未来方向
     */
    public Map<String, Boolean> getHasFutureMap() { return hasFutureMap; }
    public void setHasFutureMap(Map<String, Boolean> hasFutureMap) { this.hasFutureMap = hasFutureMap; }
    /**
     * 获取线路是否全为未来线路状态表。
     *
     * @return 线路ID → 该线路是否所有方向均为未来线路
     */
    public Map<String, Boolean> getAllFutureMap() { return allFutureMap; }
    public void setAllFutureMap(Map<String, Boolean> hasFutureMap) { this.allFutureMap = hasFutureMap; }
    /**
     * 获取方向环线标记表。
     *
     * @return 方向键 → 该方向是否为环线方向
     */
    public Map<String, Boolean> getDirLoopMap() { return dirLoopMap; }
    public void setDirLoopMap(Map<String, Boolean> dirLoopMap) { this.dirLoopMap = dirLoopMap; }
    /**
     * 获取线路颜色表。
     *
     * @return 线路ID → 十六进制颜色值（格式如 "#FF0000"）
     */
    public synchronized Map<String, String> getLineColorMap() { return lineColorMap; }
    public synchronized void setLineColorMap(Map<String, String> lineColorMap) { this.lineColorMap = lineColorMap; }
    /**
     * 获取线路所属城市表。
     *
     * @return 线路ID → 城市名称
     */
    public synchronized Map<String, String> getLineCityMap() { return lineCityMap; }
    public synchronized void setLineCityMap(Map<String, String> lineCityMap) { this.lineCityMap = lineCityMap; }
    /**
     * 获取线路环线状态表。
     *
     * @return 线路ID → 该线路是否包含环线方向
     */
    public synchronized Map<String, Boolean> getHasLoopMap() { return hasLoopMap; }
    public synchronized void setHasLoopMap(Map<String, Boolean> hasLoopMap) { this.hasLoopMap = hasLoopMap; }

    public synchronized String getKeyCity() { return keyCity; }
    public synchronized void setKeyCity(String keyCity) { this.keyCity = keyCity; }
    /**
     * 获取线路总长度表（去重后所有边的累计长度）。
     *
     * @return 线路ID → 总里程（公里）
     */
    public synchronized Map<String, Double> getLineTotalLengthMap() { return lineTotalLengthMap; }
    public synchronized void setLineTotalLengthMap(Map<String, Double> lineTotalLengthMap) { this.lineTotalLengthMap = lineTotalLengthMap; }
    public synchronized Map<String, Boolean> getLineSpecialMap() { return lineSpecialMap; }
    public synchronized void setLineSpecialMap(Map<String, Boolean> lineSpecialMap) { this.lineSpecialMap = lineSpecialMap; }
    /**
     * 获取站点线路状态表。
     *
     * @return 站点名 → (线路ID → 状态字符串，如 "operating", "under_construction", "future")
     */
    public synchronized Map<String, Map<String, String>> getStationLineStatusMap() { return stationLineStatusMap; }
    public synchronized void setStationLineStatusMap(Map<String, Map<String, String>> stationLineStatusMap) { this.stationLineStatusMap = stationLineStatusMap; }
    /**
     * 获取换乘状态表。
     *
     * @return 站点名 → (换乘键如 "lineA|lineB" → 状态字符串)
     */
    public synchronized Map<String, Map<String, String>> getTransferStatusMap() { return transferStatusMap; }
    public synchronized void setTransferStatusMap(Map<String, Map<String, String>> transferStatusMap) { this.transferStatusMap = transferStatusMap; }
    /**
     * 获取站点特殊换乘时间表（覆盖默认换乘时间）。
     *
     * @return 键为 "站点名|换乘键" → 额外耗时（秒），包含正向和反向键
     */
    public synchronized Map<String, Double> getSpecialTransferTimeMap() { return specialTransferTimeMap; }
    public synchronized void setSpecialTransferTimeMap(Map<String, Double> specialTransferTimeMap) { this.specialTransferTimeMap = specialTransferTimeMap; }
    public synchronized double getAvgTransferTime() { return avgTransferTime; }
    public synchronized void setAvgTransferTime(double avgTransferTime) { this.avgTransferTime = avgTransferTime; }
    public synchronized double getAvgStopTime() { return avgStopTime; }
    public synchronized void setAvgStopTime(double avgStopTime) { this.avgStopTime = avgStopTime; }
    public synchronized double getOutStationTransferTime() { return outStationTransferTime; }
    public synchronized void setOutStationTransferTime(double outStationTransferTime) { this.outStationTransferTime = outStationTransferTime; }
    public synchronized String getPricingType() { return pricingType; }
    public synchronized void setPricingType(String pricingType) { this.pricingType = pricingType; }
    /**
     * 获取票价阶梯区间表。
     *
     * @return 按里程范围划分的票价阶梯列表
     */
    public synchronized List<FareSegment> getFareSegments() { return fareSegments; }
    public synchronized void setFareSegments(List<FareSegment> fareSegments) { this.fareSegments = fareSegments; }
    /**
     * 获取票价线性扩展规则。
     *
     * @return 超出阶梯上限后的线性计价规则（阈值、基础票价、每公里费率）
     */
    public synchronized LinearExtension getLinearExtension() { return linearExtension; }
    public synchronized void setLinearExtension(LinearExtension linearExtension) { this.linearExtension = linearExtension; }
    /**
     * 获取特殊线路票价规则列表。
     *
     * @return 特殊线路的附加费或独立计价规则
     */
    public synchronized List<SpecialLineRule> getSpecialLineRules() { return specialLineRules; }
    public synchronized void setSpecialLineRules(List<SpecialLineRule> specialLineRules) { this.specialLineRules = specialLineRules; }
    /**
     * 获取默认票价（不满足任何阶梯规则时使用）。
     *
     * @return 默认票价（元），可能为 null
     */
    public synchronized Double getDefaultFare() { return defaultFare; }
    public synchronized void setDefaultFare(Double defaultFare) { this.defaultFare = defaultFare; }
    public synchronized List<StationValue> getCachedBatchData() { return cachedBatchData; }
    public synchronized void setCachedBatchData(List<StationValue> cachedBatchData) { this.cachedBatchData = cachedBatchData; }
    public synchronized String getCachedStartStation() { return cachedStartStation; }
    public synchronized void setCachedStartStation(String cachedStartStation) { this.cachedStartStation = cachedStartStation; }
    public synchronized void setCacheValid(boolean cacheValid) { this.cacheValid = cacheValid; }
    /**
     * 获取线路分段运营状态表。
     *
     * @return 线路ID → 是否为分段运营（站点不连通的多段运营）
     */
    public synchronized Map<String, Boolean> getLineSegmentedMap() { return lineSegmentedMap; }
    public synchronized void setLineSegmentedMap(Map<String, Boolean> lineSegmentedMap) { this.lineSegmentedMap = lineSegmentedMap; }
    /**
     * 获取线路快车状态表。
     *
     * @return 线路ID → 该线路是否包含快车方向
     */
    public synchronized Map<String, Boolean> getLineHasFastMap() { return lineHasFastMap; }
    public synchronized void setLineHasFastMap(Map<String, Boolean> lineHasFastMap) { this.lineHasFastMap = lineHasFastMap; }
    /**
     * 获取线路最高速度表。
     *
     * @return 线路ID → 最高运营速度（km/h）
     */
    public synchronized Map<String, Integer> getLineMaxSpeedMap() { return lineMaxSpeedMap; }
    public synchronized void setLineMaxSpeedMap(Map<String, Integer> lineMaxSpeedMap) { this.lineMaxSpeedMap = lineMaxSpeedMap; }
    public synchronized String getVersion() { return version; }
    public synchronized void setVersion(String version) { this.version = version; }
    public synchronized String getUpdateDate() { return updateDate; }
    public synchronized void setUpdateDate(String updateDate) { this.updateDate = updateDate; }
    /**
     * 获取方向方向类型表。
     *
     * @return 方向键 → 方向类型字符串（如 "left", "right", "inner", "outer"）
     */
    public synchronized Map<String, String> getDirDirectionTypeMap() { return dirDirectionTypeMap; }
    public synchronized void setDirDirectionTypeMap(Map<String, String> dirDirectionTypeMap) { this.dirDirectionTypeMap = dirDirectionTypeMap; }
    /**
     * 获取方向未来状态表。
     *
     * @return 方向键 → 该方向是否为未来线路（未开通）
     */
    public synchronized Map<String, Boolean> getDirFutureMap() { return dirFutureMap; }
    public synchronized void setDirFutureMap(Map<String, Boolean> dirFutureMap) { this.dirFutureMap = dirFutureMap; }
    public synchronized double getAvgEntryTime() { return avgEntryTime; }
    public synchronized void setAvgEntryTime(double avgEntryTime) { this.avgEntryTime = avgEntryTime; }
    /**
     * 获取线路制动加速度表。
     *
     * @return 线路ID → 制动加速度（m/s²）
     */
    public synchronized Map<String, Double> getLineBrakeMap() { return lineBrakeMap; }
    public synchronized void setLineBrakeMap(Map<String, Double> lineBrakeMap) { this.lineBrakeMap = lineBrakeMap; }
    /**
     * 获取站点停站时间表。
     *
     * @return 键为 "站点名|方向键" → 该站点在该方向上的停站时间（秒）
     */
    public synchronized Map<String, Double> getStationStopTimeMap() { return stationStopTimeMap; }
    public synchronized void setStationStopTimeMap(Map<String, Double> stationStopTimeMap) { this.stationStopTimeMap = stationStopTimeMap; }
    /**
     * 获取限速规则列表。
     *
     * @return 路段限速规则列表，指定特定区间和方向的限速值
     */
    public synchronized List<SpeedLimit> getSpeedLimits() { return speedLimits; }
    public synchronized void setSpeedLimits(List<SpeedLimit> speedLimits) { this.speedLimits = speedLimits; }
    /**
     * 获取拥挤度规则列表。
     *
     * @return 按时段定义拥挤系数的规则列表
     */
    public synchronized List<CrowdRule> getCrowdRules() { return crowdRules; }
    public synchronized void setCrowdRules(List<CrowdRule> crowdRules) { this.crowdRules = crowdRules; }
    /**
     * 获取行车间隔规则列表。
     *
     * @return 按时段定义各线路发车间隔的规则列表
     */
    public synchronized List<HeadwayRule> getHeadwayRules() { return headwayRules; }
    public synchronized void setHeadwayRules(List<HeadwayRule> headwayRules) { this.headwayRules = headwayRules; }
    /**
     * 获取线路编组表。
     *
     * @return 线路ID → 编组类型（如 "6B", "8A"）
     */
    public synchronized Map<String, String> getLineFormationMap() { return lineFormationMap; }
    public synchronized void setLineFormationMap(Map<String, String> lineFormationMap) { this.lineFormationMap = lineFormationMap; }
    /**
     * 获取线路功率表。
     *
     * @return 线路ID → 功率（kW）
     */
    public synchronized Map<String, Double> getLinePowerKwMap() { return linePowerKwMap; }
    public synchronized void setLinePowerKwMap(Map<String, Double> linePowerKwMap) { this.linePowerKwMap = linePowerKwMap; }
    public synchronized double getDefaultBoardTime() { return defaultBoardTime; }
    public synchronized void setDefaultBoardTime(double defaultBoardTime) { this.defaultBoardTime = defaultBoardTime; }
    /**
     * 获取票价封顶值。
     *
     * @return 票价封顶金额（元），0 表示无封顶
     */
    public synchronized double getFareCap() { return fareCap; }
    public synchronized void setFareCap(double fareCap) { this.fareCap = fareCap; }
    /**
     * 获取运营时间规则列表。
     *
     * @return 定义各线路运营时段的规则列表
     */
    public synchronized List<OpenTimeRule> getOpenTimes() { return openTimes; }
    public synchronized void setOpenTimes(List<OpenTimeRule> openTimes) { this.openTimes = openTimes; }
    /**
     * 获取虚拟换乘列表。
     *
     * @return 站外换乘通道��表，含步行时间与安检信息
     */
    public synchronized List<VirtualTransfer> getVirtualTransfers() { return virtualTransfers; }
    public synchronized void setVirtualTransfers(List<VirtualTransfer> virtualTransfers) { this.virtualTransfers = virtualTransfers; }
}
