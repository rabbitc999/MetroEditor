using System.Collections.Concurrent;
using System.Collections.Generic;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.Simulator.Models;

namespace MetroEditor.Simulator.Data
{
    /// <summary>
    /// 地图数据缓存
    /// </summary>
    public class SimDataCache
    {
        // ======================== 基础运营参数 ========================
        /// <summary>平均站内换乘时间（秒）</summary>
        public double AvgTransferTime { get; set; } = 120.0;

        /// <summary>平均停站时间（秒）</summary>
        public double AvgStopTime { get; set; } = 45.0;

        /// <summary>出站换乘时间（秒）</summary>
        public double OutStationTransferTime { get; set; } = 300.0;

        /// <summary>平均进站时间（秒）</summary>
        public double AvgEntryTime { get; set; } = 120.0;

        /// <summary>默认上车时间（秒）</summary>
        public double DefaultBoardTime { get; set; } = 3.0;

        /// <summary>票价封顶上限（0表示无封顶）</summary>
        public double FareCap { get; set; }

        /// <summary>主城市标识</summary>
        public string KeyCity { get; set; } = string.Empty;

        /// <summary>数据版本号</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>数据更新日期</summary>
        public string UpdateDate { get; set; } = string.Empty;

        /// <summary>计价模式（如 "distance"）</summary>
        public string PricingType { get; set; } = "distance";

        /// <summary>默认起步票价（元）</summary>
        public double? DefaultFare { get; set; }

        // ======================== 票价相关 ========================
        /// <summary>分段票价列表</summary>
        public List<SimFareSegment>? FareSegments { get; set; }

        /// <summary>线性续价配置</summary>
        public SimLinearExtension? LinearExtension { get; set; }

        /// <summary>特殊线路票价规则列表</summary>
        public List<SimSpecialLineRule>? SpecialLineRules { get; set; }

        // ======================== 规则列表 ========================
        /// <summary>发车间隔规则列表</summary>
        public List<SimHeadwayRule>? HeadwayRules { get; set; }

        /// <summary>拥挤度规则列表</summary>
        public List<SimCrowdRule>? CrowdRules { get; set; }

        /// <summary>运营时间规则列表</summary>
        public List<SimOpenTimeRule>? OpenTimes { get; set; }

        /// <summary>虚拟换乘规则列表</summary>
        public List<SimVirtualTransfer>? VirtualTransfers { get; set; }

        /// <summary>区间限速规则列表</summary>
        public List<SimSpeedLimit>? SpeedLimits { get; set; }

        /// <summary>原始 JSON 路网数据</summary>
        public JsonRoot JsonData { get; set; }

        // ======================== 核心数据字典（线程安全） ========================
        /// <summary>车站坐标字典（Key: 车站ID, Value: [经度, 纬度]）</summary>
        public ConcurrentDictionary<string, double[]> StationCoords { get; } = new();

        /// <summary>方向站点列表字典（Key: 方向键, Value: 站点名列表）</summary>
        public ConcurrentDictionary<string, List<string>> DirStations { get; } = new();

        /// <summary>线路显示名称映射（Key: 线路ID, Value: 显示名）</summary>
        public ConcurrentDictionary<string, string> LineNameMap { get; } = new();

        /// <summary>线路颜色映射（Key: 线路ID, Value: 十六进制颜色）</summary>
        public ConcurrentDictionary<string, string> LineColorMap { get; } = new();

        /// <summary>线路所属城市映射（Key: 线路ID, Value: 城市名）</summary>
        public ConcurrentDictionary<string, string> LineCityMap { get; } = new();

        /// <summary>线路是否包含环线（Key: 线路ID）</summary>
        public ConcurrentDictionary<string, bool> HasLoopMap { get; } = new();

        /// <summary>线路总长度（Key: 线路ID, Value: 公里）</summary>
        public ConcurrentDictionary<string, double> LineTotalLengthMap { get; } = new();

        /// <summary>线路是否为特殊线路（Key: 线路ID）</summary>
        public ConcurrentDictionary<string, bool> LineSpecialMap { get; } = new();

        /// <summary>线路最高速度（Key: 线路ID, Value: km/h）</summary>
        public ConcurrentDictionary<string, int> LineMaxSpeedMap { get; } = new();

