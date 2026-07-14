using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// MetroLang 编译器输出的根对象，对应最终生成的 JSON 文件结构。
    /// </summary>
    public class JsonRoot
    {
        /// <summary>
        /// 所属城市标识（如 "BJ", "SH"）。
        /// </summary>
        [JsonPropertyName("key_city")]
        public string KeyCity { get; set; } = string.Empty;

        /// <summary>
        /// 线路数据作者。
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// 数据版本号。
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "V1.0";

        /// <summary>
        /// 数据创建日期（格式：YYYY-MM-DD）。
        /// </summary>
        [JsonPropertyName("create_date")]
        public string CreateDate { get; set; } = "2026-01-01";

        /// <summary>
        /// 数据最近更新日期（格式：YYYY-MM-DD）。
        /// </summary>
        [JsonPropertyName("update_date")]
        public string UpdateDate { get; set; } = "2026-01-01";

        /// <summary>
        /// 运营通用参数配置（停站时间、换乘时间等）。
        /// </summary>
        [JsonPropertyName("operation_params")]
        public JsonOperationParams OperationParams { get; set; } = new();

        /// <summary>
        /// 基础票价配置。
        /// </summary>
        [JsonPropertyName("price")]
        public JsonPrice Price { get; set; } = new();

        /// <summary>
        /// 额外的票价规则（如跨城、特定线路组）。
        /// </summary>
        [JsonPropertyName("additional_price_rules")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<JsonPriceRule>? PriceRules { get; set; }

        /// <summary>
        /// 车站字典，Key 为车站 ID。
        /// </summary>
        [JsonPropertyName("stations")]
        public Dictionary<string, JsonStation> Stations { get; set; } = new();

        /// <summary>
        /// 线路字典，Key 为线路 ID。
        /// </summary>
        [JsonPropertyName("lines")]
        public Dictionary<string, JsonLine> Lines { get; set; } = new();

        /// <summary>
        /// 发车间隔规则列表。
        /// </summary>
        [JsonPropertyName("headway_rules")]
        public List<JsonHeadwayRule> HeadwayRules { get; set; } = new();

        /// <summary>
        /// 车站运营模式规则（如只进不出、只出不进）。
        /// </summary>
        [JsonPropertyName("station_modes")]
        public List<JsonStationModeRule> StationModes { get; set; } = new();

        /// <summary>
        /// 拥挤度因子规则列表。
        /// </summary>
        [JsonPropertyName("crowd_rules")]
        public List<JsonCrowdRule> CrowdRules { get; set; } = new();

        /// <summary>
        /// 线路运营时间规则列表。
        /// </summary>
        [JsonPropertyName("open_times")]
        public List<JsonOpenTimeRule> OpenTimes { get; set; } = new();

        /// <summary>
        /// 车辆段（停车场）信息列表。
        /// </summary>
        [JsonPropertyName("depots")]
        public List<JsonDepotInfo> Depots { get; set; } = new();

        /// <summary>
        /// 区间限速规则列表。
        /// </summary>
        [JsonPropertyName("speed_limits")]
        public List<JsonSpeedLimit> SpeedLimits { get; set; } = new();

        /// <summary>
        /// 虚拟换乘规则列表（如地面公交接驳、出站换乘）。
        /// </summary>
        [JsonPropertyName("virtual_transfers")]
        public List<JsonVirtualTransferRule> VirtualTransfers { get; set; } = new();

        /// <summary>
        /// 地图上的文本标注列表。
        /// </summary>
        [JsonPropertyName("labels")]
        public List<JsonLabel> Labels { get; set; } = new();

        /// <summary>
        /// 车站设施信息字典，Key 为车站 ID。
        /// </summary>
        [JsonPropertyName("facilities")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, JsonStationFacilities>? Facilities { get; set; }

        /// <summary>
        /// 优惠策略列表（如学生票、老人票）。
        /// </summary>
        [JsonPropertyName("promotions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<JsonPromotionPolicy>? Promotions { get; set; }
    }
}