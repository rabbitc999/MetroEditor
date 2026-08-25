namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 定义超过基础里程后的线性计价规则。
    /// </summary>
    public class LinearExtension
    {
        /// <summary>触发线性计价的阈值（里程或分区数）</summary>
        public double Threshold { get; set; }

        /// <summary>基础票价（达到阈值前的费用）</summary>
        public double BaseFare { get; set; }

        /// <summary>超过阈值后，每单位里程/分区的加价金额</summary>
        public double RatePerKm { get; set; }

        public LinearExtension Clone()
        {
            return new LinearExtension
            {
                Threshold = this.Threshold,
                BaseFare = this.BaseFare,
                RatePerKm = this.RatePerKm
            };
        }
    }
}