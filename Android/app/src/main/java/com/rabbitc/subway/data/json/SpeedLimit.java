package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

/**
 * 限速规则模型，定义线路中特定区段的速度限制。
 * 用于模拟列车运行时的实际限速情况。
 */
public class SpeedLimit {

    /** 运行方向键（JSON: "direction_key"） */
    @SerializedName("direction_key")
    public String directionKey;

    /** 限速区段起始站点（JSON: "from_station"） */
    @SerializedName("from_station")
    public String fromStation;

    /** 限速区段终止站点（JSON: "to_station"） */
    @SerializedName("to_station")
    public String toStation;

    /** 该区段的限速值（公里/小时）（JSON: "speed_kmh"） */
    @SerializedName("speed_kmh")
    public int speedKmh;
}
