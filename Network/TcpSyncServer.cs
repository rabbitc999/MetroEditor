using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MetroEditor.Network
{
    /// <summary>
    /// TCP 同步服务端 — 将 MetroEditor 编译后的地图数据推送到已连接的 Android 客户端。
    /// <para>二进制协议（大端序，长度前缀帧）：</para>
    /// <list type="bullet">
    ///   <item>帧头: [4 bytes payload_length][payload]</item>
    ///   <item>payload 首字节为消息类型 MessageType</item>
    /// </list>
    /// </summary>
    public class TcpSyncServer : IDisposable
    {
        // ======================== 消息类型 ========================

        /// <summary>消息类型枚举</summary>
        public enum MessageType : byte
        {
            /// <summary>客户端→服务端：握手，携带客户端名称</summary>
            Hello = 0x01,
            /// <summary>服务端→客户端：欢迎，携带服务端名称</summary>
            Welcome = 0x02,
            /// <summary>服务端→客户端：推送地图数据</summary>
            SyncData = 0x03,
            /// <summary>客户端→服务端：同步结果反馈</summary>
            SyncResult = 0x04,
            /// <summary>心跳请求（双向）</summary>
            Ping = 0x05,
            /// <summary>心跳响应（双向）</summary>
            Pong = 0x06,
            /// <summary>断开通知（双向）</summary>
            Disconnect = 0x07,
            /// <summary>服务端→客户端：请求记录数据（打卡/行程），body: [1 byte recordType]</summary>
            RequestRecords = 0x08,
            /// <summary>客户端→服务端：记录数据（UTF-8 JSON），body: [1 byte recordType][4 bytes json_len][json]</summary>
            RecordsData = 0x09,
            /// <summary>服务端→客户端：推送记录数据（UTF-8 JSON），body: [1 byte recordType][4 bytes json_len][json]</summary>
            PushRecords = 0x0A,
            /// <summary>客户端→服务端：记录推送/合并结果，body: [1 byte recordType][1 byte success][2 bytes msg_len][msg]</summary>
            RecordsResult = 0x0B,
        }

        /// <summary>记录类型：站点打卡</summary>
        public const byte RecordTypeCheckIn = 1;
        /// <summary>记录类型：历史行程</summary>
        public const byte RecordTypeTripHistory = 2;

        // ======================== 事件 ========================

        /// <summary>客户端连接时触发</summary>
        public event Action<ClientSession>? ClientConnected;
        /// <summary>客户端断开时触发</summary>
        public event Action<ClientSession>? ClientDisconnected;
        /// <summary>收到客户端消息时触发</summary>
        public event Action<ClientSession, MessageType, byte[]>? MessageReceived;
        /// <summary>收到客户端记录数据（打卡/行程，UTF-8 JSON）时触发</summary>
        public event Action<ClientSession, byte, string>? RecordsDataReceived;
        /// <summary>收到客户端记录推送/合并结果时触发</summary>
        public event Action<ClientSession, byte, bool, string>? RecordsResultReceived;
        /// <summary>服务端日志输出</summary>
        public event Action<string>? Log;

        // ======================== 属性 ========================

        /// <summary>监听端口</summary>
        public int Port { get; }
        /// <summary>服务端名称（发送给客户端）</summary>
        public string ServerName { get; set; } = "MetroEditor";
        /// <summary>是否正在运行</summary>
        public bool IsRunning { get; private set; }
        /// <summary>已连接客户端数量</summary>
        public int ClientCount => _sessions.Count;

        // ======================== 字段 ========================

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly List<ClientSession> _sessions = new();
        private readonly object _lock = new();

        // ======================== 构造 ========================

        /// <summary>
        /// 创建 TCP 同步服务端实例。
        /// </summary>
        /// <param name="port">监听端口，默认 28080</param>
        public TcpSyncServer(int port = 28080)
        {
            Port = port;
        }

        // ======================== 启动/停止 ========================

        /// <summary>
        /// 启动 TCP 服务端，开始监听并接受连接。
        /// </summary>
        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            IsRunning = true;
            Log?.Invoke($"[Server] 监听已启动，端口 {Port}");

            _ = Task.Run(() => AcceptClientsAsync(_cts.Token));
        }

        /// <summary>
        /// 停止服务端，关闭所有客户端连接。
        /// </summary>
        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;
            _cts?.Cancel();
            _listener?.Stop();

            lock (_lock)
            {
                foreach (var s in _sessions)
                    s.Dispose();
                _sessions.Clear();
            }

            Log?.Invoke("[Server] 服务端已停止");
        }

        // ======================== 数据推送 ========================

        /// <summary>
        /// 向所有已连接客户端推送地图数据。
        /// </summary>
        /// <param name="fileName">目标文件名（如 "guangzhou.bmetro"）</param>
        /// <param name="data">文件原始字节数据</param>
        /// <returns>成功推送的客户端数量</returns>
        public async Task<int> BroadcastSyncDataAsync(string fileName, byte[] data)
        {
            List<ClientSession> snapshot;
            lock (_lock)
                snapshot = _sessions.ToList();

            if (snapshot.Count == 0)
            {
                Log?.Invoke("[Server] 无已连接客户端，跳过推送");
                return 0;
            }

            byte[] payload = BuildSyncDataPayload(fileName, data);
            byte[] frame = WrapFrame(payload);

            int success = 0;
            foreach (var session in snapshot)
            {
                try
                {
                    await session.SendAsync(frame);
                    success++;
                }
                catch (Exception ex)
                {
                    Log?.Invoke($"[Server] 推送到 {session} 失败: {ex.Message}");
                    RemoveSession(session);
                }
            }

            Log?.Invoke($"[Server] 已推送到 {success}/{snapshot.Count} 个客户端");
            return success;
        }

        // ======================== 记录同步（打卡/行程） ========================

        /// <summary>
        /// 向所有已连接客户端请求记录数据（客户端将回复 RecordsData 消息）。
        /// </summary>
        /// <param name="recordType">记录类型：1=打卡，2=行程</param>
        /// <returns>成功发送请求的客户端数量</returns>
        public async Task<int> RequestRecordsAsync(byte recordType)
        {
            List<ClientSession> snapshot;
            lock (_lock)
                snapshot = _sessions.ToList();

            if (snapshot.Count == 0)
            {
                Log?.Invoke("[Server] 无已连接客户端，无法请求记录数据");
                return 0;
            }

            byte[] frame = WrapFrame(new byte[] { (byte)MessageType.RequestRecords, recordType });

            int success = 0;
            foreach (var session in snapshot)
            {
                try
                {
                    await session.SendAsync(frame);
                    success++;
                }
                catch (Exception ex)
                {
                    Log?.Invoke($"[Server] 向 {session} 请求记录失败: {ex.Message}");
                    RemoveSession(session);
                }
            }

            Log?.Invoke($"[Server] 已向 {success}/{snapshot.Count} 个客户端请求记录数据（类型 {recordType}）");
            return success;
        }

        /// <summary>
        /// 向所有已连接客户端推送记录数据（客户端合并入库并回复 RecordsResult）。
        /// </summary>
        /// <param name="recordType">记录类型：1=打卡，2=行程</param>
        /// <param name="json">记录 JSON（与导出格式一致）</param>
        /// <returns>成功推送的客户端数量</returns>
        public async Task<int> PushRecordsAsync(byte recordType, string json)
        {
            List<ClientSession> snapshot;
            lock (_lock)
                snapshot = _sessions.ToList();

            if (snapshot.Count == 0)
            {
                Log?.Invoke("[Server] 无已连接客户端，跳过记录推送");
                return 0;
            }

            byte[] payload = BuildRecordsPayload(MessageType.PushRecords, recordType, json);
            byte[] frame = WrapFrame(payload);

            int success = 0;
            foreach (var session in snapshot)
            {
                try
                {
                    await session.SendAsync(frame);
                    success++;
                }
                catch (Exception ex)
                {
                    Log?.Invoke($"[Server] 向 {session} 推送记录失败: {ex.Message}");
                    RemoveSession(session);
                }
            }

            Log?.Invoke($"[Server] 已向 {success}/{snapshot.Count} 个客户端推送记录数据（类型 {recordType}）");
            return success;
        }

        /// <summary>
        /// 构建 PUSH_RECORDS / RECORDS_DATA 消息 payload:
        /// [1 byte type][1 byte recordType][4 bytes json_len][json_utf8]
        /// </summary>
        private static byte[] BuildRecordsPayload(MessageType type, byte recordType, string json)
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json ?? "");
            byte[] payload = new byte[1 + 1 + 4 + jsonBytes.Length];
            payload[0] = (byte)type;
            payload[1] = recordType;
            BitConverter.GetBytes(jsonBytes.Length).CopyTo(payload, 2);
            jsonBytes.CopyTo(payload, 6);
            return payload;
        }

        /// <summary>
        /// 解析 RECORDS_DATA / PUSH_RECORDS 消息 body:
        /// [1 byte recordType][4 bytes json_len][json_utf8]
        /// </summary>
        private static (byte recordType, string json)? ParseRecordsPayload(byte[] body)
        {
            if (body.Length < 5) return null;
            byte recordType = body[0];
            int jsonLen = BitConverter.ToInt32(body, 1);
            if (jsonLen < 0 || body.Length < 5 + jsonLen) return null;
            string json = Encoding.UTF8.GetString(body, 5, jsonLen);
            return (recordType, json);
        }

        // ======================== 内部逻辑 ========================

        private async Task AcceptClientsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    client = await _listener!.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    if (!IsRunning) break;
                    Log?.Invoke($"[Server] Accept 异常: {ex.Message}");
                    continue;
                }

                var session = new ClientSession(client);
                lock (_lock)
                    _sessions.Add(session);

                Log?.Invoke($"[Server] 客户端连接: {session}");
                ClientConnected?.Invoke(session);

                _ = HandleClientAsync(session, ct);
            }
        }

        private async Task HandleClientAsync(ClientSession session, CancellationToken ct)
        {
            var stream = session.Stream;

            try
            {
                // 发送 Welcome
                byte[] welcomePayload = new byte[1 + 2 + ServerName.Length * 3];
                welcomePayload[0] = (byte)MessageType.Welcome;
                string name = ServerName;
                byte[] nameBytes = Encoding.UTF8.GetBytes(name);
                byte[] welcome = new byte[1 + 2 + nameBytes.Length];
                welcome[0] = (byte)MessageType.Welcome;
                BitConverter.GetBytes((short)nameBytes.Length).CopyTo(welcome, 1);
                nameBytes.CopyTo(welcome, 3);
                await stream.WriteAsync(WrapFrame(welcome), ct);
                await stream.FlushAsync(ct);

                // 接收循环
                while (!ct.IsCancellationRequested && session.IsConnected)
                {
                    byte[]? payload = await ReadFrameAsync(stream, ct);
                    if (payload == null) break;

                    if (payload.Length == 0) continue;
                    var msgType = (MessageType)payload[0];
                    byte[] body = payload[1..];

                    MessageReceived?.Invoke(session, msgType, body);

                    // 处理特定消息
                    switch (msgType)
                    {
                        case MessageType.Ping:
                            await session.SendAsync(WrapFrame(new byte[] { (byte)MessageType.Pong }));
                            break;

                        case MessageType.SyncResult:
                            if (body.Length >= 1)
                            {
                                bool success = body[0] == 1;
                                string msg = "";
                                if (body.Length >= 3)
                                {
                                    short msgLen = BitConverter.ToInt16(body, 1);
                                    if (body.Length >= 3 + msgLen)
                                        msg = Encoding.UTF8.GetString(body, 3, msgLen);
                                }
                                Log?.Invoke($"[Server] {session} 同步结果: {(success ? "成功" : "失败")} {msg}");
                            }
                            break;

                        case MessageType.RecordsData:
                        {
                            var parsed = ParseRecordsPayload(body);
                            if (parsed != null)
                            {
                                Log?.Invoke($"[Server] 收到 {session} 的记录数据（类型 {parsed.Value.recordType}）");
                                RecordsDataReceived?.Invoke(session, parsed.Value.recordType, parsed.Value.json);
                            }
                            else
                            {
                                Log?.Invoke($"[Server] {session} 的记录数据格式错误");
                            }
                            break;
                        }

                        case MessageType.RecordsResult:
                        {
                            // [1 recordType][1 success][2 msg_len][msg]
                            if (body.Length >= 4)
                            {
                                byte recordType = body[0];
                                bool success = body[1] == 1;
                                string msg = "";
                                short msgLen = BitConverter.ToInt16(body, 2);
                                if (body.Length >= 4 + msgLen)
                                    msg = Encoding.UTF8.GetString(body, 4, msgLen);
                                Log?.Invoke($"[Server] {session} 记录合并结果（类型 {recordType}）: {(success ? "成功" : "失败")} {msg}");
                                RecordsResultReceived?.Invoke(session, recordType, success, msg);
                            }
                            break;
                        }

                        case MessageType.Disconnect:
                            Log?.Invoke($"[Server] {session} 请求断开");
                            break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log?.Invoke($"[Server] {session} 异常: {ex.Message}");
            }
            finally
            {
                RemoveSession(session);
            }
        }

        private void RemoveSession(ClientSession session)
        {
            lock (_lock)
                _sessions.Remove(session);

            try { session.Dispose(); } catch { }
            Log?.Invoke($"[Server] 客户端断开: {session}");
            ClientDisconnected?.Invoke(session);
        }

        // ======================== 协议帧编解码 ========================

        /// <summary>
        /// 读取一帧数据：先读 4 字节长度，再读对应字节的 payload。
        /// </summary>
        private static async Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationToken ct)
        {
            byte[] lenBuf = new byte[4];
            if (!await ReadExactAsync(stream, lenBuf, 4, ct))
                return null;

            int length = BitConverter.ToInt32(lenBuf, 0);
            if (length <= 0 || length > 100 * 1024 * 1024) // 限制 100MB
                return null;

            byte[] payload = new byte[length];
            if (!await ReadExactAsync(stream, payload, length, ct))
                return null;

            return payload;
        }

        /// <summary>
        /// 精确读取指定字节数。
        /// </summary>
        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
                if (read == 0) return false;
                offset += read;
            }
            return true;
        }

        /// <summary>
        /// 将 payload 包装成帧: [4字节长度][payload]
        /// </summary>
        private static byte[] WrapFrame(byte[] payload)
        {
            byte[] frame = new byte[4 + payload.Length];
            BitConverter.GetBytes(payload.Length).CopyTo(frame, 0);
            payload.CopyTo(frame, 4);
            return frame;
        }

        /// <summary>
        /// 构建 SYNC_DATA 消息 payload:
        /// [1 byte type][2 bytes filename_len][filename_utf8][4 bytes data_len][data]
        /// </summary>
        private static byte[] BuildSyncDataPayload(string fileName, byte[] data)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(fileName);
            byte[] payload = new byte[1 + 2 + nameBytes.Length + 4 + data.Length];
            payload[0] = (byte)MessageType.SyncData;
            BitConverter.GetBytes((short)nameBytes.Length).CopyTo(payload, 1);
            nameBytes.CopyTo(payload, 3);
            BitConverter.GetBytes(data.Length).CopyTo(payload, 3 + nameBytes.Length);
            data.CopyTo(payload, 3 + nameBytes.Length + 4);
            return payload;
        }

        // ======================== IDisposable ========================

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }

    // ======================== 客户端会话 ========================

    /// <summary>
    /// 表示一个已连接的客户端会话。
    /// </summary>
    public class ClientSession : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;

        /// <summary>远程端点地址</summary>
        public string RemoteEndPoint { get; }
        /// <summary>客户端名称（从 HELLO 消息获取）</summary>
        public string ClientName { get; set; } = "";
        /// <summary>连接时间</summary>
        public DateTime ConnectedAt { get; } = DateTime.Now;
        /// <summary>是否仍连接</summary>
        public bool IsConnected => _client.Connected;

        internal NetworkStream Stream => _stream;

        /// <summary>
        /// 创建客户端会话。
        /// </summary>
        public ClientSession(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
            RemoteEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        }

        /// <summary>
        /// 发送原始帧数据。
        /// </summary>
        public async Task SendAsync(byte[] frame)
        {
            await _stream.WriteAsync(frame);
            await _stream.FlushAsync();
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(ClientName)
                ? RemoteEndPoint
                : $"{ClientName} ({RemoteEndPoint})";
        }

        public void Dispose()
        {
            try { _stream?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }
        }
    }
}
