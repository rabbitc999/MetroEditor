package com.rabbitc.subway.data.json;

import java.util.List;

/**
 * 虚拟换乘模型，定义两个物理上不相连的站点之间的虚拟换乘关系。
 * 用于计算需出站再入站的换乘路径及时间。
 */
public class VirtualTransfer {

    /** 换乘起始站点标识符 */
    public String stationA;

    /** 换乘目的站点标识符 */
    public String stationB;

    /** 虚拟换乘所需时间（分钟） */
    public int timeMinutes;

    /** 是否需要重新安检 */
    public boolean securityCheck;

    /** 换乘途经的方向键列表，用于指定特定方向的换乘路径 */
    public List<String> viaDirections;
}
