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
        public double AvgTransferTime { get; set; } = 120.0;
        public double AvgStopTime { get; set; } = 45.0;
        public double OutStationTransferTime { get; set; } = 300.0;
        public double AvgEntryTime { get; set; } = 120.0;
        public double DefaultBoardTime { get; set; } = 3.0;
        public double FareCap { get; set; }
        public string KeyCity { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string UpdateDate { get; set; } = string.Empty;
        public string PricingType { get; set; } = "distance";
        public double? DefaultFare { get; set; }

        // ======================== 票价相关 ========================
        public List<SimFareSegment>? FareSegments { get; set; }
        public SimLinearExtension? LinearExtension { get; set; }
        public List<SimSpecialLineRule>? SpecialLineRules { get; set; }

        // ======================== 规则列表 ========================
        public List<SimHeadwayRule>? HeadwayRules { get; set; }
        public List<SimCrowdRule>? CrowdRules { get; set; }
        public List<SimOpenTimeRule>? OpenTimes { get; set; }
        public List<SimVirtualTransfer>? VirtualTransfers { get; set; }
        public List<SimSpeedLimit>? SpeedLimits { get; set; }
        public JsonRoot JsonData { get; set; }

        // ======================== 核心数据字典（线程安全） ========================
        public ConcurrentDictionary<string, double[]> StationCoords { get; } = new();
        public ConcurrentDictionary<string, List<string>> DirStations { get; } = new();
        public ConcurrentDictionary<string, string> LineNameMap { get; } = new();
        public ConcurrentDictionary<string, string> LineColorMap { get; } = new();
        public ConcurrentDictionary<string, string> LineCityMap { get; } = new();
        public ConcurrentDictionary<string, bool> HasLoopMap { get; } = new();
        public ConcurrentDictionary<string, bool> HasBranchMap { get; } = new();
        public ConcurrentDictionary<string, double> LineTotalLengthMap { get; } = new();
        public ConcurrentDictionary<string, bool> LineSpecialMap { get; } = new();
        public ConcurrentDictionary<string, bool> LineHasFastMap { get; } = new();
        public ConcurrentDictionary<string, int> LineMaxSpeedMap { get; } = new();
        public ConcurrentDictionary<string, string> DirDirectionTypeMap { get; } = new();
        public ConcurrentDictionary<string, string> DirNickNameMap { get; } = new();
        public ConcurrentDictionary<string, string> DirNickColorMap { get; } = new();
        public ConcurrentDictionary<string, bool> DirFutureMap { get; } = new();
        public ConcurrentDictionary<string, bool> DirLoopMap { get; } = new();
        public ConcurrentDictionary<string, double> LineBrakeMap { get; } = new();
        public ConcurrentDictionary<string, double> StationStopTimeMap { get; } = new();
        public ConcurrentDictionary<string, string> LineFormationMap { get; } = new();
        public ConcurrentDictionary<string, double> LinePowerKwMap { get; } = new();
        public ConcurrentDictionary<string, double> LineTotalLengthMapWithoutFuture { get; } = new();
        public ConcurrentDictionary<string, bool> HasFutureMap { get; } = new();
        public ConcurrentDictionary<string, bool> AllFutureMap { get; } = new();
        public ConcurrentDictionary<string, bool> LineSegmentedMap { get; } = new();

        public ConcurrentDictionary<string, Dictionary<string, string>> StationLineStatusMap { get; } = new();
        public ConcurrentDictionary<string, Dictionary<string, string>> TransferStatusMap { get; } = new();
        public ConcurrentDictionary<string, double> SpecialTransferTimeMap { get; } = new();

        // ======================== 清空方法 ========================
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
            HasBranchMap.Clear();
            LineTotalLengthMap.Clear();
            LineSpecialMap.Clear();
            LineHasFastMap.Clear();
            LineMaxSpeedMap.Clear();
            DirDirectionTypeMap.Clear();
            DirNickNameMap.Clear();
            DirNickColorMap.Clear();
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