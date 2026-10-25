package com.rabbitc.subway.data.json;

/**
 * 电梯信息模型，记录站点内电梯的位置和无障碍支持情况。
 * 用于无障碍出行导航。
 */
public class ElevatorInfo {

    /** 电梯所在位置描述（如站厅、站台、出入口等） */
    public String location;

    /** 是否为无障碍电梯 */
    public boolean accessible;
}
