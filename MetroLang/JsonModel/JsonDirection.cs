using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 线路下的具体运行方向定义。
    /// </summary>
    public class JsonDirection
    {
        /// <summary>
        /// 是否为支线/存车线。
        /// </summary>
        [JsonPropertyName("is_branch")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsBranch { get; set; }

        /// <summary>
        /// 是否为快车/大站快车。
        /// </summary>
        [JsonPropertyName("is_fast")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsFast { get; set; }

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
        /// 方向别名（如 "往市区方向", "往机场方向"）。
        /// </summary>
        [JsonPropertyName("nick_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NickName { get; set; }

        /// <summary>
        /// 别名显示颜色。
        /// </summary>
        [JsonPropertyName("nick_color")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NickColor { get; set; }

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
    }
}