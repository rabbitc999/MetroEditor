namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 交路信息（列车运行交路方案）。
    /// 每个交路由名称 + 实际经过的站点列表构成。
    /// 一个方向可包含多个交路（如大交路、小交路、快慢车交路）。
    /// </summary>
    public class RoutingInfo
    {
        /// <summary>交路名称（如 "全程交路"、"机场北交路"）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>该交路依次经过的车站 ID 列表（须为方向站点序列的子集）</summary>
        public List<string> Stations { get; set; } = new();

        /// <summary>深拷贝当前交路信息。</summary>
        public RoutingInfo Clone()
        {
            return new RoutingInfo
            {
                Name = this.Name,
                Stations = new List<string>(this.Stations)
            };
        }
    }
}
