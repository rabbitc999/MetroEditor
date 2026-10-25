package com.rabbitc.subway.network;

import android.content.Context;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import com.rabbitc.subway.data.DataLoader;
import com.rabbitc.subway.utils.CheckInManager;
import com.rabbitc.subway.utils.TripHistoryManager;

import java.io.ByteArrayInputStream;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * TCP 同步客户端 — 连接 MetroEditor 服务端，接收推送的地图数据并自动导入。
 *
 * <p>二进制协议（与服务端 TcpSyncServer.cs 一致，小端序）：</p>
 * <pre>
 * 帧格式: [4 bytes payload_length] [payload]
 * payload 首字节为消息类型:
 *   0x02 Welcome   — 服务端→客户端，携带服务端名称
 *   0x03 SyncData  — 服务端→客户端，携带文件名+数据
 *   0x05 Ping      — 心跳请求（双向）
 *   0x06 Pong      — 心跳响应（双向）
 *   0x07 Disconnect — 断开通知（双向）
 *   0x08 RequestRecords — 服务端→客户端，请求记录数据（打卡/行程）
 *   0x09 RecordsData    — 客户端→服务端，记录数据（UTF-8 JSON）
 *   0x0A PushRecords    — 服务端→客户端，推送记录数据（UTF-8 JSON，客户端合并入库）
 *   0x0B RecordsResult  — 客户端→服务端，记录推送/合并结果
 * </pre>
 *
 * <p>典型用法：</p>
 * <pre>{@code
 * TcpSyncClient client = new TcpSyncClient(context, callback);
 * client.connect("192.168.1.100", 28080);
 * // ... 接收数据自动导入 ...
 * client.disconnect();
 * }</pre>
 *
 * @see SyncCallback
 * @see DataLoader#importMapFromStream(Context, InputStream, String)
 */
public class TcpSyncClient {

    private static final String TAG = "TcpSyncClient";

    // ======================== 消息类型常量 ========================

    private static final byte MSG_HELLO = 0x01;
    private static final byte MSG_WELCOME = 0x02;
    private static final byte MSG_SYNC_DATA = 0x03;
    private static final byte MSG_SYNC_RESULT = 0x04;
    private static final byte MSG_PING = 0x05;
    private static final byte MSG_PONG = 0x06;
    private static final byte MSG_DISCONNECT = 0x07;
    /** 服务端→客户端：请求记录数据（打卡/行程） */
    private static final byte MSG_REQUEST_RECORDS = 0x08;
    /** 客户端→服务端：记录数据（UTF-8 JSON） */
    private static final byte MSG_RECORDS_DATA = 0x09;
    /** 服务端→客户端：推送记录数据（UTF-8 JSON） */
    private static final byte MSG_PUSH_RECORDS = 0x0A;
    /** 客户端→服务端：记录推送/合并结果 */
    private static final byte MSG_RECORDS_RESULT = 0x0B;

    // ---- 记录类型 ----
    /** 站点打卡记录 */
    private static final byte RECORD_TYPE_CHECKIN = 1;
    /** 历史行程记录 */
    private static final byte RECORD_TYPE_TRIP = 2;

    // ======================== 字段 ========================

    private final Context context;
    private final SyncCallback callback;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final AtomicBoolean running = new AtomicBoolean(false);
    private final AtomicBoolean connected = new AtomicBoolean(false);

    private Socket socket;
    private DataInputStream input;
    private DataOutputStream output;
    private String serverName = "";

    // ======================== 构造 ========================

    /**
     * 创建 TCP 同步客户端实例。
     *
     * @param context  Android 上下文（用于 DataLoader 导入数据）
     * @param callback 同步回调接口
     */
    public TcpSyncClient(Context context, SyncCallback callback) {
        this.context = context.getApplicationContext();
        this.callback = callback;
    }

    // ======================== 公共 API ========================

    /**
     * 连接到 MetroEditor TCP 服务端。
     *
     * <p>异步执行，连接结果通过 {@link SyncCallback#onConnected(String)} 或
     * {@link SyncCallback#onDisconnected(String)} 回调通知。</p>
     *
     * @param host 服务端 IP 地址
     * @param port 服务端端口
     */
    public void connect(String host, int port) {
        if (running.get()) {
            notifyLog("已在连接中，请先断开");
            return;
        }

        running.set(true);
        executor.execute(() -> doConnect(host, port));
    }

    /**
     * 断开与服务端的连接。
     */
    public void disconnect() {
        running.set(false);
        closeSocket();
    }

    /**
     * 是否已连接。
     *
     * @return {@code true} 如果当前已连接到服务端
     */
    public boolean isConnected() {
        return connected.get();
    }

