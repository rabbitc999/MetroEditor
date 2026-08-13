using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 线路下的具体运行方向定义。
    /// </summary>
    public class JsonDirection
    {
        /// <summary>
        /// 快车停靠站列表（仅列出快车停靠的车站，未列出的车站快车不停靠；空/null 表示无快车服务）。
        /// </summary>
        [JsonPropertyName("express_stops")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? ExpressStops { get; set; }

        /// <summary>
        /// 方向起点站 ID。
        /// </summary>
        [JsonPropertyName("start")]
        public string Start { get; set; } = string.Empty;

        /// <summary>
        /// 方向终点站 ID。
        /// </summary>
        [JsonPropertyName("end")]
        public string End { get; set; } = string.Empty;

        /// <summary>
        /// 是否为环线。
        /// </summary>
        [JsonPropertyName("is_loop")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsLoop { get; set; }

        /// <summary>
        /// 方向类型（如 "inner", "outer"，主要用于环线区分）。
        /// </summary>
        [JsonPropertyName("direction_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DirectionType { get; set; }

        /// <summary>
        /// 该方向依次经过的车站列表。
        /// </summary>
        [JsonPropertyName("station")]
        public List<JsonDirectionStation> Station { get; set; } = new();

        /// <summary>
        /// 是否为远期规划车站/方向。
        /// </summary>
        [JsonPropertyName("is_future")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsFuture { get; set; }

        /// <summary>
        /// 联络线连接的接轨站 ID。
        /// </summary>
        [JsonPropertyName("connect_station")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ConnectStation { get; set; }

        /// <summary>
        /// 经由的其他方向 Key 列表（用于复杂交路）。
        /// </summary>
        [JsonPropertyName("via_directions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> ViaDirections { get; set; } = new();

        /// <summary>
        /// 路段类型列表（高架/地下/地面），每段指定两站间的轨道敷设方式。
        /// </summary>
        [JsonPropertyName("segment_types")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<JsonSegmentType>? SegmentTypes { get; set; }

        /// <summary>
        /// 交路信息列表（列车运行交路方案），每个交路含名称与经过的站点。
        /// 空/null 表示无交路信息。
        /// </summary>
        [JsonPropertyName("routings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<JsonRouting>? Routings { get; set; }
    }
}