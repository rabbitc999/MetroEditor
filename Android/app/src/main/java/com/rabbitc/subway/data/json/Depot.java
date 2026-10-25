package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

/**
 * 车辆段/停车场数据模型，表示地铁车辆停放和检修的场所信息。
 * 包含所属线路、关联站点、发车方向及首班发车时间。
 */
public class Depot {

    /** 所属线路标识符（JSON: "line_id"） */
    @SerializedName("line_id")
    public String lineId;

    /** 关联的站点名称 */
    public String station;

    /** 出库后运行方向键（JSON: "direction_key"） */
    @SerializedName("direction_key")
    public String directionKey;

    /** 首班发车时间，格式如 "06:00"（JSON: "departure_time"） */
    @SerializedName("departure_time")
    public String departureTime;
}
