package com.rabbitc.subway.data.json;

/**
 * 站点与线路关联模型，表示某条线路在某个站点的运行区间信息。
 * 用于描述线路经过该站时的上下行区间及运营状态。
 */
public class StationLine {

    /** 线路唯一标识符 */
    public String id;

    /** 该站往线路一个方向的下一起点站（或区间起点标识） */
    public String start;

    /** 该站往线路另一个方向的下一起点站（或区间终点标识） */
    public String end;

    /** 线路在该站的运营状态，取值如 "operating"（运营中）、"pending"（未开通）、"out_station"（站外） */
    public String status;
}
