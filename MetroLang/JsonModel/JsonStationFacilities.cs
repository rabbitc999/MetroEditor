using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 车站无障碍及服务设施的集合。
    /// </summary>
    public class JsonStationFacilities
    {
        /// <summary>
        /// 普通卫生间信息。
        /// </summary>
        [JsonPropertyName("toilet")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonToiletInfo? Toilet { get; set; }

        /// <summary>
        /// 无障碍卫生间信息。
        /// </summary>
        [JsonPropertyName("accessible_toilet")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonToiletInfo? AccessibleToilet { get; set; }

        /// <summary>
        /// 电梯列表。
        /// </summary>
        [JsonPropertyName("elevators")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<JsonElevatorInfo>? Elevators { get; set; }

        /// <summary>
        /// 宽通道闸机信息。
        /// </summary>
        [JsonPropertyName("wide_gate")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonWideGateInfo? WideGate { get; set; }

        /// <summary>
        /// 自动扶梯列表。
        /// </summary>
        [JsonPropertyName("escalators")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<JsonEscalatorInfo>? Escalators { get; set; }

        /// <summary>
        /// 盲道铺设情况。
        /// </summary>
        [JsonPropertyName("tactile_paving")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonTactilePavingInfo? TactilePaving { get; set; }
    }
}