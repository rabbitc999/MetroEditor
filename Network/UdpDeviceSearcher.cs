using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace MetroEditor.Network
{
    /// <summary>
    /// UDP 设备搜索器 — 在局域网中自动发现运行地铁查询 App 的 Android 设备，
    /// 并可向设备发送连接指令使其自动连接本机 TCP 服务端。
    ///
    /// <para>协议（UDP 端口 <see cref="UdpPort"/>）：</para>
    /// <list type="bullet">
    ///   <item>PC 广播 "METRO_DISCOVER_REQ"</item>
    ///   <item>手机单播回应 "METRO_DISCOVER_RSP|设备名"</item>
    ///   <item>PC 单播 "METRO_CONNECT|tcp端口" → 手机自动连接该端口</item>
    /// </list>
    /// </summary>
    public static class UdpDeviceSearcher
    {
        /// <summary>UDP 发现协议端口（与 Android 端 UdpDiscoveryResponder 一致）</summary>
        public const int UdpPort = 28081;

        /// <summary>
        /// 获取本机所有已启用的 IPv4 地址，多个地址以逗号分隔。
        /// </summary>
        public static string GetLocalIPv4Addresses()
        {
            var addresses = new List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            addresses.Add(addr.Address.ToString());
                    }
                }
            }
            catch
            {
                // 忽略枚举失败
            }
            return addresses.Count > 0 ? string.Join(", ", addresses) : "127.0.0.1";
        }

        private const string CmdDiscoverReq = "METRO_DISCOVER_REQ";
        private const string CmdDiscoverRsp = "METRO_DISCOVER_RSP";
        private const string CmdConnect = "METRO_CONNECT";

        /// <summary>
        /// 发现的设备信息。
        /// </summary>
        public class DiscoveredDevice
        {
            /// <summary>设备 IP 地址</summary>
            public string Ip { get; }
            /// <summary>设备名称（手机型号）</summary>
            public string Name { get; }

            /// <summary>
            /// 创建设备信息。
            /// </summary>
            public DiscoveredDevice(string ip, string name)
            {
                Ip = ip;
                Name = name;
            }

            public override string ToString() => $"{Name} ({Ip})";
        }

        /// <summary>
        /// 广播发现请求并收集设备回应。
        /// </summary>
        /// <param name="timeout">等待回应的总时长</param>
        /// <returns>发现的设备列表（按 IP 去重）</returns>
        public static async Task<List<DiscoveredDevice>> SearchDevicesAsync(TimeSpan timeout)
        {
            var devices = new List<DiscoveredDevice>();
            var seenIps = new HashSet<string>();

            using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            udp.EnableBroadcast = true;

            // 向受限广播地址和各网段广播地址发送发现请求
            var targets = new List<IPEndPoint> { new IPEndPoint(IPAddress.Broadcast, UdpPort) };
            foreach (var addr in GetInterfaceBroadcastAddresses())
                targets.Add(new IPEndPoint(addr, UdpPort));

            byte[] req = Encoding.UTF8.GetBytes(CmdDiscoverReq);
            foreach (var target in targets)
            {
                try { await udp.SendAsync(req, req.Length, target); } catch { }
            }

            // 收集回应直到超时
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var remain = deadline - DateTime.UtcNow;
                if (remain <= TimeSpan.Zero) break;

                udp.Client.ReceiveTimeout = (int)Math.Min(remain.TotalMilliseconds, 1000);
                try
                {
                    var result = await udp.ReceiveAsync();
                    string msg = Encoding.UTF8.GetString(result.Buffer).Trim();
                    if (msg.StartsWith(CmdDiscoverRsp + "|"))
                    {
                        string name = msg[(CmdDiscoverRsp.Length + 1)..];
                        string ip = result.RemoteEndPoint.Address.ToString();
                        if (seenIps.Add(ip))
                            devices.Add(new DiscoveredDevice(ip, name));
                    }
                }
                catch (SocketException)
                {
                    // 接收超时，继续等待
                }
            }

            return devices;
        }

        /// <summary>
        /// 向指定设备发送连接指令，使其自动连接本机 TCP 服务端。
        /// </summary>
        /// <param name="deviceIp">设备 IP 地址</param>
        /// <param name="tcpPort">本机 TCP 服务端端口</param>
        public static async Task SendConnectCommandAsync(string deviceIp, int tcpPort)
        {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            byte[] cmd = Encoding.UTF8.GetBytes($"{CmdConnect}|{tcpPort}");
            var target = new IPEndPoint(IPAddress.Parse(deviceIp), UdpPort);
            await udp.SendAsync(cmd, cmd.Length, target);
        }

        /// <summary>
        /// 枚举本机所有网段的广播地址。
        /// </summary>
        private static IEnumerable<IPAddress> GetInterfaceBroadcastAddresses()
        {
            var result = new List<IPAddress>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var mask = addr.IPv4Mask;
                        if (mask == null) continue;

                        // 广播地址 = 网络地址 | 主机位全 1
                        byte[] ipBytes = addr.Address.GetAddressBytes();
                        byte[] maskBytes = mask.GetAddressBytes();
                        var broadcast = new byte[4];
                        for (int i = 0; i < 4; i++)
                            broadcast[i] = (byte)(ipBytes[i] | (maskBytes[i] ^ 0xFF));
                        var bcAddr = new IPAddress(broadcast);
                        if (!bcAddr.Equals(IPAddress.Broadcast))
                            result.Add(bcAddr);
                    }
                }
            }
            catch
            {
                // 忽略枚举失败，仅使用受限广播地址
            }
            return result;
        }
    }
}
