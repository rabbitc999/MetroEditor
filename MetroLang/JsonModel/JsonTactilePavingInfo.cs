using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 盲道铺设详情。
    /// </summary>
    public class JsonTactilePavingInfo
    {
        /// <summary>
        /// 铺设状态：可用 (available)、部分可用 (partial)、无 (none)。
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "available";
    }
}