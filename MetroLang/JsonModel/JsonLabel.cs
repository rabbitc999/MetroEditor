using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 地图文本标注模型。
    /// </summary>
    public class JsonLabel
    {
        /// <summary>
        /// 标注显示的文本内容。
        /// </summary>
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// 经度（WGS-84 坐标系）。
        /// </summary>
        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        /// <summary>
        /// 纬度（WGS-84 坐标系）。
        /// </summary>
        [JsonPropertyName("lat")]
        public double Lat { get; set; }
    }
}