    /**
     * 获取服务端名称。
     *
     * @return 服务端名称（连接成功后有效，否则为空字符串）
     */
    public String getServerName() {
        return serverName;
    }

    // ======================== 连接与接收逻辑 ========================

    /**
     * 执行连接和接收循环（在后台线程运行）。
     */
    private void doConnect(String host, int port) {
        try {
            socket = new Socket();
            socket.connect(new InetSocketAddress(host, port), 5000);
            socket.setSoTimeout(0); // 无超时，持续等待数据

            input = new DataInputStream(socket.getInputStream());
            output = new DataOutputStream(socket.getOutputStream());

            connected.set(true);
            notifyLog("已连接到 " + host + ":" + port);

            // 发送 HELLO
            sendHello("Android-Subway");

            // 接收循环
            while (running.get() && connected.get()) {
                // 每帧先读取长度，再按长度读取完整 payload，避免 TCP 分包造成消息边界错乱。
                byte[] payload = readFrame();
                if (payload == null || payload.length == 0) {
                    break;
                }

                byte msgType = payload[0];
                // 类型字节只负责路由，剩余字节交给对应处理器解析具体协议。
                byte[] body = new byte[payload.length - 1];
                System.arraycopy(payload, 1, body, 0, body.length);

                handleMessage(msgType, body);
            }
        } catch (Exception e) {
            notifyLog("连接异常: " + e.getMessage());
        } finally {
            connected.set(false);
            running.set(false);
            closeSocket();
            notifyDisconnected("连接已断开");
        }
    }

    /**
     * 处理收到的消息。
     */
    private void handleMessage(byte msgType, byte[] body) throws IOException {
        switch (msgType) {
            case MSG_WELCOME:
                // [2 bytes name_len][name_utf8]
                if (body.length >= 2) {
                    short nameLen = (short) ((body[0] & 0xFF) | ((body[1] & 0xFF) << 8));
                    if (body.length >= 2 + nameLen) {
                        serverName = new String(body, 2, nameLen, StandardCharsets.UTF_8);
                        notifyConnected(serverName);
                    }
                }
                break;

            case MSG_SYNC_DATA:
                // [2 bytes filename_len][filename_utf8][4 bytes data_len][data]
                // 地图文件导入在后台线程完成，回调统一切回主线程更新界面。
                handleSyncData(body);
                break;

            case MSG_PING:
                sendPong();
                break;

            case MSG_PONG:
                // 心跳响应，忽略
                break;

            case MSG_REQUEST_RECORDS:
                // 服务端请求记录数据（打卡/行程），导出并以 RECORDS_DATA 回复
                if (body.length >= 1) {
                    handleRequestRecords(body[0]);
                }
                break;

            case MSG_PUSH_RECORDS:
                // 服务端推送记录数据，合并入库并以 RECORDS_RESULT 回复
                // 记录同步按站点/时间戳去重，不会覆盖本地已有内容。
                handlePushRecords(body);
                break;

            case MSG_DISCONNECT:
                notifyLog("服务端请求断开");
                running.set(false);
                break;

            default:
                notifyLog("未知消息类型: 0x" + Integer.toHexString(msgType & 0xFF));
                break;
        }
    }

    /**
     * 处理 SYNC_DATA 消息：解析文件名和数据，调用 DataLoader 导入。
     */
    private void handleSyncData(byte[] body) {
        try {
            if (body.length < 2) {
                sendSyncResult(false, "数据格式错误");
                return;
            }

            int pos = 0;
            // 小端序读取 short
            short fileNameLen = (short) ((body[pos] & 0xFF) | ((body[pos + 1] & 0xFF) << 8));
            pos += 2;

            if (body.length < pos + fileNameLen + 4) {
                sendSyncResult(false, "数据不完整");
                return;
            }

            String fileName = new String(body, pos, fileNameLen, StandardCharsets.UTF_8);
            pos += fileNameLen;

            // 小端序读取 int
            int dataLen = (body[pos] & 0xFF) |
                    ((body[pos + 1] & 0xFF) << 8) |
                    ((body[pos + 2] & 0xFF) << 16) |
                    ((body[pos + 3] & 0xFF) << 24);
            pos += 4;

            if (body.length < pos + dataLen) {
                sendSyncResult(false, "数据长度不匹配");
                return;
            }

            byte[] data = new byte[dataLen];
            System.arraycopy(body, pos, data, 0, dataLen);

            notifyLog("收到数据: " + fileName + " (" + dataLen + " bytes)");

            // 通过 DataLoader 导入到内部存储
            boolean success = DataLoader.importMapFromStream(
                    context, new ByteArrayInputStream(data), fileName);

            String message = success ? "导入成功" : "导入失败";
            sendSyncResult(success, message);
            notifyDataSynced(fileName, success, message);

        } catch (Exception e) {
            Log.e(TAG, "处理同步数据失败", e);
            sendSyncResult(false, e.getMessage() != null ? e.getMessage() : "处理异常");
            notifyDataSynced("unknown", false, e.getMessage() != null ? e.getMessage() : "处理异常");
        }
    }

