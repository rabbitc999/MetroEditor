package com.rabbitc.subway.listener;

/**
 * 站点长按事件回调接口。
 * <p>
 * 用于处理在地图或列表视图中长按站点名称时的交互逻辑，
 * 例如弹出设置起点/终点的对话框。
 * </p>
 */
public interface OnStationLongClickListener {

    /**
     * 当用户长按某个站点时触发。
     *
     * @param stationName 被长按的站点名称
     */
    void onStationLongClick(String stationName);
}