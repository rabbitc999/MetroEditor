package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * 地铁线路数据模型，是线路的全量属性定义。
 * 包含线路基本属性（名称、颜色、编组）、速度参数（最高速度、牵引功率、制动加速度）、
 * 线路开通年份、所属城市以及各运行方向的详细信息。
 */
public class Line {

    /** 线路标识颜色（十六进制色值） */
    public String color;

    /** 列车编组形式（如 "6A" 表示6节A型车编组） */
    public String formation;

    /** 线路开通年份（JSON: "open_year"） */
    @SerializedName("open_year")
    public int openYear;

    /** 线路所属城市 */
    public String city;

    /** 线路最高运行速度（公里/小时） */
    public int maxspeed;

    /** 线路相关链接（如官网介绍页面） */
    public String link;

    /** 线路名称 */
    public String name;

    /** 线路英文名称（JSON: "english_name"） */
    @SerializedName("english_name")
    public String englishName;

    /** 列车牵引功率（千瓦）（JSON: "power_kw"） */
    @SerializedName("power_kw")
    public double powerKw;

    /** 列车制动加速度（m/s²），用于运行模拟计算 */
    public double brake;

    /** 该线路是否已停用 */
    public boolean disabled;

    /** 是否为特殊线路（如机场线、磁浮线等有独立计费规则的线路） */
    public boolean special;

    /** 线路运行方向列表，每个方向包含该方向的站点序列和属性 */
    public List<Direction> direction;
}
