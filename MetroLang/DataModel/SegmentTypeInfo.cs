namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 线路路段类型信息（高架/地下/地面）。
    /// </summary>
    public class SegmentTypeInfo
    {
        /// <summary>起始车站 ID</summary>
        public string FromStation { get; set; } = string.Empty;

        /// <summary>终点车站 ID</summary>
        public string ToStation { get; set; } = string.Empty;

        /// <summary>路段类型：ELEVATED(高架)、UNDERGROUND(地下)、GROUND(地面)</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>深拷贝</summary>
        public SegmentTypeInfo Clone()
        {
            return new SegmentTypeInfo
            {
                FromStation = this.FromStation,
                ToStation = this.ToStation,
                Type = this.Type
            };
        }
    }
}
