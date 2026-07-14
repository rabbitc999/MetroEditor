using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 全局运营参数配置，影响路径计算的核心逻辑。
    /// </summary>
    public class JsonOperationParams
    {
        /// <summary>
        /// 站内换乘步行时间（单位：秒）。默认 180 秒（3分钟）。
        /// </summary>
        [JsonPropertyName("inner_transfer_time")]
        public int InnerTransferTime { get; set; } = 180;

        /// <summary>
        /// 列车停站时间（单位：秒）。默认 30 秒。
        /// </summary>
        [JsonPropertyName("dwell_time")]
        public int DwellTime { get; set; } = 30;

        /// <summary>
        /// 出站再进站换乘的时间惩罚（单位：秒）。默认 300 秒（5分钟）。
        /// </summary>
        [JsonPropertyName("out_station_transfer_time")]
        public int OutStationTransferTime { get; set; } = 300;

        /// <summary>
        /// 进站闸机通行时间（单位：秒）。
        /// </summary>
        [JsonPropertyName("entry_time")]
        public int EntryTime { get; set; }
    }
}