package com.rabbitc.subway.listener;

/**
 * 站点操作事件回调接口，包含点击和长按两种交互。
 * <p>
 * 用于列表项中站点的交互处理：
 * <ul>
 *     <li>点击：通常用于在地图上高亮该站点</li>
 *     <li>长按：通常用于弹出设置起点/终点的对话框</li>
 * </ul>
 * </p>
 */
public interface OnStationActionListener {

    /**
     * 当用户单击某个站点时触发。
     *
     * @param stationName 被单击的站点名称
     */
    void onStationClick(String stationName);

    /**
     * 当用户长按某个站点时触发。
     *
     * @param stationName 被长按的站点名称
     */
    void onStationLongClick(String stationName);
}