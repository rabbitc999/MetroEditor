namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 方向定义信息（编译阶段使用）。
    /// </summary>
    public class DirectionInfo
    {
        /// <summary>所属线路 ID</summary>
        public string LineId { get; set; } = string.Empty;

        /// <summary>依次经过的车站 ID 列表</summary>
        public List<string> Stations { get; set; } = new();

        /// <summary>方向属性集合（如 LOOP 等）</summary>
        public HashSet<string> Attrs { get; set; } = new();

        /// <summary>方向类型（主要用于环线区分 inner/outer）</summary>
        public string DirectionType { get; set; } = string.Empty;

        /// <summary>是否为远期规划方向</summary>
        public bool IsFuture { get; set; } = false;

        /// <summary>联络线接轨站 ID</summary>
        public string? ConnectStation { get; set; }

        /// <summary>经由的其他方向 ID 列表（用于复杂交路）</summary>
        public List<string> ViaDirections { get; set; } = new();

        /// <summary>路段类型列表（高架/地下/地面）</summary>
        public List<SegmentTypeInfo> SegmentTypes { get; set; } = new();

        /// <summary>
        /// 快车停靠站列表（空列表表示无快车服务）。
        /// 仅列出快车（大站快车）停靠的车站，未列出的车站快车不停靠。
        /// </summary>
        public List<string> ExpressStops { get; set; } = new();

        /// <summary>
        /// 交路信息列表（列车运行交路方案）。
        /// 每个交路包含名称与经过的站点列表，可定义多个交路（如大交路 + 小交路）。
        /// </summary>
        public List<RoutingInfo> Routings { get; set; } = new();

        /// <summary>
        /// 深拷贝当前方向信息，返回一个独立的副本。
        /// </summary>
        public DirectionInfo Clone()
        {
            return new DirectionInfo
            {
                LineId = this.LineId,
                Stations = new List<string>(this.Stations),
                Attrs = new HashSet<string>(this.Attrs),
                DirectionType = this.DirectionType,
                IsFuture = this.IsFuture,
                ConnectStation = this.ConnectStation,
                ViaDirections = new List<string>(this.ViaDirections),
                SegmentTypes = this.SegmentTypes.Select(s => s.Clone()).ToList(),
                ExpressStops = new List<string>(this.ExpressStops),
                Routings = this.Routings.Select(r => r.Clone()).ToList()
            };
        }
    }
}
