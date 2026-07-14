using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler
{
    public class Utils
    {

        // ====================================================================
        // 距离列表反转工具方法
        // 环线：直接反转列表
        // 非环线：去掉末尾 NIL，反转后再补回 NIL
        // ====================================================================
        public static List<double> ReverseDistanceList(List<double> distances, bool isLoop)
        {
            if (distances == null || distances.Count == 0)
                return new List<double>();

            if (isLoop)
            {
                // 环线：无 NIL 标记，直接反转
                var reversed = new List<double>(distances);
                reversed.Reverse();
                return reversed;
            }
            else
            {
                // 非环线：末尾通常为 NIL（1e50），反转时需要特殊处理
                var nonNilDistances = distances.Take(distances.Count - 1).ToList();
                nonNilDistances.Reverse();
                nonNilDistances.Add(1e50); // 重新添加 NIL 标记
                return nonNilDistances;
            }
        }

        // ====================================================================
        // 自动计算站间距：使用 Catmull-Rom 样条曲线估算地理距离
        // 参数：did - 方向标识符
        // 逻辑：基于车站坐标，通过样条插值计算平滑路径的长度
        // ====================================================================
        public static void CalcAutoDistances(Context context, string directionId)
        {
            var direction = context.Directions[directionId];
            var stations = direction.Stations;
            if (stations.Count < 2)
            {
                context.Distances[directionId] = new List<double>();
                return;
            }

            var coords = stations.Select(s => context.Stations[s]).ToList();
            var pts = new List<(double, double)>
            {
                (2 * coords[0].Lon - coords[1].Lon, 2 * coords[0].Lat - coords[1].Lat)
            };
            pts.AddRange(coords);

            // 兼容写法：倒数第二个元素（不用 ^2，也不用 [2]）
            int lastIdx = coords.Count - 1;
            int secondLastIdx = coords.Count - 2;
            pts.Add((
                2 * coords[lastIdx].Lon - coords[secondLastIdx].Lon,
                2 * coords[lastIdx].Lat - coords[secondLastIdx].Lat
            ));

            var dists = new List<double>();
            for (int i = 1; i < pts.Count - 2; i++)
                dists.Add(CatmullRomArcLength(pts[i - 1], pts[i], pts[i + 1], pts[i + 2]));

            if (direction.Attrs.Contains("LOOP"))
            {
                int n = pts.Count;
                dists.Add(CatmullRomArcLength(
                    pts[n - 3],
                    pts[n - 2],
                    pts[n - 1],
                    (
                        2 * pts[n - 1].Item1 - pts[n - 2].Item1,
                        2 * pts[n - 1].Item2 - pts[n - 2].Item2
                    )
                ));
            }
            else
            {
                dists.Add(1e50);
            }

            context.Distances[directionId] = dists;
        }

        // ====================================================================
        // Catmull-Rom 样条曲线弧长计算
        // 参数：p0-p3 - 四个控制点，samples - 采样点数
        // 返回：曲线段的近似弧长（公里）
        // 算法：通过参数方程采样，累加相邻采样点的球面距离
        // ====================================================================
        private static double CatmullRomArcLength(
            (double Lon, double Lat) p0, (double Lon, double Lat) p1,
            (double Lon, double Lat) p2, (double Lon, double Lat) p3,
            int samples = 40)
        {
            // Catmull-Rom 样条参数方程
            static (double Lon, double Lat) Catmull(double t,
                (double Lon, double Lat) p0, (double Lon, double Lat) p1,
                (double Lon, double Lat) p2, (double Lon, double Lat) p3)
            {
                double t2 = t * t, t3 = t2 * t;
                double lon = 0.5 * (2 * p1.Lon + (-p0.Lon + p2.Lon) * t +
                                   (2 * p0.Lon - 5 * p1.Lon + 4 * p2.Lon - p3.Lon) * t2 +
                                   (-p0.Lon + 3 * p1.Lon - 3 * p2.Lon + p3.Lon) * t3);
                double lat = 0.5 * (2 * p1.Lat + (-p0.Lat + p2.Lat) * t +
                                   (2 * p0.Lat - 5 * p1.Lat + 4 * p2.Lat - p3.Lat) * t2 +
                                   (-p0.Lat + 3 * p1.Lat - 3 * p2.Lat + p3.Lat) * t3);
                return (lon, lat);
            }

            // 从起点开始积分
            var previousPoint = Catmull(0, p0, p1, p2, p3);
            double totalLength = 0;

            // 采样累加：计算相邻采样点间的球面距离
            for (int i = 1; i <= samples; i++)
            {
                double t = (double)i / samples;
                var currentPoint = Catmull(t, p0, p1, p2, p3);
                totalLength += Haversine(previousPoint.Lat, previousPoint.Lon, currentPoint.Lat, currentPoint.Lon);
                previousPoint = currentPoint;
            }
            return totalLength;
        }

        // ====================================================================
        // 哈弗辛公式：计算球面上两点间的大圆距离
        // 参数：lat1, lon1 - 第一点经纬度；lat2, lon2 - 第二点经纬度
        // 返回：两点间的距离（公里）
        // 算法：基于球面三角学，考虑了地球曲率
        // ====================================================================
        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadius = 6371.0; // 地球平均半径（公里）

            // 角度转弧度
            double deltaLat = (lat2 - lat1) * Math.PI / 180.0;
            double deltaLon = (lon2 - lon1) * Math.PI / 180.0;

            // 哈弗辛公式核心计算
            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            // 反哈弗辛函数求距离
            return EarthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        // ====================================================================
        // 特殊票价规则添加辅助方法
        // ====================================================================
        public static void AddSpecialFare(Context context, string lineId, string type, double price, string? stationA = null, string? stationB = null)
        {
            var config = context.PriceConfig;
            var specialLine = config.SpecialLines.FirstOrDefault(s => s.LineId == lineId);
            if (specialLine == null)
            {
                specialLine = new SpecialLine { LineId = lineId };
                config.SpecialLines.Add(specialLine);
            }

            if (type == "all")
                specialLine.All = price;
            else if (type == "segment")
            {
                if (specialLine.Surcharge == null)
                    specialLine.Surcharge = new List<SurchargeItem>();
                specialLine.Surcharge.Add(new SurchargeItem
                {
                    NodeA = stationA ?? "",
                    NodeB = stationB ?? "",
                    Price = price
                });
                specialLine.Surcharge.Add(new SurchargeItem
                {
                    NodeA = stationB ?? "",
                    NodeB = stationA ?? "",
                    Price = price
                });
            }
        }

        /// <summary>
        /// 将内部方向 ID（如 "B9_0"）转换为外部格式 "LineId|Start|End"
        /// 用于 JSON 输出时与外部系统交互，隐藏内部实现细节
        /// </summary>
        /// <param name="internalDirKey">编译器内部使用的方向标识符</param>
        /// <returns>外部可读的方向键，若无法解析则返回原值</returns>
        public static string ConvertToExternalDirKey(Context context, string internalDirKey)
        {
            // 尝试从已解析的方向字典中获取对应的方向信息
            if (context.Directions.TryGetValue(internalDirKey, out var dir))
            {
                var lineId = dir.LineId;
                var stations = dir.Stations;
                // 确保方向至少包含两个车站（起点和终点）
                if (stations.Count >= 2)
                {
                    // 提取方向序列的第一个（起点）和最后一个（终点）车站
                    var start = stations.First();
                    var end = stations.Last();
                    // 组合成外部约定的格式：线路ID|起点站|终点站
                    return $"{lineId}|{start}|{end}";
                }
            }
            // 若方向未定义或车站不足，保留原内部ID作为容错手段
            return internalDirKey;
        }
    }
}
