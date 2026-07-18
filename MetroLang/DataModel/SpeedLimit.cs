namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 区间限速规则。
    /// </summary>
    public class SpeedLimit
    {
        /// <summary>目标方向 Key</summary>
        public string DirectionKey { get; set; } = string.Empty;

        /// <summary>限速区间起点站</summary>
        public string FromStation { get; set; } = string.Empty;

        /// <summary>限速区间终点站</summary>
        public string ToStation { get; set; } = string.Empty;

        /// <summary>最高允许速度（km/h）</summary>
        public int SpeedKmh { get; set; }

        public SpeedLimit Clone()
        {
            return new SpeedLimit
            {
                DirectionKey = this.DirectionKey,
                FromStation = this.FromStation,
                ToStation = this.ToStation,
                SpeedKmh = this.SpeedKmh
            };
        }
    }
}