        /// <summary>方向类型（Key: 方向键, Value: inner/outer）</summary>
        public ConcurrentDictionary<string, string> DirDirectionTypeMap { get; } = new();

        /// <summary>方向是否为远期规划（Key: 方向键）</summary>
        public ConcurrentDictionary<string, bool> DirFutureMap { get; } = new();

        /// <summary>方向是否为环线（Key: 方向键）</summary>
        public ConcurrentDictionary<string, bool> DirLoopMap { get; } = new();

        /// <summary>线路制动率（Key: 线路ID）</summary>
        public ConcurrentDictionary<string, double> LineBrakeMap { get; } = new();

        /// <summary>站点停站时间（Key: "站名|方向键", Value: 秒）</summary>
        public ConcurrentDictionary<string, double> StationStopTimeMap { get; } = new();

        /// <summary>列车编组形式（Key: 线路ID, Value: 如 "6A"）</summary>
        public ConcurrentDictionary<string, string> LineFormationMap { get; } = new();

        /// <summary>牵引电机功率（Key: 线路ID, Value: kW）</summary>
        public ConcurrentDictionary<string, double> LinePowerKwMap { get; } = new();

        /// <summary>线路总长度（不含远期规划）（Key: 线路ID, Value: 公里）</summary>
        public ConcurrentDictionary<string, double> LineTotalLengthMapWithoutFuture { get; } = new();

        /// <summary>线路是否包含远期规划方向（Key: 线路ID）</summary>
        public ConcurrentDictionary<string, bool> HasFutureMap { get; } = new();

        /// <summary>线路所有方向是否均为远期规划（Key: 线路ID）</summary>
        public ConcurrentDictionary<string, bool> AllFutureMap { get; } = new();

        /// <summary>线路是否分段运营（Key: 线路ID）</summary>
        public ConcurrentDictionary<string, bool> LineSegmentedMap { get; } = new();

        /// <summary>车站线路状态（Key: 车站ID, Value: {线路ID, status}）</summary>
        public ConcurrentDictionary<string, Dictionary<string, string>> StationLineStatusMap { get; } = new();

        /// <summary>换乘状态（Key: 车站ID, Value: {"线路A|线路B", status}）</summary>
        public ConcurrentDictionary<string, Dictionary<string, string>> TransferStatusMap { get; } = new();

        /// <summary>特殊换乘时间（Key: "站名|线路A|线路B", Value: 秒）</summary>
        public ConcurrentDictionary<string, double> SpecialTransferTimeMap { get; } = new();

        // ======================== 清空方法 ========================
        /// <summary>
        /// 清空所有缓存数据，恢复为默认状态。
        /// </summary>
        public void Clear()
        {
            AvgTransferTime = 120.0;
            AvgStopTime = 45.0;
            OutStationTransferTime = 300.0;
            AvgEntryTime = 120.0;
            DefaultBoardTime = 3.0;
            FareCap = 0;
            KeyCity = string.Empty;
            Version = string.Empty;
            UpdateDate = string.Empty;
            PricingType = "distance";
            DefaultFare = null;

            FareSegments = null;
            LinearExtension = null;
            SpecialLineRules = null;
            HeadwayRules = null;
            CrowdRules = null;
            OpenTimes = null;
            VirtualTransfers = null;
            SpeedLimits = null;
            JsonData = null;

            StationCoords.Clear();
            DirStations.Clear();
            LineNameMap.Clear();
            LineColorMap.Clear();
            LineCityMap.Clear();
            HasLoopMap.Clear();
            LineTotalLengthMap.Clear();
            LineSpecialMap.Clear();
            LineMaxSpeedMap.Clear();
            DirDirectionTypeMap.Clear();
            DirFutureMap.Clear();
            DirLoopMap.Clear();
            LineBrakeMap.Clear();
            StationStopTimeMap.Clear();
            LineFormationMap.Clear();
            LinePowerKwMap.Clear();
            LineTotalLengthMapWithoutFuture.Clear();
            HasFutureMap.Clear();
            AllFutureMap.Clear();
            LineSegmentedMap.Clear();
            StationLineStatusMap.Clear();
            TransferStatusMap.Clear();
            SpecialTransferTimeMap.Clear();
        }
    }
}