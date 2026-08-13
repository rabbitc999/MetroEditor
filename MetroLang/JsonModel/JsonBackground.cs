using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 背景图片定义（支持 SVG 或固定像素图片）。
    /// </summary>
    public class JsonBackground
    {
        /// <summary>背景 ID</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>背景显示名称</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>图片类型：svg 或 image</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "image";

        /// <summary>图片内容（Base64 编码）</summary>
        [JsonPropertyName("src")]
        public string Src { get; set; } = string.Empty;

        /// <summary>地理范围 [minLon, minLat, maxLon, maxLat]</summary>
        [JsonPropertyName("bounds")]
        public double[] Bounds { get; set; } = new double[4];

        /// <summary>透明度（0-1）</summary>
        [JsonPropertyName("opacity")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Opacity { get; set; }
    }
}
