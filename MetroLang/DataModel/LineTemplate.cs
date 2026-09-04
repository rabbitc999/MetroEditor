// LineTemplate.cs
namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 线路定义模板，集中保存线路元数据和运营参数的默认值。
    /// </summary>
    public class LineTemplate
    {
        /// <summary>线路显示名称。</summary>
        public string? Name { get; set; }
        /// <summary>线路颜色。</summary>
        public string? Color { get; set; }
        /// <summary>线路所属城市。</summary>
        public string? City { get; set; }
        /// <summary>线路开通年份。</summary>
        public int? OpenYear { get; set; }
        /// <summary>列车编组形式。</summary>
        public string? Formation { get; set; }
        /// <summary>最高运行速度（km/h）。</summary>
        public double? Maxspeed { get; set; }
        /// <summary>牵引功率（kW）。</summary>
        public double? PowerKw { get; set; }
        /// <summary>制动参数。</summary>
        public double? Brake { get; set; }
        /// <summary>是否停用该线路。</summary>
        public bool? Disabled { get; set; }
        /// <summary>线路图片的 Base64 内容。</summary>
        public string? ImageBase64 { get; set; }
        /// <summary>线路外部链接。</summary>
        public string? Link { get; set; }
        /// <summary>是否标记为特殊线路。</summary>
        public bool? IsSpecial { get; set; }
        /// <summary>继承的父模板 ID。</summary>
        public string? Inherits { get; set; }  // 父模板名
    }
}
