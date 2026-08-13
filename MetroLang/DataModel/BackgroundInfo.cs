namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 背景图片定义（支持 SVG 或固定像素图片）。
    /// </summary>
    public class BackgroundInfo
    {
        /// <summary>背景显示名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>图片类型：svg 或 image</summary>
        public string Type { get; set; } = "image";

        /// <summary>图片内容（Base64 编码）</summary>
        public string SrcBase64 { get; set; } = string.Empty;

        /// <summary>地理范围 [minLon, minLat, maxLon, maxLat]</summary>
        public double[] Bounds { get; set; } = new double[4];

        /// <summary>透明度（0-1，默认 1）</summary>
        public double? Opacity { get; set; }

        /// <summary>深拷贝</summary>
        public BackgroundInfo Clone()
        {
            return new BackgroundInfo
            {
                Name = this.Name,
                Type = this.Type,
                SrcBase64 = this.SrcBase64,
                Bounds = (double[])this.Bounds?.Clone(),
                Opacity = this.Opacity
            };
        }
    }
}
