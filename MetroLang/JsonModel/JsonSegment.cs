using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 定义单一票价区间段（阶梯票价的一部分）。
    /// </summary>
    public class JsonSegment
    {
        /// <summary>
        /// 区间起始值（里程或分区数）。
        /// </summary>
        [JsonPropertyName("rangeStart")]
        public double RangeStart { get; set; }

        /// <summary>
        /// 区间结束值。
        /// </summary>
        [JsonPropertyName("rangeEnd")]
        public double RangeEnd { get; set; }

        /// <summary>
        /// 该区间内的固定票价。
        /// </summary>
        [JsonPropertyName("fare")]
        public double Fare { get; set; }
    }
}