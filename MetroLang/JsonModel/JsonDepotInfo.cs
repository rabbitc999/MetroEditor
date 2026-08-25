using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 车辆段（停车场）信息，用于首末班车的始发/终到逻辑。
    /// </summary>
    public class JsonDepotInfo
    {
        /// <summary>
        /// 所属线路 ID。
        /// </summary>
        [JsonPropertyName("line_id")]
        public string LineId { get; set; } = string.Empty;

        /// <summary>
        /// 车辆段所在的接轨站 ID。
        /// </summary>
        [JsonPropertyName("station")]
        public string Station { get; set; } = string.Empty;

        /// <summary>
        /// 关联的方向 Key（可选，用于确定出入库方向）。
        /// </summary>
        [JsonPropertyName("direction_key")]
        public string? DirectionKey { get; set; }

        /// <summary>
        /// 出库/入库的发车时间（格式：HH:mm）。
        /// </summary>
        [JsonPropertyName("departure_time")]
        public string? DepartureTime { get; set; }
    }
}