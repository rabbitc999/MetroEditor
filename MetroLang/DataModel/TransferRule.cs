namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 换乘规则定义。
    /// </summary>
    public class TransferRule
    {
        /// <summary>换乘站 ID</summary>
        public string Station { get; set; } = string.Empty;

        /// <summary>第一条线路 ID</summary>
        public string L1 { get; set; } = string.Empty;

        /// <summary>第二条线路 ID</summary>
        public string L2 { get; set; } = string.Empty;

        /// <summary>换乘状态（operating/out_station 等）</summary>
        public string Status { get; set; } = "operating";

        /// <summary>换乘耗时（秒）</summary>
        public double? Time { get; set; }

        /// <summary>是否需要重新安检</summary>
        public bool SecurityCheck { get; internal set; }

        /// <summary>换乘类型标识（用于模板匹配）</summary>
        public string TransferType { get; set; } = string.Empty;

        public TransferRule Clone()
        {
            return new TransferRule
            {
                Station = this.Station,
                L1 = this.L1,
                L2 = this.L2,
                Status = this.Status,
                Time = this.Time,
                SecurityCheck = this.SecurityCheck,
                TransferType = this.TransferType
            };
        }
    }
}
