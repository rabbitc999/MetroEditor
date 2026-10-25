package com.rabbitc.subway.data;

import java.util.List;

/**
 * 线路详情页面的显示项，用于适配器渲染。
 * <p>
 * 支持两种类型：
 * <ul>
 *     <li>type = 0：标题行（显示线路分段名称）</li>
 *     <li>type = 1：站点行（显示站点名称、序号、换乘标签等）</li>
 * </ul>
 * </p>
 */
public class DisplayItem {

    /** 类型：0 = 标题行，1 = 站点行 */
    public int type;

    /** 标题行：标题文本 */
    public String headerTitle;

    /** 标题行：标题颜色 */
    public int headerColor;

    /** 站点行：站点名称 */
    public String stationName;
    public String lineTag;

    /**
     * 站点行：站点类型（如 "main"，保留用于行内样式区分）
     */
    public String stationType;

    /** 站点行：在该方向中的序号（从 1 开始） */
    public int index;

    /** 站点行：可换乘的线路列表 */
    public List<TransferLineInfo> transfers;

    /**
     * 构造站点行（type = 1）
     *
     * @param stationName 站点名称
     * @param stationType 站点类型
     * @param index       序号
     * @param lineTag     线路标签
     */
    public DisplayItem(String stationName, String stationType, int index, String lineTag) {
        this.type = 1;
        this.stationName = stationName;
        this.stationType = stationType;
        this.index = index;
        this.lineTag = lineTag;
    }

    /**
     * 构造标题行（type = 0）
     *
     * @param headerTitle 标题文本
     * @param headerColor 标题颜色
     */
    public DisplayItem(String headerTitle, int headerColor) {
        this.type = 0;
        this.headerTitle = headerTitle;
        this.headerColor = headerColor;
        // 站点行相关字段置空
        this.stationName = null;
        this.stationType = null;
        this.index = -1;
        this.lineTag = null;
    }
}
