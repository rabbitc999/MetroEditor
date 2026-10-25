package com.rabbitc.subway.network;

import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * UDP 设备发现应答器 — 监听局域网广播，让 MetroEditor PC 端自动发现本机。
 *
 * <p>协议（UDP 端口 {@link #UDP_PORT}）：</p>
 * <pre>
 * PC → 广播: "METRO_DISCOVER_REQ"
 * 手机 → 单播回应: "METRO_DISCOVER_RSP|&lt;设备名&gt;"
 *
 * PC → 单播: "METRO_CONNECT|&lt;tcp端口&gt;"
 * 手机 → 自动向数据包来源 IP 发起 TCP 连接（显示"已连接到电脑端"）
 * </pre>
 *
 * <p>典型用法：在 MainActivity 的 onCreate 中启动、onDestroy 中停止。
 * 收到连接指令后通过 {@link OnConnectCommand} 回调（主线程）。</p>
 */
public class UdpDiscoveryResponder {

    private static final String TAG = "UdpDiscoveryResponder";

    /** UDP 发现协议端口 */
    public static final int UDP_PORT = 28081;
    /** 发现请求指令 */
    public static final String CMD_DISCOVER_REQ = "METRO_DISCOVER_REQ";
    /** 发现回应前缀 */
    public static final String CMD_DISCOVER_RSP = "METRO_DISCOVER_RSP";
    /** 连接指令前缀 */
    public static final String CMD_CONNECT = "METRO_CONNECT";

    // ======================== 回调接口 ========================

    /**
     * 收到 PC 端连接指令回调（主线程）。
     */
    public interface OnConnectCommand {
        /**
         * PC 端请求本机连接其 TCP 服务端。
         *
         * @param serverIp   PC 端 IP（数据包来源地址）
         * @param serverPort PC 端 TCP 端口
         */
        void onConnectCommand(String serverIp, int serverPort);
    }

    // ======================== 字段 ========================

    private final OnConnectCommand callback;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final AtomicBoolean running = new AtomicBoolean(false);
    private DatagramSocket socket;
    private Thread thread;

    // ======================== 构造 ========================

    /**
     * 创建 UDP 发现应答器。
     *
     * @param callback 连接指令回调（主线程回调）
     */
    public UdpDiscoveryResponder(OnConnectCommand callback) {
        this.callback = callback;
    }

    // ======================== 公共 API ========================

    /**
     * 启动 UDP 监听（异步，重复调用无效果）。
     */
    public void start() {
        if (running.get()) return;
        running.set(true);
        thread = new Thread(this::listenLoop, "UdpDiscoveryResponder");
        thread.start();
        Log.d(TAG, "UDP 发现监听已启动，端口 " + UDP_PORT);
    }

    /**
     * 停止 UDP 监听。
     */
    public void stop() {
        running.set(false);
        try {
            if (socket != null && !socket.isClosed()) socket.close();
        } catch (Exception ignored) {
        }
        if (thread != null) {
            thread.interrupt();
            thread = null;
        }
        Log.d(TAG, "UDP 发现监听已停止");
    }

    /**
     * 是否正在监听。
     */
    public boolean isRunning() {
        return running.get();
    }

    // ======================== 内部逻辑 ========================

    /**
     * 接收循环（后台线程）。
     */
    private void listenLoop() {
        try {
            socket = new DatagramSocket(UDP_PORT);
            socket.setBroadcast(true);
            byte[] buf = new byte[256];

            while (running.get()) {
                // receive 阻塞在后台线程；stop() 关闭 socket 后会自然跳出循环。
                DatagramPacket packet = new DatagramPacket(buf, buf.length);
                try {
                    socket.receive(packet);
                } catch (Exception e) {
                    if (!running.get()) break; // 正常关闭
                    continue;
                }

                String msg = new String(packet.getData(), 0, packet.getLength(),
                        StandardCharsets.UTF_8).trim();
                InetAddress sender = packet.getAddress();
                if (sender == null) continue;

                if (CMD_DISCOVER_REQ.equals(msg)) {
                    // 发现阶段只返回设备名，不建立连接，避免广播风暴。
                    // 回应发现请求：发送本机设备名
                    String rsp = CMD_DISCOVER_RSP + "|" + Build.MODEL;
                    sendTo(sender, packet.getPort(), rsp);
                    Log.d(TAG, "已回应发现请求来自 " + sender.getHostAddress());
                } else if (msg.startsWith(CMD_CONNECT + "|")) {
                    // 连接指令中的端口来自 PC，解析成功后再切回主线程交给 Activity。
                    // 解析连接指令: METRO_CONNECT|<port>
                    String portStr = msg.substring(CMD_CONNECT.length() + 1).trim();
                    try {
                        int port = Integer.parseInt(portStr);
                        String serverIp = sender.getHostAddress();
                        mainHandler.post(() -> {
                            if (callback != null) callback.onConnectCommand(serverIp, port);
                        });
                    } catch (NumberFormatException e) {
                        Log.w(TAG, "连接指令端口无效: " + msg);
                    }
                }
                // 其他消息忽略
            }
        } catch (Exception e) {
            if (running.get()) {
                Log.e(TAG, "UDP 监听异常: " + e.getMessage());
            }
        } finally {
            running.set(false);
            try {
                if (socket != null && !socket.isClosed()) socket.close();
            } catch (Exception ignored) {
            }
        }
    }

    /**
     * 向指定地址发送 UDP 文本。
     */
    private void sendTo(InetAddress address, int port, String message) {
        try {
            byte[] data = message.getBytes(StandardCharsets.UTF_8);
            DatagramPacket packet = new DatagramPacket(data, data.length, address, port);
            socket.send(packet);
        } catch (Exception e) {
            Log.e(TAG, "发送 UDP 回应失败: " + e.getMessage());
        }
    }
}
