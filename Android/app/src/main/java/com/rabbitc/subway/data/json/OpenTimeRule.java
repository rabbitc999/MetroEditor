package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * 运营时间规则模型，定义线路在不同日期的首末班运营时段。
 * 用于控制线路在特定日期范围内的服务时间窗口。
 */
public class OpenTimeRule {

    /** 线路标识符（JSON: "line_id"） */
    @SerializedName("line_id")
    public String lineId;

    /** 运营开始时间，格式如 "06:00"（JSON: "start_time"） */
    @SerializedName("start_time")
    public String startTime;

    /** 运营结束时间，格式如 "23:00"（JSON: "end_time"） */
    @SerializedName("end_time")
    public String endTime;

    /** 该规则适用的星期列表，如 ["周一","周二","周三","周四","周五"] */
    public List<String> days;
}
