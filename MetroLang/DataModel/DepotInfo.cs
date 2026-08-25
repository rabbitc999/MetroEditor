namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 车辆段（停车场）信息。
    /// </summary>
    public class DepotInfo
    {
        /// <summary>所属线路 ID</summary>
        public string LineId { get; set; } = string.Empty;

        /// <summary>接轨站 ID</summary>
        public string Station { get; set; } = string.Empty;

        /// <summary>关联的方向 Key</summary>
        public string DirectionKey { get; set; } = string.Empty;

        /// <summary>出库/入库发车时间</summary>
        public TimeSpan? DepartureTime { get; set; }

        public DepotInfo Clone()
        {
            return new DepotInfo
            {
                LineId = this.LineId,
                Station = this.Station,
                DirectionKey = this.DirectionKey,
                DepartureTime = this.DepartureTime
            };
        }
    }
}
