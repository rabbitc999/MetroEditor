package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * 发车间隔规则模型，定义线路在不同时间段的发车间隔信息。
 * 支持按日期和时段设置不同的发车间隔，用于模拟和展示运营时间表。
 */
public class HeadwayRule {

    /** 线路标识符（JSON: "line_id"） */
    @SerializedName("line_id")
    public String lineId;

    /** 该规则生效的开始时间，格式如 "07:00"（JSON: "start_time"） */
    @SerializedName("start_time")
    public String startTime;

    /** 该规则生效的结束时间，格式如 "09:00"（JSON: "end_time"） */
    @SerializedName("end_time")
    public String endTime;

    /** 发车间隔（秒）（JSON: "interval_seconds"） */
    @SerializedName("interval_seconds")
    public int intervalSeconds;

    /** 该规则适用的星期列表，如 ["周一","周二","周三","周四","周五"] */
    public List<String> days;
}
