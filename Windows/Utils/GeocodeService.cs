using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MetroEditor.Windows.Utils
{
    /// <summary>
    /// 高德地图地理编码服务封装类
    /// 提供地址解析、POI搜索和坐标转换功能，内置重试机制和异常处理
    /// 支持 GCJ-02（火星坐标系）到 WGS-84（GPS坐标系）的转换
    /// </summary>
    /// <remarks>
    /// 使用示例：
    /// using var service = new GeocodeService("your_amap_api_key");
    /// var (lng, lat, address) = await service.GetLocationAsync("北京市天安门");
    /// </remarks>
    public class GeocodeService : IDisposable
    {
        #region 常量定义
        /// <summary>地理编码API端点</summary>
        private const string API_URL = "https://restapi.amap.com/v3/geocode/geo";

        /// <summary>POI搜索API端点（用于地铁站搜索）</summary>
        private const string POI_URL = "https://restapi.amap.com/v3/place/text";

        /// <summary>最大重试次数（应对临时性网络故障）</summary>
        private const int MAX_RETRIES = 10;

        /// <summary>初始退避时间（秒），用于指数退避算法</summary>
        private const int INITIAL_BACKOFF_SECONDS = 1;

        // ===== 坐标转换算法常量（GCJ-02 → WGS-84）=====
        /// <summary>克拉索夫斯基椭球体长半轴</summary>
        private const double A = 6378245.0;

        /// <summary>第一偏心率平方</summary>
        private const double EE = 0.00669342162296594323;

        /// <summary>圆周率π的近似值</summary>
        private const double PI = Math.PI;
        #endregion

        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private bool _disposed;

        /// <summary>
        /// 初始化高德地图地理编码服务
        /// </summary>
        /// <param name="apiKey">高德开放平台API密钥</param>
        /// <exception cref="ArgumentException">当API Key为空或空白时抛出</exception>
        public GeocodeService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API Key 不能为空", nameof(apiKey));

            _apiKey = apiKey;
            // 配置HttpClient超时时间为30秒，避免长时间阻塞
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        // ===== 公开API方法 =====

        /// <summary>
        /// 将结构化地址转换为经纬度坐标
        /// </summary>
        /// <param name="address">结构化地址（如"北京市朝阳区阜通东大街6号"）</param>
        /// <param name="city">指定查询城市（可选，提高查询精度）</param>
        /// <param name="convertToWgs84">是否转换为WGS-84坐标系（默认false，返回GCJ-02）</param>
        /// <returns>
        /// 包含经度、纬度和格式化地址的元组
        /// </returns>
        /// <exception cref="ArgumentException">当地址为空时抛出</exception>
        /// <exception cref="InvalidOperationException">当API返回错误或找不到地址时抛出</exception>
        /// <example>
        /// var (lng, lat, addr) = await GetLocationAsync("上海火车站", "上海");
        /// </example>
        public async Task<(double Lng, double Lat, string FormattedAddress)> GetLocationAsync(
            string address, string city = null, bool convertToWgs84 = false)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("地址不能为空", nameof(address));

            // 构建带参数的API请求URL
            string url = $"{API_URL}?address={Uri.EscapeDataString(address)}&output=JSON&key={_apiKey}";
            if (!string.IsNullOrWhiteSpace(city))
                url += $"&city={Uri.EscapeDataString(city)}";

            // 使用重试机制执行请求
            return await ExecuteWithRetryAsync(async () =>
                await DoGeocodeRequestAsync(url, address, convertToWgs84));
        }

        /// <summary>
        /// 搜索地铁站POI并获取坐标
        /// </summary>
        /// <param name="stationName">地铁站名称（如"人民广场站"）</param>
        /// <param name="city">指定查询城市（可选，提高查询精度）</param>
        /// <param name="convertToWgs84">是否转换为WGS-84坐标系（默认false，返回GCJ-02）</param>
        /// <returns>
        /// 包含经度、纬度和标准化站名的元组
        /// </returns>
        /// <exception cref="ArgumentException">当站名为空时抛出</exception>
        /// <exception cref="InvalidOperationException">当API返回错误或找不到地铁站时抛出</exception>
        /// <example>
        /// var (lng, lat, name) = await SearchSubwayStationAsync("西直门站", "北京");
        /// </example>
        public async Task<(double Lng, double Lat, string Name)> SearchSubwayStationAsync(
            string stationName, string city = null, bool convertToWgs84 = false)
        {
            if (string.IsNullOrWhiteSpace(stationName))
                throw new ArgumentException("站名不能为空", nameof(stationName));

            // 构建POI搜索URL，types=150500表示地铁站分类
            string url = $"{POI_URL}?keywords={Uri.EscapeDataString(stationName)}&types=150500&output=JSON&key={_apiKey}";
            if (!string.IsNullOrWhiteSpace(city))
                url += $"&city={Uri.EscapeDataString(city)}";

            // 使用重试机制执行请求
            return await ExecuteWithRetryAsync(async () =>
                await DoPoiSearchAsync(url, convertToWgs84));
        }

        // ===== 核心请求方法（不含重试逻辑）=====

        /// <summary>
        /// 执行地理编码API请求的具体实现
        /// </summary>
        private async Task<(double Lng, double Lat, string FormattedAddress)> DoGeocodeRequestAsync(
            string url, string address, bool convertToWgs84)
        {
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode(); // 确保HTTP响应成功

            string content = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;

            // 验证API返回状态
            if (!root.TryGetProperty("status", out var statusProp) || statusProp.GetString() != "1")
            {
                string info = root.TryGetProperty("info", out var infoProp) ? infoProp.GetString() : "未知错误";
                throw new InvalidOperationException($"高德 API 返回错误: {info}");
            }

            // 提取地理编码结果
            var geocodes = root.GetProperty("geocodes");
            if (geocodes.GetArrayLength() == 0)
                throw new InvalidOperationException("未找到该地址的经纬度信息");

            var first = geocodes[0];
            string location = first.GetProperty("location").GetString();
            string formattedAddress = first.TryGetProperty("formatted_address", out var addrProp)
                ? addrProp.GetString() : address;

            // 解析经纬度字符串（格式："经度,纬度"）
            var (lng, lat) = ParseLocation(location);

            // 根据需要转换坐标系
            if (convertToWgs84)
                (lng, lat) = Gcj02ToWgs84(lng, lat);

            return (lng, lat, formattedAddress ?? address);
        }

        /// <summary>
        /// 执行POI搜索API请求的具体实现（专用于地铁站）
        /// </summary>
        private async Task<(double Lng, double Lat, string Name)> DoPoiSearchAsync(string url, bool convertToWgs84)
        {
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;

            // 验证API返回状态
            if (!root.TryGetProperty("status", out var statusProp) || statusProp.GetString() != "1")
            {
                string info = root.TryGetProperty("info", out var infoProp) ? infoProp.GetString() : "未知错误";
                throw new InvalidOperationException($"高德 API 返回错误: {info}");
            }

            // 提取POI结果
            var pois = root.GetProperty("pois");
            if (pois.GetArrayLength() == 0)
                throw new InvalidOperationException("未找到地铁站");

            var first = pois[0];
            string location = first.GetProperty("location").GetString();
            string name = first.GetProperty("name").GetString();

            // 解析经纬度字符串
            var (lng, lat) = ParseLocation(location);

            // 根据需要转换坐标系
            if (convertToWgs84)
                (lng, lat) = Gcj02ToWgs84(lng, lat);

            return (lng, lat, name ?? "未知站名");
        }

        // ===== 工具方法 =====

        /// <summary>
        /// 解析经纬度字符串（格式："经度,纬度"）
        /// </summary>
        /// <param name="location">经纬度字符串</param>
        /// <returns>解析后的(经度, 纬度)元组</returns>
        /// <exception cref="FormatException">当格式不正确时抛出</exception>
        private static (double Lng, double Lat) ParseLocation(string location)
        {
            if (string.IsNullOrEmpty(location))
                throw new InvalidOperationException("返回数据中缺少经纬度");

            var parts = location.Split(',');
            if (parts.Length != 2)
                throw new FormatException("经纬度数据格式异常");

            if (!double.TryParse(parts[0], out var lng) || !double.TryParse(parts[1], out var lat))
                throw new FormatException("经纬度数值解析失败");

            return (lng, lat);
        }

        // ===== 重试机制 =====

        /// <summary>
        /// 执行带重试逻辑的异步操作
        /// 采用指数退避策略应对临时性故障
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="action">要执行的异步操作</param>
        /// <returns>操作结果</returns>
        /// <exception cref="Exception">当达到最大重试次数后仍失败时抛出原始异常</exception>
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
        {
            int attempt = 0;
            int backoff = INITIAL_BACKOFF_SECONDS;

            while (true)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex) when (attempt < MAX_RETRIES &&
                       (!ex.Message.Contains("ENGINE_RESPONSE_DATA_ERROR") ||
                        ex is HttpRequestException))
                {
                    // 仅对特定异常类型进行重试：
                    // 1. 非ENGINE_RESPONSE_DATA_ERROR：高德API临时错误
                    // 2. HttpRequestException：网络相关异常

                    attempt++;

                    // 指数退避：等待时间随重试次数增加
                    await Task.Delay(TimeSpan.FromSeconds(backoff));

                    // 下次退避时间翻倍（指数退避）
                    backoff *= 2;

                    backoff = Math.Min(backoff, 32);
                }
            }
        }

        // ===== 坐标转换算法（GCJ-02 → WGS-84）=====
        // 算法说明：高德地图使用GCJ-02坐标系（火星坐标系），而GPS设备通常使用WGS-84坐标系
        // 本算法通过数学变换将GCJ-02坐标转换为WGS-84坐标，适用于中国境内坐标

        /// <summary>
        /// 判断坐标是否在中国境外（无需转换）
        /// </summary>
        private static bool OutOfChina(double lon, double lat)
            => lon < 72.004 || lon > 137.8347 || lat < 0.8293 || lat > 55.8271;

        /// <summary>
        /// 纬度转换函数（核心算法部分）
        /// </summary>
        private static double TransformLat(double x, double y)
        {
            double ret = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y + 0.2 * Math.Sqrt(Math.Abs(x));
            ret += (20.0 * Math.Sin(6.0 * x * PI) + 20.0 * Math.Sin(2.0 * x * PI)) * 2.0 / 3.0;
            ret += (20.0 * Math.Sin(y * PI) + 40.0 * Math.Sin(y / 3.0 * PI)) * 2.0 / 3.0;
            ret += (160.0 * Math.Sin(y / 12.0 * PI) + 320.0 * Math.Sin(y * PI / 30.0)) * 2.0 / 3.0;
            return ret;
        }

        /// <summary>
        /// 经度转换函数（核心算法部分）
        /// </summary>
        private static double TransformLon(double x, double y)
        {
            double ret = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y + 0.1 * Math.Sqrt(Math.Abs(x));
            ret += (20.0 * Math.Sin(6.0 * x * PI) + 20.0 * Math.Sin(2.0 * x * PI)) * 2.0 / 3.0;
            ret += (20.0 * Math.Sin(x * PI) + 40.0 * Math.Sin(x / 3.0 * PI)) * 2.0 / 3.0;
            ret += (150.0 * Math.Sin(x / 12.0 * PI) + 300.0 * Math.Sin(x / 30.0 * PI)) * 2.0 / 3.0;
            return ret;
        }

        /// <summary>
        /// 将GCJ-02坐标（火星坐标系）转换为WGS-84坐标（GPS坐标系）
        /// </summary>
        /// <param name="gcjLon">GCJ-02经度</param>
        /// <param name="gcjLat">GCJ-02纬度</param>
        /// <returns>转换后的WGS-84坐标(经度, 纬度)</returns>
        private static (double wgsLon, double wgsLat) Gcj02ToWgs84(double gcjLon, double gcjLat)
        {
            // 境外坐标直接返回
            if (OutOfChina(gcjLon, gcjLat))
                return (gcjLon, gcjLat);

            // 计算偏移量
            double dLat = TransformLat(gcjLon - 105.0, gcjLat - 35.0);
            double dLon = TransformLon(gcjLon - 105.0, gcjLat - 35.0);

            // 计算WGS-84纬度
            double radLat = gcjLat / 180.0 * PI;
            double magic = Math.Sin(radLat);
            magic = 1 - EE * magic * magic;
            double sqrtMagic = Math.Sqrt(magic);
            dLat = dLat * 180.0 / (A * (1 - EE) / (magic * sqrtMagic) * PI);
            dLon = dLon * 180.0 / (A / sqrtMagic * Math.Cos(radLat) * PI);

            // 计算最终坐标
            double wgsLat = gcjLat - dLat;
            double wgsLon = gcjLon - dLon;
            return (wgsLon, wgsLat);
        }

        // ===== IDisposable实现 =====

        /// <summary>
        /// 释放托管资源（HttpClient）
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 受保护的释放方法，支持继承类重写
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 释放HttpClient等托管资源
                _httpClient?.Dispose();
            }

            _disposed = true;
        }
    }
}