namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 定义单一票价区间段（阶梯票价的一部分）。
    /// </summary>
    public class Segment
    {
        /// <summary>区间起始值（里程或分区数）</summary>
        public double RangeStart { get; set; }

        /// <summary>区间结束值</summary>
        public double RangeEnd { get; set; }

        /// <summary>该区间内的固定票价</summary>
        public double Fare { get; set; }

        public Segment Clone()
        {
            return new Segment
            {
                RangeStart = this.RangeStart,
                RangeEnd = this.RangeEnd,
                Fare = this.Fare
            };
        }
    }
}