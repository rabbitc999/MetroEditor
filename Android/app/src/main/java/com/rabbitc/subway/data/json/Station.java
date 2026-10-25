package com.rabbitc.subway.data.json;

import java.util.List;
import java.util.Map;

/**
 * 地铁站点数据模型，表示一个地铁车站的基本信息。
 * 包含地理位置坐标、所属城市、途经线路以及换乘信息。
 * 通过 Metro Editor 桌面工具导出。
 */
public class Station {

    /** 站点经度（WGS84坐标系） */
    public double lon;

    /** 站点纬度（WGS84坐标系） */
    public double lat;

    /** 站点所属城市名称 */
    public String city;

    /** 该站点途经的线路列表 */
    public List<StationLine> lines;
    /**
     * ⚠️ 特殊处理：
     * 这里 JSON 可能是 "operating" 字符串，也可能是包含 "status"/"time" 的对象。
     * 用 Map<String, Object> 最为安全，在后续 DataParser 遍历时通过 instanceof 判断。
     */
    public Map<String, Object> transfers;
}
