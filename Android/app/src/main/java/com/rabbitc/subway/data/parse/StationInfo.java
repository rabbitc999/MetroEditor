package com.rabbitc.subway.data.parse;

import java.util.Set;

/**
 * 车站基本信息，由 {@code DataParser} 从原始 JSON 解析得到，存储于 {@code DataCache} 中供运行时快速访问。
 * 包含车站名称和该车站所属的线路类型集合。
 */
public class StationInfo {
    /** 车站名称 */
    public String name;
    /** 车站所属线路类型集合 */
    public Set<String> types;

    public StationInfo(String name, Set<String> types) {
        this.name = name;
        this.types = types;
    }
}
