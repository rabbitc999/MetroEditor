using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// JSON 中的区域/分区描述，可用圆形半径或多边形边界表达空间范围。
    /// </summary>
    public class JsonZone
    {
        /// <summary>区域唯一标识。</summary>
        public string Id { get; set; }
        /// <summary>面向用户显示的区域名称。</summary>
        public string Name { get; set; }
        /// <summary>区域类型，例如行政区、商业区或自定义分区。</summary>
        public string Type { get; set; }
        /// <summary>区域所属城市；为空时通常继承根对象城市。</summary>
        public string City { get; set; }
        /// <summary>用于地图绘制的颜色字符串。</summary>
        public string Color { get; set; }
        /// <summary>圆形区域中心点，约定为经度和纬度数组。</summary>
        public double[] Center { get; set; }
        /// <summary>圆形区域半径，单位由导出协议约定。</summary>
        public double? Radius { get; set; }
        /// <summary>多边形边界点列表，每个点为经纬度数组。</summary>
        public List<double[]> Polygon { get; set; }
        /// <summary>属于该区域的车站名称列表。</summary>
        public List<string> Stations { get; set; }
        /// <summary>可选的统计和运营属性。</summary>
        public JsonZoneAttributes Attributes { get; set; }
    }

    /// <summary>
    /// 区域的可选统计属性，字段缺失时保持为 <see langword="null"/>。
    /// </summary>
    public class JsonZoneAttributes
    {
        /// <summary>高峰时段文本列表。</summary>
        public List<string> PeakHours { get; set; }
        /// <summary>人口密度指标。</summary>
        public double? PopulationDensity { get; set; }
        /// <summary>平均租金指标。</summary>
        public double? AvgRent { get; set; }
    }
}
