package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * 客流拥挤度规则模型，定义线路在特定方向和时段的客流拥挤系数。
 * 用于计算和展示地铁线路的实时拥挤程度。
 */
public class CrowdRule {

    /** 运行方向键（JSON: "direction_key"） */
    @SerializedName("direction_key")
    public String directionKey;

    /** 该规则生效的开始时间，格式如 "07:00"（JSON: "start_time"） */
    @SerializedName("start_time")
    public String startTime;

    /** 该规则生效的结束时间，格式如 "09:00"（JSON: "end_time"） */
    @SerializedName("end_time")
    public String endTime;

    /** 拥挤系数，数值越大表示越拥挤 */
    public double factor;

    /** 该规则适用的星期列表，如 ["周一","周二","周三","周四","周五"] */
    public List<String> days;
}
