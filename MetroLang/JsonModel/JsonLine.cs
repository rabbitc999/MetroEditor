using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 线路的元数据及方向定义集合。
    /// </summary>
    public class JsonLine
    {
        /// <summary>
        /// 线路显示颜色（十六进制，如 #FF0000）。
        /// </summary>
        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// 列车编组形式（如 "6A", "8B"）。
        /// </summary>
        [JsonPropertyName("formation")]
        public string Formation { get; set; } = string.Empty;

        /// <summary>
        /// 线路开通年份。
        /// </summary>
        [JsonPropertyName("open_year")]
        public int OpenYear { get; set; }

        /// <summary>
        /// 所属城市标识。
        /// </summary>
        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// 线路设计最高速度（km/h）。
        /// </summary>
        [JsonPropertyName("maxspeed")]
        public double Maxspeed { get; set; }

        /// <summary>
        /// 相关外部链接（如维基百科）。
        /// </summary>
        [JsonPropertyName("link")]
        public string Link { get; set; } = string.Empty;

        /// <summary>
        /// 线路官方名称。
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 该线路包含的所有方向（上行、下行、环线等）。
        /// </summary>
        [JsonPropertyName("direction")]
        public List<JsonDirection> Direction { get; set; } = new();

        /// <summary>
        /// 牵引电机功率（kW），用于能耗模拟。
        /// </summary>
        [JsonPropertyName("power_kw")]
        public double? PowerKw { get; set; }

        /// <summary>
        /// 制动率，用于能耗模拟。
        /// </summary>
        [JsonPropertyName("brake")]
        public double? Brake { get; set; }

        /// <summary>
        /// 是否已废弃或隐藏。
        /// </summary>
        [JsonPropertyName("disabled")]
        public bool? Disabled { get; set; }

        /// <summary>
        /// 是否为特殊线路（如虚拟线、公交接驳线）。
        /// </summary>
        [JsonPropertyName("special")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsSpecial { get; set; }

        /// <summary>
        /// 图片Base64编码
        /// </summary>
        [JsonPropertyName("image")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ImageBase64 { get; set; }
    }
}