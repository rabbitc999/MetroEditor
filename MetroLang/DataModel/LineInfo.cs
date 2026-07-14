namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 线路基础信息（编译阶段使用）。
    /// </summary>
    public class LineInfo
    {
        /// <summary>线路官方名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>线路显示颜色（十六进制）</summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>所属城市标识</summary>
        public string City { get; set; } = string.Empty;

        /// <summary>列车编组形式（如 "6A"）</summary>
        public string? Formation { get; set; }

        /// <summary>设计最高速度（km/h）</summary>
        public double? Maxspeed { get; set; }

        /// <summary>开通年份</summary>
        public int OpenYear { get; set; } = 2000;

        /// <summary>牵引电机功率（kW）</summary>
        public double? PowerKw { get; set; }

        /// <summary>制动率</summary>
        public double? Brake { get; set; }

        /// <summary>是否已禁用/废弃</summary>
        public bool Disabled { get; set; }
    }
}
