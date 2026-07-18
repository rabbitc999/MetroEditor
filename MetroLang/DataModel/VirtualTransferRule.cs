namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 虚拟换乘规则（出站换乘/虚拟换乘）。
    /// </summary>
    public class VirtualTransferRule
    {
        /// <summary>换乘起点站 ID</summary>
        public string StationA { get; set; } = string.Empty;

        /// <summary>换乘终点站 ID</summary>
        public string StationB { get; set; } = string.Empty;

        /// <summary>换乘耗时（分钟）</summary>
        public double TimeMinutes { get; set; }

        /// <summary>是否需要重新安检</summary>
        public bool SecurityCheck { get; set; }

        /// <summary>换乘必经方向 ID 列表</summary>
        public List<string> ViaDirections { get; set; } = new();

        public VirtualTransferRule Clone()
        {
            return new VirtualTransferRule
            {
                StationA = this.StationA,
                StationB = this.StationB,
                TimeMinutes = this.TimeMinutes,
                SecurityCheck = this.SecurityCheck,
                ViaDirections = new List<string>(this.ViaDirections)
            };
        }
    }
}
