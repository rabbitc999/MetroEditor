using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 垂直电梯设施详情。
    /// </summary>
    public class JsonElevatorInfo
    {
        /// <summary>
        /// 电梯位置描述。
        /// </summary>
        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// 是否为无障碍电梯。
        /// </summary>
        [JsonPropertyName("accessible")]
        public bool Accessible { get; set; } = true;
    }
}