    /**
     * 处理 REQUEST_RECORDS 消息：按记录类型导出本地数据，以 RECORDS_DATA 回复服务端。
     *
     * <p>消息格式：请求 [type][1 byte recordType]；
     * 回复 [type][1 byte recordType][4 bytes json_len (小端)][json_utf8]。</p>
     *
     * @param recordType 记录类型：1=打卡，2=行程
     */
    private void handleRequestRecords(byte recordType) {
        String json;
        String typeName;
        if (recordType == RECORD_TYPE_CHECKIN) {
            json = new CheckInManager(context).exportAsJson();
            typeName = "打卡记录";
        } else {
            json = new TripHistoryManager(context).exportAsJson();
            typeName = "行程记录";
        }
        try {
            byte[] jsonBytes = json.getBytes(StandardCharsets.UTF_8);
            byte[] payload = new byte[1 + 1 + 4 + jsonBytes.length];
            payload[0] = MSG_RECORDS_DATA;
            payload[1] = recordType;
            payload[2] = (byte) (jsonBytes.length & 0xFF);
            payload[3] = (byte) ((jsonBytes.length >> 8) & 0xFF);
            payload[4] = (byte) ((jsonBytes.length >> 16) & 0xFF);
            payload[5] = (byte) ((jsonBytes.length >> 24) & 0xFF);
            System.arraycopy(jsonBytes, 0, payload, 6, jsonBytes.length);
            sendFrame(payload);
            notifyLog("已发送" + typeName + " (" + jsonBytes.length + " bytes)");
        } catch (IOException e) {
            Log.e(TAG, "发送" + typeName + "失败", e);
            notifyLog("发送" + typeName + "失败: " + e.getMessage());
        }
    }

    /**
     * 处理 PUSH_RECORDS 消息：解析服务端推送的记录 JSON，合并入库后以 RECORDS_RESULT 回复。
     *
     * <p>消息格式：推送 [type][1 byte recordType][4 bytes json_len (小端)][json_utf8]；
     * 回复 [type][1 byte recordType][1 byte success][2 bytes msg_len (小端)][msg_utf8]。</p>
     */
    private void handlePushRecords(byte[] body) {
        String resultMsg;
        boolean success = false;
        byte recordType = body.length >= 1 ? body[0] : RECORD_TYPE_CHECKIN;

        try {
            if (body.length < 5) {
                resultMsg = "数据格式错误";
            } else {
                // 小端序读取 int
                int jsonLen = (body[1] & 0xFF) |
                        ((body[2] & 0xFF) << 8) |
                        ((body[3] & 0xFF) << 16) |
                        ((body[4] & 0xFF) << 24);
                if (body.length < 5 + jsonLen) {
                    resultMsg = "数据长度不匹配";
                } else {
                    String json = new String(body, 5, jsonLen, StandardCharsets.UTF_8);
                    int added;
                    String typeName;
                    if (recordType == RECORD_TYPE_CHECKIN) {
                        added = new CheckInManager(context).mergeFromJson(json);
                        typeName = "打卡记录";
                    } else {
                        added = new TripHistoryManager(context).mergeFromJson(json);
                        typeName = "行程记录";
                    }
                    if (added < 0) {
                        resultMsg = typeName + "JSON解析失败";
                    } else {
                        success = true;
                        resultMsg = typeName + "合并完成，新增 " + added + " 条";
                    }
                }
            }
        } catch (Exception e) {
            Log.e(TAG, "处理推送记录失败", e);
            resultMsg = "处理异常: " + (e.getMessage() != null ? e.getMessage() : "unknown");
        }

        sendRecordsResult(recordType, success, resultMsg);
        notifyLog((success ? "✅ " : "❌ ") + resultMsg);
    }

    /**
     * 发送 RECORDS_RESULT 消息:
     * [type][1 byte recordType][1 byte success][2 bytes msg_len (小端)][msg_utf8]
     */
    private void sendRecordsResult(byte recordType, boolean success, String message) {
        if (!connected.get()) return;
        try {
            byte[] msgBytes = (message != null ? message : "").getBytes(StandardCharsets.UTF_8);
            byte[] payload = new byte[1 + 1 + 1 + 2 + msgBytes.length];
            payload[0] = MSG_RECORDS_RESULT;
            payload[1] = recordType;
            payload[2] = (byte) (success ? 1 : 0);
            payload[3] = (byte) (msgBytes.length & 0xFF);
            payload[4] = (byte) ((msgBytes.length >> 8) & 0xFF);
            System.arraycopy(msgBytes, 0, payload, 5, msgBytes.length);
            sendFrame(payload);
        } catch (IOException e) {
            Log.e(TAG, "发送记录结果失败", e);
        }
    }

