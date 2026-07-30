using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 区域定义（商业区、住宅区等）
    /// </summary>
    public class ZoneInfo
    {
        /// <summary>区域显示名称</summary>
        public string Name { get; set; }

        /// <summary>区域类型（COMMERCIAL, RESIDENTIAL, CBD, ...）</summary>
        public string Type { get; set; }

        /// <summary>所属城市</summary>
        public string City { get; set; }

        /// <summary>区域颜色（前端热力图）</summary>
        public string Color { get; set; }

        /// <summary>中心点坐标 [经度, 纬度]</summary>
        public double[] Center { get; set; }

        /// <summary>圆形缓冲区半径（米）</summary>
        public double? Radius { get; set; }

        /// <summary>多边形边界，每个点为 [经度, 纬度]</summary>
        public List<double[]> Polygon { get; set; }

        /// <summary>关联站点列表</summary>
        public List<string> Stations { get; set; } = new();

        /// <summary>附加属性</summary>
        public ZoneAttributes Attributes { get; set; } = new();
    }

    /// <summary>区域附加属性</summary>
    public class ZoneAttributes
    {
        /// <summary>高峰时段列表（如 "08:00-09:00"）</summary>
        public List<string> PeakHours { get; set; } = new();

        /// <summary>人口密度（万人/km²）</summary>
        public double? PopulationDensity { get; set; }

        /// <summary>平均租金（元/月）</summary>
        public double? AvgRent { get; set; }
    }
}
