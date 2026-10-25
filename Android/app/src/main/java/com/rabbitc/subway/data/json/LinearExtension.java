package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

/**
 * 线性延伸计价模型，定义超出基础里程后的线性票价计算规则。
 * 超过阈值里程的部分按每公里固定费率累进计费。
 */
public class LinearExtension {

    /** 基础里程阈值（公里），超出该里程后开始按延伸费率计费 */
    public double threshold;

    /** 基准票价，阈值范围内的基础票价 */
    public double baseFare;

    /** 超出阈值后每公里的费率（JSON: "ratePerKm"） */
    @SerializedName("ratePerKm")
    public double ratePerKm;
}
