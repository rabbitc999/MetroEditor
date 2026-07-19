using MetroEditor.MetroLang.DataModel;
using System.Text.Json;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 编译数据上下文（CompilerContext）是整个编译流程的数据中枢。
    /// 它集中存放所有解析过程中产生的中间数据，并提供安全的访问和修改方法。
    /// 所有数据通过属性对外暴露，修改通过专门的方法完成，确保数据一致性。
    /// 
    /// 设计原则：
    /// - 单一职责：仅负责数据存储与基础校验，不包含解析或生成逻辑。
    /// - 封装性：内部集合只读（或 internal set），修改通过明确的方法。
    /// - 可扩展：预留了条件编译变量、换乘模板等扩展点。
    /// </summary>
    public class Context
    {
        // ====================================================================
        // 1. 基础元数据（从 PROGRAM_DEFINE 中提取）
        // ====================================================================

        /// <summary>主城市标识（如 "BJ"、"SH"）</summary>
        public string MainCity { get; internal set; } = string.Empty;

        /// <summary>数据版本号</summary>
        public string Version { get; internal set; } = "V1.0";

        /// <summary>数据创建日期（格式：YYYY-MM-DD）</summary>
        public string CreateDate { get; internal set; } = DateTime.Now.ToString("yyyy-MM-dd");

        /// <summary>数据最近更新日期</summary>
        public string UpdateDate { get; internal set; } = DateTime.Now.ToString("yyyy-MM-dd");

        /// <summary>数据作者</summary>
        public string Author { get; internal set; } = "匿名";

        /// <summary>语言顺序列表</summary>
        public List<string> LanguageOrder { get; internal set; } = new();

        /// <summary>站点其他名称映射（Key=站点ID, Value=按 LANGUAGE_ORDER 排列的名称列表）</summary>
        public Dictionary<string, List<string>> StationOtherNames { get; internal set; } = new();

        // ====================================================================
        // 2. 核心路网数据
        // ====================================================================

        /// <summary>
        /// 城市元数据字典：Key=城市ID, Value=城市属性键值对（如版本、更新日期等）
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> Cities { get; internal set; } = new();

        /// <summary>
        /// 车站坐标字典：Key=车站ID, Value=(经度, 纬度)
        /// </summary>
        public Dictionary<string, (double Lon, double Lat)> Stations { get; internal set; } = new();

        /// <summary>
        /// 线路信息字典：Key=线路ID, Value=线路元数据（名称、颜色、城市等）
        /// </summary>
        public Dictionary<string, LineInfo> Lines { get; internal set; } = new();

        /// <summary>
        /// 方向信息字典：Key=方向ID（内部标识）, Value=方向定义（站点序列、属性等）
        /// </summary>
        public Dictionary<string, DirectionInfo> Directions { get; internal set; } = new();

        /// <summary>
        /// 站间距字典：Key=方向ID, Value=相邻站距离列表（单位：米），NIL用1e50表示
        /// </summary>
        public Dictionary<string, List<double>> Distances { get; internal set; } = new();

        /// <summary>
        /// 线路外部链接字典：Key=线路ID, Value=URL（如维基百科）
        /// </summary>
        public Dictionary<string, string> LineLinks { get; internal set; } = new();

        /// <summary>
        /// 特殊线路ID集合（如机场线、虚拟线、公交接驳线）
        /// </summary>
        public HashSet<string> SpecialLineIds { get; internal set; } = new();

        // ====================================================================
        // 3. 换乘与车站状态
        // ====================================================================

        /// <summary>换乘规则列表（同站换乘）</summary>
        public List<TransferRule> Transfers { get; internal set; } = new();

        /// <summary>
        /// 车站状态映射：Key=(车站ID, 线路ID?)，Value=状态（如 "operating"、"pending"、"out_station"）
        /// 线路ID为空时表示该车站所有线路的默认状态
        /// </summary>
        public Dictionary<(string Station, string? LineId), string> StationStatus { get; internal set; } = new();

        /// <summary>虚拟换乘规则列表（出站换乘/虚拟换乘）</summary>
        public List<VirtualTransferRule> VirtualTransfers { get; internal set; } = new();

        // ====================================================================
        // 4. 时间与运营参数
        // ====================================================================

        /// <summary>
        /// 自定义停站时间：Key=(车站ID, 线路ID), Value=停站秒数（覆盖全局默认值）
        /// </summary>
        public Dictionary<(string Station, string LineId), double> DwellTimes { get; internal set; } = new();

        /// <summary>
        /// 默认时间参数：Key=参数名（如 "in_station", "out_station", "dwell", "entry", "board"）, Value=秒数
        /// </summary>
        public Dictionary<string, double> DefaultTimes { get; internal set; } = new();

        // ====================================================================
        // 5. 动态规则（发车间隔、运营模式、拥挤度等）
        // ====================================================================

        public List<HeadwayRule> HeadwayRules { get; internal set; } = new();
        public List<StationModeRule> StationModes { get; internal set; } = new();
        public List<CrowdRule> CrowdRules { get; internal set; } = new();
        public List<OpenTimeRule> OpenTimes { get; internal set; } = new();
        public List<SpeedLimit> SpeedLimits { get; internal set; } = new();
        public List<DepotInfo> Depots { get; internal set; } = new();

        // ====================================================================
        // 6. 票价与优惠
        // ====================================================================

        /// <summary>基础票价配置（包含分段计价、线性续价、换乘优惠等）</summary>
        public PriceConfig PriceConfig { get; internal set; } = new();

        /// <summary>票价优惠策略列表（如学生卡、老人卡）</summary>
        public List<PromotionPolicy> Promotions { get; internal set; } = new();

        // ====================================================================
        // 7. 地图标注与设施
        // ====================================================================

        /// <summary>地图文本标注列表</summary>
        public List<Label> Labels { get; internal set; } = new();

        /// <summary>车站无障碍设施字典：Key=车站ID, Value=设施详情</summary>
        public Dictionary<string, StationFacilities> Facilities { get; internal set; } = new();

        // ====================================================================
        // 8. 换乘模板（内部使用，解析过程中生成）
        // ====================================================================

        /// <summary>
        /// 换乘类型模板定义：Key=类型ID（如 "walk_transfer"）, Value=模板配置（状态、时间、安检等）
        /// </summary>
        public Dictionary<string, TransferTypeDefinition> TransferTypes { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 换乘模板实例列表：存储尚未展开的 TRANSFER 指令，待解析完成后批量展开为具体换乘规则
        /// </summary>
        public List<TransferTemplate> TransferTemplates { get; internal set; } = new();

        /// <summary>
        /// 线路模板
        /// </summary>
        public Dictionary<string, LineTemplate> LineTemplates { get; internal set; } = new();

        /// <summary>
        /// 方向模板
        /// </summary>
        public Dictionary<string, DirectionTemplate> DirectionTemplates { get; internal set; } = new();

        // ====================================================================
        // 9. 辅助映射
        // ====================================================================
        /// <summary>
        /// 车站所属城市映射（用于跨城数据）：Key=车站ID, Value=城市ID
        /// 该映射在解析过程中由 STATION_DEFINE 的 CITY 属性显式指定，或由语义分析器推断
        /// </summary>
        public Dictionary<string, string> StationCity { get; internal set; } = new();

        /// <summary>编译期条件符号集合（不区分大小写）</summary>
        public HashSet<string> DefinedSymbols { get; internal set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>编译期断言信息（不区分大小写）</summary>
        public List<Assertion> Assertions { get; internal set; } = new();

        /// <summary>输出信息</summary>
        public List<string> EchoMessages { get; internal set; } = new();

        // ====================================================================
        // 10. 编译警告列表（用于收集非致命问题）
        // ====================================================================

        public List<string> Warnings { get; internal set; } = new();

        /// <summary>
        /// 获取编译器解析后的中间数据模型
        /// 该方法将编译器内部状态转换为对外暴露的 ParsedData 结构
        /// 与 ExportJson 不同，此方法保留更多内部细节，供编辑器、反编译器和分析工具使用
        /// </summary>
        /// <returns>包含所有解析数据的 ParsedData 对象</returns>
        public ParsedData GetParsedData()
        {
            // 创建 ParsedData 对象并映射基础数据
            var data = new ParsedData
            {
                // 基础元数据
                MainCity = MainCity,
                Cities = Cities,
                Stations = Stations,

                // 核心路网数据：保留内部方向键，不进行外部格式转换
                // 因为 ParsedData 是内部数据结构，直接使用内部格式更高效
                Lines = Lines,
                Directions = Directions,

                // 换乘规则和车站状态
                Transfers = Transfers,
                StationStatus = new Dictionary<(string, string?), string>(StationStatus),

                // 距离和停站时间数据
                Distances = Distances,
                DwellTimes = DwellTimes,

                // 默认时间参数：从默认时间字典提取，提供合理的默认值
                DefaultInStationTime = DefaultTimes.GetValueOrDefault("in_station", 180),
                DefaultOutStationTime = DefaultTimes.GetValueOrDefault("out_station", 300),
                DefaultEntryTime = DefaultTimes.GetValueOrDefault("entry", 120),
                DefaultDwellTime = DefaultTimes.GetValueOrDefault("dwell", 120),

                // 警告信息和线路链接
                Warnings = Warnings,
                LineLinks = LineLinks,

                // 特殊线路标识集合
                SpecialLineIds = new HashSet<string>(SpecialLineIds),

                // 运营规则数据
                HeadwayRules = HeadwayRules,
                StationModes = StationModes,
                OpenTimes = OpenTimes,

                // 车辆段信息：需要将内部方向键转换为外部格式
                Depots = Depots.Select(d => new DepotInfo
                {
                    LineId = d.LineId,
                    Station = d.Station,
                    // 方向键转换：将内部ID（如"B9_0"）转为外部格式（如"LINE1|START|END"）
                    DirectionKey = d.DirectionKey,
                    DepartureTime = d.DepartureTime
                }).ToList(),

                // 虚拟换乘和车站城市映射
                VirtualTransfers = VirtualTransfers,
                StationCities = new Dictionary<string, string>(StationCity),

                // 地图标注和特殊票价配置
                Labels = new List<Label>(Labels),
                SpecialLines = new List<SpecialLine>(PriceConfig.SpecialLines)
            };

            // 转换拥挤度规则中的方向键：内部ID → 外部格式
            data.CrowdRules = CrowdRules.Select(c => new CrowdRule
            {
                DirectionKey = c.DirectionKey,
                StartTime = c.StartTime,
                EndTime = c.EndTime,
                Factor = c.Factor,
                Days = new List<DayOfWeek>(c.Days)
            }).ToList();

            // 转换区间限速规则中的方向键：内部ID → 外部格式
            data.SpeedLimits = SpeedLimits.Select(s => new SpeedLimit
            {
                DirectionKey = s.DirectionKey,
                FromStation = s.FromStation,
                ToStation = s.ToStation,
                SpeedKmh = s.SpeedKmh
            }).ToList();

            // 清空已有的票价规则（如果存在）
            data.MoneyRules.Clear();

            // ===== 票价规则转换：将 PriceConfig 转换为 MoneyRule 列表 =====

            // 1. 起步价规则：使用默认票价或回退到2.0
            double startPrice = PriceConfig.DefaultFare > 0 ? PriceConfig.DefaultFare : 2.0;
            data.MoneyRules.Add(new MoneyStartRule { Price = startPrice });

            // 2. 分段计价规则：将每个里程段转换为独立的规则
            foreach (var segment in PriceConfig.Segments)
            {
                data.MoneyRules.Add(new MoneySegmentRule
                {
                    Start = segment.RangeStart,
                    End = segment.RangeEnd,
                    Price = segment.Fare
                });
            }

            // 3. 线性延伸规则：将超过阈值的部分按里程计费
            if (PriceConfig.LinearExtension != null)
            {
                var extension = PriceConfig.LinearExtension;
                // 确定基准票价：优先使用最后一个分段票价，否则使用起步价
                double baseFare = startPrice;
                if (PriceConfig.Segments.Count > 0)
                    baseFare = PriceConfig.Segments.Last().Fare;

                // 创建线性计费规则：每公里加收指定金额
                data.MoneyRules.Add(new MoneyCalcRule
                {
                    DistSegment = 1.0,           // 计费单位：1公里
                    AddPrice = extension.RatePerKm // 每公里加收费用
                });
            }

            // ===== 特殊线路票价规则转换 =====
            data.SpecialFareRules.Clear();
            foreach (var specialLine in PriceConfig.SpecialLines)
            {
                var rule = new SpecialFareRule { LineId = specialLine.LineId };

                // 处理全线统一票价
                if (specialLine.All.HasValue)
                {
                    rule.Type = "all";
                    rule.Price = specialLine.All.Value;
                    rule.StartStation = "";
                    rule.EndStation = "";
                }
                // 处理区间附加费：仅取第一个区间规则
                else if (specialLine.Surcharge != null && specialLine.Surcharge.Count > 0)
                {
                    rule.Type = "segment";
                    var firstSurcharge = specialLine.Surcharge.First();
                    rule.StartStation = firstSurcharge.NodeA ?? "";
                    rule.EndStation = firstSurcharge.NodeB ?? "";
                    rule.Price = firstSurcharge.Price;
                }

                data.SpecialFareRules.Add(rule);
            }

            data.LanguageOrder = new List<string>(LanguageOrder);
            data.StationOtherNames = new Dictionary<string, List<string>>(StationOtherNames);
            data.Facilities = new Dictionary<string, StationFacilities>(Facilities);
            data.Promotions = new List<PromotionPolicy>(Promotions);
            data.Author = Author;
            data.UpdateDate = UpdateDate;
            data.CreateDate = CreateDate;
            data.OtherCities = Cities.Where(k => k.Key != MainCity).Select(k => k.Key).ToList();

            return data;
        }

        /// <summary>
        /// 返回当前上下文的深拷贝副本。
        /// 使用 JSON 序列化实现，确保所有数据独立于原对象。
        /// </summary>
        /// <summary>
        /// 返回当前上下文的深拷贝副本（递归复制所有数据，完全独立）
        /// </summary>
        public Context DeepClone()
        {
            var clone = new Context();

            // ---- 1. 基础元数据 ----
            clone.MainCity = this.MainCity;
            clone.Version = this.Version;
            clone.CreateDate = this.CreateDate;
            clone.UpdateDate = this.UpdateDate;
            clone.Author = this.Author;
            clone.LanguageOrder = new List<string>(this.LanguageOrder);
            clone.StationOtherNames = CloneDictionary(this.StationOtherNames, v => new List<string>(v));

            // ---- 2. 核心路网数据 ----
            clone.Cities = CloneDictionary(this.Cities, v => new Dictionary<string, string>(v));
            clone.Stations = new Dictionary<string, (double Lon, double Lat)>(this.Stations);
            clone.Lines = CloneDictionary(this.Lines, v => v.Clone()); // 需 LineInfo 实现 Clone 方法
            clone.Directions = CloneDictionary(this.Directions, v => v.Clone());
            clone.Distances = CloneDictionary(this.Distances, v => new List<double>(v));
            clone.LineLinks = new Dictionary<string, string>(this.LineLinks);
            clone.SpecialLineIds = new HashSet<string>(this.SpecialLineIds);

            // ---- 3. 换乘与车站状态 ----
            clone.Transfers = this.Transfers.Select(t => t.Clone()).ToList(); // TransferRule 需实现 Clone
            clone.StationStatus = CloneDictionary(this.StationStatus, v => v);
            clone.VirtualTransfers = this.VirtualTransfers.Select(vt => vt.Clone()).ToList();

            // ---- 4. 时间与运营参数 ----
            clone.DwellTimes = CloneDictionary(this.DwellTimes, v => v);
            clone.DefaultTimes = new Dictionary<string, double>(this.DefaultTimes);

            // ---- 5. 动态规则 ----
            clone.HeadwayRules = this.HeadwayRules.Select(r => r.Clone()).ToList();
            clone.StationModes = this.StationModes.Select(m => m.Clone()).ToList();
            clone.CrowdRules = this.CrowdRules.Select(c => c.Clone()).ToList();
            clone.OpenTimes = this.OpenTimes.Select(o => o.Clone()).ToList();
            clone.SpeedLimits = this.SpeedLimits.Select(s => s.Clone()).ToList();
            clone.Depots = this.Depots.Select(d => d.Clone()).ToList();

            // ---- 6. 票价与优惠 ----
            clone.PriceConfig = this.PriceConfig.Clone(); // PriceConfig 需实现 Clone
            clone.Promotions = this.Promotions.Select(p => p.Clone()).ToList();

            // ---- 7. 地图标注与设施 ----
            clone.Labels = this.Labels.Select(l => l.Clone()).ToList();
            clone.Facilities = CloneDictionary(this.Facilities, f => f.Clone());

            // ---- 8. 换乘模板 ----
            clone.TransferTypes = CloneDictionary(this.TransferTypes, t => t.Clone());
            clone.TransferTemplates = this.TransferTemplates.Select(t => t.Clone()).ToList();

            // ---- 9. 线路/方向模板 ----
            //clone.LineTemplates = CloneDictionary(this.LineTemplates, t => t.Clone());
            //clone.DirectionTemplates = CloneDictionary(this.DirectionTemplates, t => t.Clone());

            // ---- 10. 辅助映射 ----
            clone.StationCity = new Dictionary<string, string>(this.StationCity);
            clone.DefinedSymbols = new HashSet<string>(this.DefinedSymbols);
            //clone.Assertions = this.Assertions.Select(a => a.Clone()).ToList();
            //clone.EchoMessages = new List<string>(this.EchoMessages);

            // ---- 11. 警告 ----
            clone.Warnings = new List<string>(this.Warnings);

            return clone;
        }

        // ---------- 辅助克隆方法 ----------
        private static Dictionary<TKey, TValue> CloneDictionary<TKey, TValue>(Dictionary<TKey, TValue> src, Func<TValue, TValue> cloneValue)
        {
            var dst = new Dictionary<TKey, TValue>();
            foreach (var kv in src)
                dst[kv.Key] = cloneValue(kv.Value);
            return dst;
        }
    }
}