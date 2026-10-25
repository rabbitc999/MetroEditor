package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * 票价优惠/促销规则模型，定义地铁票价折扣或减免活动。
 * 支持按比例折扣、固定金额减免和固定优惠金额等多种优惠类型。
 */
public class Promotion {

    /** 优惠活动唯一标识符 */
    public String id;

    /** 优惠活动名称 */
    public String name;

    /** 优惠活动描述信息 */
    public String description;

    /** 优惠类型，取值如 "rate"（折扣率）、"fixed"（固定金额）、"fixed_discount"（固定减免） */
    public String type;

    /** 优惠数值，根据 type 类型表示折扣率（如0.8=8折）或固定金额 */
    public double value;

    /** 适用该优惠的线路ID列表 */
    public List<String> applicable_lines;

    /** 优惠活动生效开始时间（JSON: "start_time"） */
    @SerializedName("start_time")
    public String startTime;

    /** 优惠活动生效结束时间（JSON: "end_time"） */
    @SerializedName("end_time")
    public String endTime;

    /** 优惠活动适用的星期列表，如 ["周一","周二","周三","周四","周五"] */
    public List<String> days;
}
