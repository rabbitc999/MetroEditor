package com.rabbitc.subway.data.parse;

import java.util.List;

/**
 * 虚拟换乘规则，由 {@code DataParser} 从原始 JSON 解析得到，表示两个车站之间的虚拟换乘信息。
 * 存储于 {@code DataCache} 中供运行时路径规划使用。
 */
public class VirtualTransfer {
    /** 换乘起点车站名称 */
    public String stationA;
    /** 换乘终点车站名称 */
    public String stationB;
    /** 换乘步行时间（分钟） */
    public int timeMinutes;
    /** 是否需要重新安检 */
    public boolean securityCheck;
    /** 换乘途经方向指引列表 */
    public List<String> viaDirections;
}
