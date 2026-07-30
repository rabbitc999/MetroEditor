using MetroEditor.MetroLang.JsonModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 编译器解析后的核心数据容器。
    /// 作为词法分析、语法分析和语义分析的中间产物，最终用于生成 <see cref="JsonRoot"/>。
    /// </summary>
    public class ParsedData
    {
        /// <summary>主城市标识</summary>
        public string MainCity { get; set; } = string.Empty;

        /// <summary>主城市标识</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>主城市标识</summary>
        public string CreateDate { get; set; } = string.Empty;

        /// <summary>主城市标识</summary>
        public List<string> OtherCities { get; set; } = new();

        /// <summary>主城市标识</summary>
        public string UpdateDate { get; set; } = string.Empty;

        /// <summary>主城市标识</summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>城市信息字典（Key: 城市ID, Value: 城市属性字典）</summary>
        public Dictionary<string, Dictionary<string, string>> Cities { get; set; } = new();

        /// <summary>车站坐标字典（Key: 车站ID, Value: (经度, 纬度)）</summary>
        public Dictionary<string, (double Lon, double Lat)> Stations { get; set; } = new();

        /// <summary>线路信息字典（Key: 线路ID）</summary>
        public Dictionary<string, LineInfo> Lines { get; set; } = new();

        /// <summary>方向信息字典（Key: 方向ID）</summary>
        public Dictionary<string, DirectionInfo> Directions { get; set; } = new();

        /// <summary>换乘规则列表</summary>
        public List<TransferRule> Transfers { get; set; } = new();

        /// <summary>车站状态字典（Key: (车站ID, 线路ID?), Value: 状态字符串）</summary>
        public Dictionary<(string Station, string? LineId), string> StationStatus { get; set; } = new();

        /// <summary>站间距字典（Key: 方向ID, Value: 距离列表）</summary>
        public Dictionary<string, List<double>> Distances { get; set; } = new();

        /// <summary>自定义停站时间字典（Key: (车站ID, 线路ID), Value: 停站秒数）</summary>
        public Dictionary<(string Station, string LineId), double> DwellTimes { get; set; } = new();

        /// <summary>默认站内换乘时间（秒）</summary>
        public double DefaultInStationTime { get; set; }

        /// <summary>默认出站换乘时间（秒）</summary>
        public double DefaultOutStationTime { get; set; }

        /// <summary>默认停站时间（秒）</summary>
        public double DefaultDwellTime { get; set; }

        /// <summary>编译警告信息列表</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>线路外部链接字典（Key: 线路ID, Value: URL）</summary>
        public Dictionary<string, string> LineLinks { get; set; } = new();

        /// <summary>票价规则列表</summary>
        public List<MoneyRule> MoneyRules { get; set; } = new();

        /// <summary>特殊票价规则列表（如机场线附加费）</summary>
        public List<SpecialFareRule> SpecialFareRules { get; set; } = new();

        /// <summary>默认进站时间（秒）</summary>
        public double DefaultEntryTime { get; set; } = 2.0;

        /// <summary>特殊线路 ID 集合</summary>
        public HashSet<string> SpecialLineIds { get; set; } = new();

        /// <summary>发车间隔规则列表</summary>
        public List<HeadwayRule> HeadwayRules { get; set; } = new();

        /// <summary>车站运营模式规则列表</summary>
        public List<StationModeRule> StationModes { get; set; } = new();

        /// <summary>拥挤度规则列表</summary>
        public List<CrowdRule> CrowdRules { get; set; } = new();

        /// <summary>运营时间规则列表</summary>
        public List<OpenTimeRule> OpenTimes { get; set; } = new();

        /// <summary>车辆段信息列表</summary>
        public List<DepotInfo> Depots { get; set; } = new();

        /// <summary>区间限速规则列表</summary>
        public List<SpeedLimit> SpeedLimits { get; set; } = new();

        /// <summary>虚拟换乘规则列表</summary>
        public List<VirtualTransferRule> VirtualTransfers { get; set; } = new();

        /// <summary>车站所属城市字典（Key: 车站ID, Value: 城市ID）</summary>
        public Dictionary<string, string> StationCities { get; set; } = new();

        /// <summary>地图标注列表</summary>
        public List<Label> Labels { get; set; } = new();

        /// <summary>特殊线路配置列表</summary>
        public List<SpecialLine> SpecialLines { get; set; } = new();

        /// <summary>默认上车时间（秒），用于模拟计算</summary>
        public double DefaultBoardTime { get; set; } = 3.0;

        public List<string> LanguageOrder { get; set; } = new();

        public Dictionary<string, List<string>> StationOtherNames { get; set; } = new();

        public Dictionary<string, StationFacilities> Facilities { get; set; } = new();

        public List<PromotionPolicy> Promotions { get; set; } = new();

        /// <summary>区域定义</summary>
        public Dictionary<string, ZoneInfo> Zones { get; set; } = new();



        /// <summary>
        /// 平台坐标映射 (车站ID, 线路ID) -> (经度, 纬度)
        /// </summary>
        public Dictionary<(string Station, string LineId), (double Lon, double Lat)> PlatformCoords { get; set; } = new();

        public ParsedData Clone()
        {
            return new ParsedData
            {
                MainCity = this.MainCity,
                Cities = new Dictionary<string, Dictionary<string, string>>(this.Cities.Select(kv => new KeyValuePair<string, Dictionary<string, string>>(kv.Key, new Dictionary<string, string>(kv.Value)))),
                Stations = new Dictionary<string, (double Lon, double Lat)>(this.Stations),
                Lines = new Dictionary<string, LineInfo>(this.Lines.Select(kv => new KeyValuePair<string, LineInfo>(kv.Key, kv.Value.Clone()))),
                Directions = new Dictionary<string, DirectionInfo>(this.Directions.Select(kv => new KeyValuePair<string, DirectionInfo>(kv.Key, kv.Value.Clone()))),
                Transfers = this.Transfers.Select(t => t.Clone()).ToList(),
                StationStatus = new Dictionary<(string Station, string? LineId), string>(this.StationStatus),
                Distances = new Dictionary<string, List<double>>(this.Distances.Select(kv => new KeyValuePair<string, List<double>>(kv.Key, new List<double>(kv.Value)))),
                DwellTimes = new Dictionary<(string Station, string LineId), double>(this.DwellTimes),
                DefaultInStationTime = this.DefaultInStationTime,
                DefaultOutStationTime = this.DefaultOutStationTime,
                DefaultDwellTime = this.DefaultDwellTime,
                Warnings = new List<string>(this.Warnings),
                LineLinks = new Dictionary<string, string>(this.LineLinks),
                MoneyRules = this.MoneyRules.Select(mr =>
                {
                    if (mr is MoneyStartRule ms) return (MoneyRule)ms.Clone();
                    if (mr is MoneySegmentRule msr) return (MoneyRule)msr.Clone();
                    if (mr is MoneyCalcRule mcr) return (MoneyRule)mcr.Clone();
                    return null; // 不应发生
                }).Where(m => m != null).Cast<MoneyRule>().ToList(),
                SpecialFareRules = this.SpecialFareRules.Select(sf => sf.Clone()).ToList(),
                DefaultEntryTime = this.DefaultEntryTime,
                SpecialLineIds = new HashSet<string>(this.SpecialLineIds),
                HeadwayRules = this.HeadwayRules.Select(h => h.Clone()).ToList(),
                StationModes = this.StationModes.Select(sm => sm.Clone()).ToList(),
                CrowdRules = this.CrowdRules.Select(c => c.Clone()).ToList(),
                OpenTimes = this.OpenTimes.Select(o => o.Clone()).ToList(),
                Depots = this.Depots.Select(d => d.Clone()).ToList(),
                SpeedLimits = this.SpeedLimits.Select(s => s.Clone()).ToList(),
                VirtualTransfers = this.VirtualTransfers.Select(vt => vt.Clone()).ToList(),
                StationCities = new Dictionary<string, string>(this.StationCities),
                Labels = this.Labels.Select(l => l.Clone()).ToList(),
                SpecialLines = this.SpecialLines.Select(sl => sl.Clone()).ToList(),
                DefaultBoardTime = this.DefaultBoardTime,
                LanguageOrder = new List<string>(this.LanguageOrder),
                StationOtherNames = new Dictionary<string, List<string>>(this.StationOtherNames.Select(kv => new KeyValuePair<string, List<string>>(kv.Key, new List<string>(kv.Value)))),
                PlatformCoords = new Dictionary<(string, string), (double, double)>(this.PlatformCoords)
            };
        }
    }
}
