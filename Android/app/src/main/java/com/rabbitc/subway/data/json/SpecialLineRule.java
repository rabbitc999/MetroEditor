package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * 特殊线路票价规则，用于定义特定线路（如机场线、环线等）的独立计价方式。
 * 支持统一票价、按里程分段计价以及区间附加费等多种计费模式。
 * JSON 中对应字段为 "use_lines" 和 "linear_extension"。
 */
public class SpecialLineRule {

    /** 线路唯一标识符 */
    public String lineId;

    /** 适用该规则的一组线路ID列表（JSON: "use_lines"） */
    @SerializedName("use_lines")
    public List<String> useLines;

    /** 统一票价（适用于全线统一价格的线路），为 null 则表示未设置 */
    public Double all;

    /** 区间附加费列表，定义特定区间段的附加费用 */
    public List<Surcharge> surcharge;

    /** 默认票价（JSON: "default_fare"） */
    @SerializedName("default_fare")
    public Double defaultFare;

    /** 按里程分段的票价列表 */
    public List<FareSegment> segments;

    /** 线性延伸计价规则（JSON: "linear_extension"） */
    @SerializedName("linear_extension")
    public LinearExtension linearExtension;
}
