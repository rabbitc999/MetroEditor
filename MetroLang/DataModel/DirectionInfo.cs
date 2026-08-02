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

        /// <summary>方向属性集合（如 LOOP, FAST, BRANCH 等）</summary>
        public HashSet<string> Attrs { get; set; } = new();

        /// <summary>方向别名（如 "往市区方向"）</summary>
        public string? Alias { get; set; }

        /// <summary>别名显示颜色</summary>
        public string? AliasColor { get; set; }

        /// <summary>方向类型（主要用于环线区分 inner/outer）</summary>
        public string DirectionType { get; set; } = string.Empty;

        /// <summary>是否为远期规划方向</summary>
        public bool IsFuture { get; set; } = false;

        /// <summary>联络线接轨站 ID</summary>
        public string? ConnectStation { get; set; }

        /// <summary>经由的其他方向 ID 列表（用于复杂交路）</summary>
        public List<string> ViaDirections { get; set; } = new();

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
                Alias = this.Alias,
                AliasColor = this.AliasColor,
                DirectionType = this.DirectionType,
                IsFuture = this.IsFuture,
                ConnectStation = this.ConnectStation,
                ViaDirections = new List<string>(this.ViaDirections)
            };
        }
    }
}
