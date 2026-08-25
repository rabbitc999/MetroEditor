namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 地图文本标注模型。
    /// </summary>
    public class Label
    {
        /// <summary>标注显示的文本内容</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>经度（WGS-84 坐标系）</summary>
        public double Lon { get; set; }

        /// <summary>纬度（WGS-84 坐标系）</summary>
        public double Lat { get; set; }

        public Label Clone()
        {
            return new Label
            {
                Text = this.Text,
                Lon = this.Lon,
                Lat = this.Lat
            };
        }
    }
}