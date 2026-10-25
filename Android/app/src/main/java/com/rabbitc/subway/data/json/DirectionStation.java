package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

/**
 * 方向上的站点数据，表示线路中某个运行方向上一个车站的信息。
 * 包含站点名称、到下一站的距离和停站时间等运行参数。
 */
public class DirectionStation {

    /** 站点名称 */
    public String name;

    /** 到下一站的距离（米），终点站该值为 null */
    public Double next;

    /** 该站停站时间（秒）（JSON: "dwell_time"） */
    @SerializedName("dwell_time")
    public Double dwellTime;
}
