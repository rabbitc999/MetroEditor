using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 卫生间设施详情。
    /// </summary>
    public class JsonToiletInfo
    {
        /// <summary>
        /// 位置：付费区 (paid_area) 或非付费区 (unpaid_area)。
        /// </summary>
        [JsonPropertyName("position")]
        public string Position { get; set; } = "paid_area";

        /// <summary>
        /// 是否配备无障碍设施。
        /// </summary>
        [JsonPropertyName("accessible")]
        public bool Accessible { get; set; }

        /// <summary>
        /// 性别类型：混合 (mixed)、男 (male)、女 (female)。
        /// </summary>
        [JsonPropertyName("gender")]
        public string Gender { get; set; } = "mixed";
    }
}