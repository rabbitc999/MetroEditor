package com.rabbitc.subway.data.json;

/**
 * 地图标签模型，用于在地图上标注文本信息。
 * 包含标签显示文本及其地理位置坐标。
 */
public class Label {

    /** 标签显示的文本内容 */
    public String text;

    /** 标签经度坐标（WGS84坐标系） */
    public double lon;

    /** 标签纬度坐标（WGS84坐标系） */
    public double lat;
}
