package com.rabbitc.subway.network;

/**
 * TCP 同步回调接口，由调用方实现以接收连接状态和数据同步通知。
 *
 * <p>所有回调方法均在主线程（UI 线程）回调，可安全更新 UI。</p>
 *
 * @see TcpSyncClient
 */
public interface SyncCallback {

    /**
     * 连接成功时回调。
     *
     * @param serverName 服务端名称
     */
    void onConnected(String serverName);

    /**
     * 连接断开或连接失败时回调。
     *
     * @param reason 断开原因描述
     */
    void onDisconnected(String reason);

    /**
     * 收到地图数据并完成导入后回调。
     *
     * @param fileName 导入的文件名
     * @param success  是否导入成功
     * @param message  结果消息
     */
    void onDataSynced(String fileName, boolean success, String message);

    /**
     * 日志信息回调（用于调试和状态显示）。
     *
     * @param message 日志文本
     */
    void onLog(String message);
}
