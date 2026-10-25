package com.rabbitc.subway.data.json;

/**
 * 票价分段模型，定义按里程区间划分的票价规则。
 * 每个分段包含里程范围和该范围对应的票价。
 */
public class FareSegment {

    /** 该分段的起始里程（公里） */
    public double rangeStart;

    /** 该分段的结束里程（公里） */
    public double rangeEnd;

    /** 该里程范围内的票价金额 */
    public double fare;
}
