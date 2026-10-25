package com.rabbitc.subway.data.parse;

import java.util.List;

/**
 * 特殊线路票价规则，由 {@code DataParser} 从原始 JSON 解析得到，表示特殊线路的完整票价配置。
 * 包含统一票价、分段票价、线性延伸和区间附加费，存储于 {@code DataCache} 中供运行时票价计算使用。
 */
public class SpecialLineRule {
    /** 单条线路标识（兼容旧逻辑） */
    public String lineId;
    /** 线路组列表（新逻辑） */
    public List<String> useLines;
    /** 统一票价（元），当所有区间票价相同时使用 */
    public Double all;
    /** 区间附加费列表 */
    public List<Surcharge> surcharge;
    // 新增完整票价配置
    /** 默认票价（元） */
    public Double defaultFare;
    /** 票价分段列表 */
    public List<FareSegment> segments;
    /** 线性延伸票价规则 */
    public LinearExtension linearExtension;
}
