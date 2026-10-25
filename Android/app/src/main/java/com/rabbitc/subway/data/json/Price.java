package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * 票价体系数据模型，是地铁票价规则的全量配置。
 * 定义货币类型、计价方式、默认票价、分段票价、延伸计价、换乘优惠、
 * 票价上限及特殊线路票价规则等完整的票价体系。
 */
public class Price {

    /** 货币单位代码（如 "CNY"） */
    public String currency;

    /** 计价方式类型（JSON: "pricingType"） */
    @SerializedName("pricingType")
    public String pricingType;

    /** 默认票价金额（JSON: "default_fare"） */
    @SerializedName("default_fare")
    public double defaultFare;

    /** 按里程分段的票价列表 */
    public List<FareSegment> segments;

    /** 线性延伸计价规则 */
    public LinearExtension linearExtension;

    /** 换乘优惠政策（JSON: "transferDiscount"） */
    @SerializedName("transferDiscount")
    public TransferDiscount transferDiscount;

    /** 票价上限（元），超过该金额不再累加（JSON: "fare_cap"） */
    @SerializedName("fare_cap")
    public double fareCap;

    /** 特殊线路票价规则列表，用于机场线等有独立计价规则的线路（JSON: "specialLines"） */
    @SerializedName("specialLines")
    public List<SpecialLineRule> specialLines;
}
