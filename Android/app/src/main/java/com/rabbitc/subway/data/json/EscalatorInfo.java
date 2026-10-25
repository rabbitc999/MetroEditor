package com.rabbitc.subway.data.json;

/**
 * 自动扶梯信息模型，记录站点内自动扶梯的数量和运行方向。
 */
public class EscalatorInfo {

    /** 自动扶梯数量 */
    public int quantity;

    /** 自动扶梯运行方向，取值如 "up"（上行）、"down"（下行）、"both"（双向） */
    public String direction;
}
