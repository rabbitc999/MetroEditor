package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * 线路运行方向模型，定义一条地铁线路某一运行方向的详细信息。
 * 包含方向类型（快车/环线）、起止站点、途经站点列表以及衔接站点信息。
 */
public class Direction {

    /** 该方向是否为快车/大站车（JSON: "is_fast"） */
    @SerializedName("is_fast")
    public boolean isFast;

    /** 该方向的起始站点名称 */
    public String start;

    /** 该方向的终点站点名称 */
    public String end;

    /** 该方向是否为环线（JSON: "is_loop"） */
    @SerializedName("is_loop")
    public boolean isLoop;

    /** 环线方向类型，取值如 "inner"（内环）或 "outer"（外环）（JSON: "direction_type"） */
    @SerializedName("direction_type")
    public String directionType;

    /** 该方向上按顺序排列的车站列表 */
    public List<DirectionStation> station;

    /** 是否为远期规划方向（JSON: "is_future"） */
    @SerializedName("is_future")
    public boolean isFuture;

    /** 该方向衔接的另一线路站点（用于汇入其他线路等场景）（JSON: "connect_station"） */
    @SerializedName("connect_station")
    public String connectStation;

    /** 该方向经过的其他方向键列表，用于复杂线路的路径描述（JSON: "via_directions"） */
    @SerializedName("via_directions")
    public List<String> viaDirections;
}
