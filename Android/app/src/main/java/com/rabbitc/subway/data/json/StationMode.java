package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * 站点模式规则模型，定义站点在特定时段内的进出站模式限制。
 * 用于控制站点在高峰时段或其他特殊时段只进不出或只出不进等运营策略。
 */
public class StationMode {

    /** 适用的站点名称 */
    public String station;

    /** 进出站模式，取值如 "ENTRY_ONLY"（只进不出）、"EXIT_ONLY"（只出不进）、"BOTH"（均可） */
    public String mode;

    /** 该模式生效的开始时间，格式如 "07:00"（JSON: "start_time"） */
    @SerializedName("start_time")
    public String startTime;

    /** 该模式生效的结束时间，格式如 "09:00"（JSON: "end_time"） */
    @SerializedName("end_time")
    public String endTime;

    /** 该模式适用的星期列表，如 ["周一","周二","周三","周四","周五"] */
    public List<String> days;
}