    // ======================== 协议帧编解码 ========================

    /**
     * 读取一帧：先读 4 字节长度（小端序），再读对应字节的 payload。
     *
     * @return payload 字节数组，或 {@code null} 如果连接断开
     */
    private byte[] readFrame() throws IOException {
        // 读取 4 字节长度（C# 端 BitConverter 是小端序）
        byte[] lenBuf = new byte[4];
        if (!readExact(lenBuf, 4))
            return null;

        // 小端序解析
        int length = (lenBuf[0] & 0xFF) |
                ((lenBuf[1] & 0xFF) << 8) |
                ((lenBuf[2] & 0xFF) << 16) |
                ((lenBuf[3] & 0xFF) << 24);

        if (length <= 0 || length > 100 * 1024 * 1024) {
            notifyLog("无效帧长度: " + length);
            return null;
        }

        byte[] payload = new byte[length];
        if (!readExact(payload, length))
            return null;
        return payload;
    }

    /**
     * 精确读取指定字节数。
     *
     * @param buffer 目标缓冲区
     * @param count  要读取的字节数
     * @return {@code true} 读取成功，{@code false} 连接断开
     */
    private boolean readExact(byte[] buffer, int count) throws IOException {
        int offset = 0;
        while (offset < count) {
            int read = input.read(buffer, offset, count - offset);
            if (read == -1) return false;
            offset += read;
        }
        return true;
    }

    /**
     * 发送一帧：写入 [4字节长度(小端)][payload]。
     */
    private void sendFrame(byte[] payload) throws IOException {
        // C# 端用 BitConverter.GetBytes(int) 读取，是小端序
        // Java 端需要写入小端序的长度
        int length = payload.length;
        output.writeByte(length & 0xFF);
        output.writeByte((length >> 8) & 0xFF);
        output.writeByte((length >> 16) & 0xFF);
        output.writeByte((length >> 24) & 0xFF);
        output.write(payload);
        output.flush();
    }

    /**
     * 发送 HELLO 消息: [type][2 bytes name_len][name_utf8]
     */
    private void sendHello(String clientName) throws IOException {
        byte[] nameBytes = clientName.getBytes(StandardCharsets.UTF_8);
        byte[] payload = new byte[1 + 2 + nameBytes.length];
        payload[0] = MSG_HELLO;
        payload[1] = (byte) (nameBytes.length & 0xFF);
        payload[2] = (byte) ((nameBytes.length >> 8) & 0xFF);
        System.arraycopy(nameBytes, 0, payload, 3, nameBytes.length);
        sendFrame(payload);
    }

    /**
     * 发送 SYNC_RESULT 消息: [type][1 byte success][2 bytes msg_len][msg_utf8]
     */
    private void sendSyncResult(boolean success, String message) {
        if (!connected.get()) return;
        try {
            byte[] msgBytes = (message != null ? message : "").getBytes(StandardCharsets.UTF_8);
            byte[] payload = new byte[1 + 1 + 2 + msgBytes.length];
            payload[0] = MSG_SYNC_RESULT;
            payload[1] = (byte) (success ? 1 : 0);
            payload[2] = (byte) (msgBytes.length & 0xFF);
            payload[3] = (byte) ((msgBytes.length >> 8) & 0xFF);
            System.arraycopy(msgBytes, 0, payload, 4, msgBytes.length);
            sendFrame(payload);
        } catch (IOException e) {
            Log.e(TAG, "发送同步结果失败", e);
        }
    }

    /**
     * 发送 PONG 消息。
     */
    private void sendPong() throws IOException {
        sendFrame(new byte[]{MSG_PONG});
    }

    // ======================== 工具方法 ========================

    /**
     * 关闭 Socket 和流。
     */
    private void closeSocket() {
        try { if (input != null) input.close(); } catch (Exception ignored) {}
        try { if (output != null) output.close(); } catch (Exception ignored) {}
        try { if (socket != null && !socket.isClosed()) socket.close(); } catch (Exception ignored) {}
    }

    // ======================== 主线程回调 ========================

    private void notifyConnected(String name) {
        mainHandler.post(() -> callback.onConnected(name));
    }

    private void notifyDisconnected(String reason) {
        mainHandler.post(() -> callback.onDisconnected(reason));
    }

    private void notifyDataSynced(String fileName, boolean success, String message) {
        mainHandler.post(() -> callback.onDataSynced(fileName, success, message));
    }

    private void notifyLog(String message) {
        Log.d(TAG, message);
        mainHandler.post(() -> callback.onLog(message));
    }
}
