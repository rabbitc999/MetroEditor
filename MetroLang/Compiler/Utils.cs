using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 编译器工具类，提供路网数据操作、地理距离计算、通配符匹配等通用辅助方法。
    /// <para>主要职责包括：</para>
    /// <list type="bullet">
    /// <item>站间距离计算（Catmull-Rom 样条 + Haversine 公式）</item>
    /// <item>线路/方向/车站的复制、重命名、删除、合并、拆分等批量操作</item>
    /// <item>方向键格式转换（内部 ID ↔ 外部 LineId|Start|End 格式）</item>
    /// <item>通配符模式匹配（支持 <c>*</c> 通配符）</item>
    /// <item>线路/方向模板继承解析</item>
    /// </list>
    /// </summary>
    public class Utils
    {

        /// <summary>
        /// 反转距离列表。环线直接反转；非环线去掉末尾 NIL 标记后反转再补回。
        /// </summary>
        /// <param name="distances">原始距离列表</param>
        /// <param name="isLoop">是否为环线</param>
        /// <returns>反转后的距离列表</returns>
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

        /// <summary>
        /// 使用 Catmull-Rom 样条曲线自动计算方向中相邻站间的地理距离。
        /// <para>基于车站经纬度坐标，通过样条插值生成平滑路径并累加球面距离。
        /// 环线方向会额外计算首尾站间的闭合距离。</para>
        /// </summary>
        /// <param name="context">编译上下文</param>
        /// <param name="directionId">方向标识符</param>
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

        /// <summary>
        /// 使用 Catmull-Rom 样条曲线计算四控制点间曲线段的近似弧长（公里）。
        /// </summary>
        /// <param name="p0">第一个控制点（前驱）</param>
        /// <param name="p1">第二个控制点（起点）</param>
        /// <param name="p2">第三个控制点（终点）</param>
        /// <param name="p3">第四个控制点（后继）</param>
        /// <param name="samples">采样点数，默认 40</param>
        /// <returns>曲线段的近似弧长（公里）</returns>
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

        /// <summary>
        /// 使用哈弗辛（Haversine）公式计算球面上两点间的大圆距离。
        /// </summary>
        /// <param name="lat1">第一点纬度</param>
        /// <param name="lon1">第一点经度</param>
        /// <param name="lat2">第二点纬度</param>
        /// <param name="lon2">第二点经度</param>
        /// <returns>两点间距离（公里），基于地球平均半径 6371km</returns>
        public static double Haversine(double lat1, double lon1, double lat2, double lon2)
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

        /// <summary>
        /// 向指定线路添加特殊票价规则（统一票价或区间附加费）。
        /// </summary>
        /// <param name="context">编译上下文</param>
        /// <param name="lineId">线路 ID</param>
        /// <param name="type">票价类型：<c>"all"</c> 为统一票价，<c>"segment"</c> 为区间附加费</param>
        /// <param name="price">票价金额</param>
        /// <param name="stationA">区间起点站（仅 type=segment 时使用）</param>
        /// <param name="stationB">区间终点站（仅 type=segment 时使用）</param>
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

        /// <summary>
        /// 合并两个方向（首尾相接），返回合并后的站点列表和距离列表。
        /// </summary>
        /// <param name="dirA">第一个方向</param>
        /// <param name="dirB">第二个方向</param>
        /// <param name="distA">第一个方向的距离列表</param>
        /// <param name="distB">第二个方向的距离列表</param>
        /// <returns>合并后的 (站点列表, 距离列表)</returns>
        /// <exception cref="InvalidOperationException">方向为环线或无公共站点时抛出</exception>
        public static (List<string> stations, List<double> distances) MergeDirections(
            DirectionInfo dirA, DirectionInfo dirB,
            List<double> distA, List<double> distB)
        {
            if (dirA.Attrs.Contains("LOOP") || dirB.Attrs.Contains("LOOP"))
                throw new InvalidOperationException("不能合并环线方向");

            var stationsA = dirA.Stations;
            var stationsB = dirB.Stations;
            int nA = stationsA.Count, nB = stationsB.Count;

            // 找到最长公共后缀-前缀
            int L = 0;
            for (int len = Math.Min(nA, nB); len >= 1; len--)
            {
                if (stationsA.Skip(nA - len).SequenceEqual(stationsB.Take(len)))
                {
                    L = len;
                    break;
                }
            }
            if (L == 0)
                throw new InvalidOperationException($"方向 '{dirA}' 与 '{dirB}' 没有公共站点，无法合并");

            int startA = nA - L;
            bool isEndOverlap = (startA + L == nA);

            // 合并站点
            List<string> mergedStations;
            if (isEndOverlap)
                mergedStations = stationsA.Concat(stationsB.Skip(L)).ToList();
            else
                mergedStations = stationsA.Take(startA + 1).Concat(stationsB.Skip(L)).ToList();

            // 合并距离
            List<double> mergedDist;
            if (isEndOverlap)
                mergedDist = distA.Take(nA - 1).Concat(distB.Skip(L - 1)).ToList();
            else
                mergedDist = distA.Take(startA).Concat(distB.Skip(L - 1)).ToList();

            return (mergedStations, mergedDist);
        }

        /// <summary>
        /// 构建外部方向键格式：<c>LineId|Start|End</c>
        /// </summary>
        public static string BuildExternalKey(string lineId, string start, string end)
        {
            return $"{lineId}|{start}|{end}";
        }

        /// <summary>
        /// 通配符匹配：支持单个 <c>*</c> 通配符，匹配任意字符串。
        /// </summary>
        /// <param name="pattern">匹配模式（可含一个 *）</param>
        /// <param name="input">待匹配的输入字符串</param>
        /// <returns>(是否匹配, 通配符捕获的内容)</returns>
        public static (bool matches, string captured) MatchWildcard(string pattern, string input)
        {
            // 支持一个 '*'，匹配任意字符串
            if (!pattern.Contains('*'))
                return (input == pattern, null);

            string before = pattern.Substring(0, pattern.IndexOf('*'));
            string after = pattern.Substring(pattern.IndexOf('*') + 1);
            if (!input.StartsWith(before) || !input.EndsWith(after))
                return (false, null);

            int start = before.Length;
            int end = input.Length - after.Length;
            string captured = input.Substring(start, end - start);
            return (true, captured);
        }

        /// <summary>
        /// 将通配符模式中的 <c>*</c> 替换为捕获内容。
        /// </summary>
        public static string ApplyWildcard(string pattern, string capture)
        {
            return pattern.Replace("*", capture);
        }

        /// <summary>
        /// 深度复制一条线路及其所有关联数据（方向、距离、票价、发车间隔、运营时间、车辆段等）。
        /// </summary>
        /// <param name="context">编译上下文</param>
        /// <param name="sourceLineId">源线路 ID</param>
        /// <param name="targetLineId">目标线路 ID</param>
        public static void CopyLine(Context context, string sourceLineId, string targetLineId)
        {
            if (!context.Lines.TryGetValue(sourceLineId, out var sourceLine))
                throw new InvalidOperationException($"线路 '{sourceLineId}' 不存在");
            if (context.Lines.ContainsKey(targetLineId))
                throw new InvalidOperationException($"线路 '{targetLineId}' 已存在");

            // 复制线路基本信息
            var newLine = new LineInfo
            {
                Name = sourceLine.Name,
                Color = sourceLine.Color,
                City = sourceLine.City,
                OpenYear = sourceLine.OpenYear,
                Formation = sourceLine.Formation,
                Maxspeed = sourceLine.Maxspeed,
                PowerKw = sourceLine.PowerKw,
                Brake = sourceLine.Brake,
                Disabled = sourceLine.Disabled,
                ImageBase64 = sourceLine.ImageBase64
            };
            context.Lines[targetLineId] = newLine;

            // 复制所有方向（深度复制）
            var dirsToCopy = context.Directions.Where(kv => kv.Value.LineId == sourceLineId).ToList();
            foreach (var (dirId, dirInfo) in dirsToCopy)
            {
                string newDirId = dirId.Replace(sourceLineId, targetLineId); // 简单替换，可能需更智能
                if (context.Directions.ContainsKey(newDirId))
                    throw new InvalidOperationException($"方向ID '{newDirId}' 已存在，无法复制");
                var newDir = new DirectionInfo
                {
                    LineId = targetLineId,
                    Stations = new List<string>(dirInfo.Stations),
                    Attrs = new HashSet<string>(dirInfo.Attrs),
                    DirectionType = dirInfo.DirectionType,
                    IsFuture = dirInfo.IsFuture,
                    ConnectStation = dirInfo.ConnectStation,
                    ViaDirections = new List<string>(dirInfo.ViaDirections),
                    SegmentTypes = dirInfo.SegmentTypes.Select(s => s.Clone()).ToList(),
                    ExpressStops = new List<string>(dirInfo.ExpressStops),
                    Routings = dirInfo.Routings.Select(r => r.Clone()).ToList()
                };
                context.Directions[newDirId] = newDir;
                if (context.Distances.TryGetValue(dirId, out var dist))
                    context.Distances[newDirId] = new List<double>(dist);
            }

            // 复制线路链接
            if (context.LineLinks.TryGetValue(sourceLineId, out var link))
                context.LineLinks[targetLineId] = link;

            // 复制特殊线路标记
            if (context.SpecialLineIds.Contains(sourceLineId))
                context.SpecialLineIds.Add(targetLineId);

            // 复制票价特殊线路配置（若存在）
            foreach (var specialLine in context.PriceConfig.SpecialLines)
            {
                if (specialLine.LineId == sourceLineId)
                {
                    var newSpecial = new SpecialLine
                    {
                        LineId = targetLineId,
                        All = specialLine.All,
                        Surcharge = specialLine.Surcharge?.Select(s => new SurchargeItem { NodeA = s.NodeA, NodeB = s.NodeB, Price = s.Price }).ToList(),
                        UseLines = specialLine.UseLines?.Select(l => l.Replace(sourceLineId, targetLineId)).ToList(),
                        DefaultFare = specialLine.DefaultFare,
                        Segments = specialLine.Segments?.Select(s => new Segment { RangeStart = s.RangeStart, RangeEnd = s.RangeEnd, Fare = s.Fare }).ToList(),
                        LinearExtension = specialLine.LinearExtension != null ? new LinearExtension { Threshold = specialLine.LinearExtension.Threshold, BaseFare = specialLine.LinearExtension.BaseFare, RatePerKm = specialLine.LinearExtension.RatePerKm } : null
                    };
                    context.PriceConfig.SpecialLines.Add(newSpecial);
                }
            }

            // 复制发车间隔规则
            foreach (var rule in context.HeadwayRules.Where(r => r.LineId == sourceLineId))
            {
                context.HeadwayRules.Add(new HeadwayRule
                {
                    LineId = targetLineId,
                    StartTime = rule.StartTime,
                    EndTime = rule.EndTime,
                    IntervalSeconds = rule.IntervalSeconds,
                    Days = new List<DayOfWeek>(rule.Days)
                });
            }

            // 复制运营时间规则
            foreach (var rule in context.OpenTimes.Where(r => r.LineId == sourceLineId))
            {
                context.OpenTimes.Add(new OpenTimeRule
                {
                    LineId = targetLineId,
                    StartTime = rule.StartTime,
                    EndTime = rule.EndTime,
                    Days = new List<DayOfWeek>(rule.Days)
                });
            }

            // 复制车辆段信息
            foreach (var depot in context.Depots.Where(d => d.LineId == sourceLineId))
            {
                context.Depots.Add(new DepotInfo
                {
                    LineId = targetLineId,
                    Station = depot.Station,
                    DirectionKey = depot.DirectionKey, // 可能需调整，暂不处理
                    DepartureTime = depot.DepartureTime
                });
            }

            // 复制停站时间
            var dwellKeys = context.DwellTimes.Keys.Where(k => k.LineId == sourceLineId).ToList();
            foreach (var key in dwellKeys)
            {
                context.DwellTimes[(key.Station, targetLineId)] = context.DwellTimes[key];
            }

            // 复制车站状态（线路相关）
            var statusKeys = context.StationStatus.Keys.Where(k => k.LineId == sourceLineId).ToList();
            foreach (var key in statusKeys)
            {
                context.StationStatus[(key.Station, targetLineId)] = context.StationStatus[key];
            }
        }

        /// <summary>
        /// 删除一条线路及其所有关联数据。
        /// </summary>
        /// <param name="context">编译上下文</param>
        /// <param name="lineId">要删除的线路 ID</param>
        public static void DeleteLine(Context context, string lineId)
        {
            if (!context.Lines.ContainsKey(lineId))
                return; // 或抛出异常

            // 删除所有方向
            var dirsToDelete = context.Directions.Where(kv => kv.Value.LineId == lineId).Select(kv => kv.Key).ToList();
            foreach (var dirId in dirsToDelete)
            {
                context.Directions.Remove(dirId);
                context.Distances.Remove(dirId);
            }

            // 删除线路
            context.Lines.Remove(lineId);
            context.LineLinks.Remove(lineId);
            context.SpecialLineIds.Remove(lineId);

            // 删除票价特殊线路配置
            context.PriceConfig.SpecialLines.RemoveAll(s => s.LineId == lineId);

            // 删除发车间隔规则
            context.HeadwayRules.RemoveAll(r => r.LineId == lineId);

            // 删除运营时间规则
            context.OpenTimes.RemoveAll(r => r.LineId == lineId);

            // 删除车辆段
            context.Depots.RemoveAll(d => d.LineId == lineId);

            // 删除停站时间
            var dwellKeys = context.DwellTimes.Keys.Where(k => k.LineId == lineId).ToList();
            foreach (var key in dwellKeys)
                context.DwellTimes.Remove(key);

            // 删除车站状态（线路相关）
            var statusKeys = context.StationStatus.Keys.Where(k => k.LineId == lineId).ToList();
            foreach (var key in statusKeys)
                context.StationStatus.Remove(key);
        }

        /// <summary>
        /// 重命名线路 ID，同步更新所有引用该线路的关联数据。
        /// </summary>
        /// <param name="context">编译上下文</param>
        /// <param name="oldId">旧线路 ID</param>
        /// <param name="newId">新线路 ID</param>
        public static void RenameLine(Context context, string oldId, string newId)
        {
            if (!context.Lines.ContainsKey(oldId))
                throw new InvalidOperationException($"线路 '{oldId}' 不存在");
            if (context.Lines.ContainsKey(newId))
                throw new InvalidOperationException($"线路 '{newId}' 已存在");

            // 更新线路字典
            var line = context.Lines[oldId];
            context.Lines.Remove(oldId);
            context.Lines[newId] = line;

            // 更新所有方向的 LineId
            foreach (var dir in context.Directions.Values.Where(d => d.LineId == oldId))
                dir.LineId = newId;

            // 更新 LineLinks
            if (context.LineLinks.TryGetValue(oldId, out var link))
            {
                context.LineLinks.Remove(oldId);
                context.LineLinks[newId] = link;
            }

            // 更新特殊线路标记
            if (context.SpecialLineIds.Contains(oldId))
            {
                context.SpecialLineIds.Remove(oldId);
                context.SpecialLineIds.Add(newId);
            }

            // 更新票价特殊线路配置
            foreach (var special in context.PriceConfig.SpecialLines)
            {
                if (special.LineId == oldId)
                    special.LineId = newId;
                if (special.UseLines != null)
                {
                    for (int i = 0; i < special.UseLines.Count; i++)
                        if (special.UseLines[i] == oldId)
                            special.UseLines[i] = newId;
                }
            }

            // 更新发车间隔
            foreach (var rule in context.HeadwayRules.Where(r => r.LineId == oldId))
                rule.LineId = newId;

            // 更新运营时间
            foreach (var rule in context.OpenTimes.Where(r => r.LineId == oldId))
                rule.LineId = newId;

            // 更新车辆段
            foreach (var depot in context.Depots.Where(d => d.LineId == oldId))
                depot.LineId = newId;

            // 更新停站时间
            var dwellKeys = context.DwellTimes.Keys.Where(k => k.LineId == oldId).ToList();
            foreach (var key in dwellKeys)
            {
                var value = context.DwellTimes[key];
                context.DwellTimes.Remove(key);
                context.DwellTimes[(key.Station, newId)] = value;
            }

            // 更新车站状态
            var statusKeys = context.StationStatus.Keys.Where(k => k.LineId == oldId).ToList();
            foreach (var key in statusKeys)
            {
                var value = context.StationStatus[key];
                context.StationStatus.Remove(key);
                context.StationStatus[(key.Station, newId)] = value;
            }
        }

        /// <summary>
        /// 检查车站是否被任何方向引用
        /// </summary>
        public static bool IsStationUsed(Context context, string stationName)
        {
            return context.Directions.Values.Any(d => d.Stations.Contains(stationName));
        }

        /// <summary>
        /// 获取引用该车站的所有方向ID
        /// </summary>
        public static List<string> GetDirectionsUsingStation(Context context, string stationName)
        {
            return context.Directions.Where(kv => kv.Value.Stations.Contains(stationName))
                                     .Select(kv => kv.Key)
                                     .ToList();
        }

        /// <summary>
        /// 在方向中替换车站名称（重命名）
        /// </summary>
        public static void ReplaceStationInDirections(Context context, string oldName, string newName)
        {
            foreach (var dir in context.Directions.Values)
            {
                for (int i = 0; i < dir.Stations.Count; i++)
                {
                    if (dir.Stations[i] == oldName)
                        dir.Stations[i] = newName;
                }
            }
        }

        /// <summary>
        /// 复制车站坐标、城市、别名、设施、停站时间、车站状态等
        /// </summary>
        public static void CopyStation(Context context, string sourceStation, string targetStation, bool copyDwellTimes = true, bool copyStatus = true)
        {
            if (context.Stations.TryGetValue(sourceStation, out var coord))
                context.Stations[targetStation] = coord;
            else
                throw new InvalidOperationException($"车站 '{sourceStation}' 不存在");

            if (context.StationCity.TryGetValue(sourceStation, out var city))
                context.StationCity[targetStation] = city;

            if (context.StationOtherNames.TryGetValue(sourceStation, out var names))
                context.StationOtherNames[targetStation] = new List<string>(names);

            if (context.Facilities.TryGetValue(sourceStation, out var fac))
                context.Facilities[targetStation] = fac; // 浅复制，若需深复制可扩展

            if (copyDwellTimes)
            {
                foreach (var key in context.DwellTimes.Keys.Where(k => k.Station == sourceStation).ToList())
                {
                    var value = context.DwellTimes[key];
                    context.DwellTimes[(targetStation, key.LineId)] = value;
                }
            }

            if (copyStatus)
            {
                foreach (var key in context.StationStatus.Keys.Where(k => k.Station == sourceStation).ToList())
                {
                    var value = context.StationStatus[key];
                    context.StationStatus[(targetStation, key.LineId)] = value;
                }
            }
        }

        /// <summary>
        /// 删除车站及所有关联数据（若被方向引用则报错）
        /// </summary>
        public static void DeleteStation(Context context, string stationName, bool force = false)
        {
            if (IsStationUsed(context, stationName))
            {
                var dirs = GetDirectionsUsingStation(context, stationName);
                throw new InvalidOperationException($"车站 '{stationName}' 被以下方向引用，无法删除: {string.Join(", ", dirs)}");
            }

            context.Stations.Remove(stationName);
            context.StationCity.Remove(stationName);
            context.StationOtherNames.Remove(stationName);
            context.Facilities.Remove(stationName);

            // 删除停站时间
            var dwellKeys = context.DwellTimes.Keys.Where(k => k.Station == stationName).ToList();
            foreach (var key in dwellKeys)
                context.DwellTimes.Remove(key);

            // 删除车站状态
            var statusKeys = context.StationStatus.Keys.Where(k => k.Station == stationName).ToList();
            foreach (var key in statusKeys)
                context.StationStatus.Remove(key);

            // 注意：不删除换乘规则？换乘规则中的车站名称会被保留，但后续若引用到不存在的车站，可在后处理中检查或忽略。
            // 建议：在删除时也清理 Transfers 和 VirtualTransfers 中涉及该车站的项
            context.Transfers.RemoveAll(t => t.Station == stationName);
            context.VirtualTransfers.RemoveAll(v => v.StationA == stationName || v.StationB == stationName);
        }

        /// <summary>
        /// 合并多条线路为一条新线路，处理方向 ID 冲突并同步更新所有关联数据。
        /// </summary>
        /// <param name="context">编译上下文</param>
        /// <param name="sourceLineIds">源线路 ID 列表（至少两条）</param>
        /// <param name="targetLineId">目标线路 ID</param>
        /// <param name="keepOriginals">是否保留源线路</param>
        public static void MergeLines(Context context, List<string> sourceLineIds, string targetLineId, bool keepOriginals)
        {
            if (sourceLineIds.Count < 2)
                throw new InvalidOperationException("至少需要两条线路进行合并");
            foreach (var id in sourceLineIds)
                if (!context.Lines.ContainsKey(id))
                    throw new InvalidOperationException($"线路 '{id}' 不存在");
            if (context.Lines.ContainsKey(targetLineId))
                throw new InvalidOperationException($"目标线路 '{targetLineId}' 已存在");

            // 1. 创建目标线路（复制第一条线路的属性）
            var firstLine = context.Lines[sourceLineIds[0]];
            var newLine = new LineInfo
            {
                Name = firstLine.Name,
                Color = firstLine.Color,
                City = firstLine.City,
                OpenYear = firstLine.OpenYear,
                Formation = firstLine.Formation,
                Maxspeed = firstLine.Maxspeed,
                PowerKw = firstLine.PowerKw,
                Brake = firstLine.Brake,
                Disabled = firstLine.Disabled,
                ImageBase64 = firstLine.ImageBase64
            };
            context.Lines[targetLineId] = newLine;

            // 2. 复制所有方向，处理 ID 冲突，并记录映射
            var dirIdMap = new Dictionary<string, string>();        // 旧ID -> 新ID
            var extKeyMap = new Dictionary<string, string>();      // 旧外部键 -> 新外部键

            foreach (var lineId in sourceLineIds)
            {
                var dirs = context.Directions.Where(kv => kv.Value.LineId == lineId).ToList();
                foreach (var (oldId, dirInfo) in dirs)
                {
                    // 生成新ID
                    string newId = oldId;
                    if (context.Directions.ContainsKey(newId) || dirIdMap.ContainsValue(newId))
                    {
                        int suffix = 1;
                        while (context.Directions.ContainsKey($"{oldId}_{suffix}") || dirIdMap.ContainsValue($"{oldId}_{suffix}"))
                            suffix++;
                        newId = $"{oldId}_{suffix}";
                    }
                    dirIdMap[oldId] = newId;

                    // 复制方向
                    var newDir = new DirectionInfo
                    {
                        LineId = targetLineId,
                        Stations = new List<string>(dirInfo.Stations),
                        Attrs = new HashSet<string>(dirInfo.Attrs),
                        DirectionType = dirInfo.DirectionType,
                        IsFuture = dirInfo.IsFuture,
                        ConnectStation = dirInfo.ConnectStation,
                        ViaDirections = new List<string>(dirInfo.ViaDirections),
                        SegmentTypes = dirInfo.SegmentTypes.Select(s => s.Clone()).ToList(),
                        ExpressStops = new List<string>(dirInfo.ExpressStops),
                        Routings = dirInfo.Routings.Select(r => r.Clone()).ToList()
                    };
                    context.Directions[newId] = newDir;

                    // 复制距离
                    if (context.Distances.TryGetValue(oldId, out var dist))
                        context.Distances[newId] = new List<double>(dist);

                    // 记录外部键映射
                    string oldExt = ConvertToExternalDirKey(context, oldId); // 此时 context 尚未改动
                    string newExt = $"{targetLineId}|{newDir.Stations.First()}|{newDir.Stations.Last()}";
                    extKeyMap[oldExt] = newExt;
                }
            }

            // 3. 更新 ViaDirections 中的旧方向ID
            foreach (var dir in context.Directions.Values)
            {
                for (int i = 0; i < dir.ViaDirections.Count; i++)
                {
                    if (dirIdMap.TryGetValue(dir.ViaDirections[i], out var newId))
                        dir.ViaDirections[i] = newId;
                }
            }

            // 4. 更新所有规则中的 DirectionKey（CrowdRules, SpeedLimits, Depots）
            foreach (var rule in context.CrowdRules)
                if (extKeyMap.TryGetValue(rule.DirectionKey, out var newKey))
                    rule.DirectionKey = newKey;
            foreach (var rule in context.SpeedLimits)
                if (extKeyMap.TryGetValue(rule.DirectionKey, out var newKey))
                    rule.DirectionKey = newKey;
            foreach (var depot in context.Depots)
                if (extKeyMap.TryGetValue(depot.DirectionKey, out var newKey))
                    depot.DirectionKey = newKey;

            // 5. 更新关联数据中的 LineId
            // LineLinks
            foreach (var lineId in sourceLineIds)
                if (context.LineLinks.TryGetValue(lineId, out var link))
                    context.LineLinks[targetLineId] = link;

            // SpecialLineIds
            foreach (var lineId in sourceLineIds)
                if (context.SpecialLineIds.Contains(lineId))
                    context.SpecialLineIds.Add(targetLineId);

            // PriceConfig.SpecialLines
            foreach (var special in context.PriceConfig.SpecialLines.Where(s => sourceLineIds.Contains(s.LineId)).ToList())
            {
                special.LineId = targetLineId;
                if (special.UseLines != null)
                {
                    for (int i = 0; i < special.UseLines.Count; i++)
                        if (sourceLineIds.Contains(special.UseLines[i]))
                            special.UseLines[i] = targetLineId;
                }
            }

            // HeadwayRules, OpenTimes, Depots, DwellTimes, StationStatus
            foreach (var rule in context.HeadwayRules.Where(r => sourceLineIds.Contains(r.LineId)))
                rule.LineId = targetLineId;
            foreach (var rule in context.OpenTimes.Where(r => sourceLineIds.Contains(r.LineId)))
                rule.LineId = targetLineId;
            foreach (var depot in context.Depots.Where(d => sourceLineIds.Contains(d.LineId)))
                depot.LineId = targetLineId;

            var dwellKeys = context.DwellTimes.Keys.Where(k => sourceLineIds.Contains(k.LineId)).ToList();
            foreach (var key in dwellKeys)
            {
                var val = context.DwellTimes[key];
                context.DwellTimes.Remove(key);
                context.DwellTimes[(key.Station, targetLineId)] = val;
            }

            var statusKeys = context.StationStatus.Keys.Where(k => sourceLineIds.Contains(k.LineId)).ToList();
            foreach (var key in statusKeys)
            {
                var val = context.StationStatus[key];
                context.StationStatus.Remove(key);
                context.StationStatus[(key.Station, targetLineId)] = val;
            }

            // 6. 删除源线路（如果不保留）
            if (!keepOriginals)
            {
                foreach (var lineId in sourceLineIds)
                {
                    DeleteLine(context, lineId); // DeleteLine 会删除所有方向，但我们的新方向 ID 已改，不会受影响
                }
            }
        }

        /// <summary>
        /// 拆分一条线路为多条新线路，按方向映射分配，处理 ID 冲突并同步更新关联数据。
        /// </summary>
        /// <param name="context">编译上下文</param>
        /// <param name="sourceLineId">源线路 ID</param>
        /// <param name="targetLineIds">目标线路 ID 列表</param>
        /// <param name="dirMapping">方向 ID 到目标线路的映射</param>
        /// <param name="remainingTarget">未映射方向的默认目标线路</param>
        /// <param name="keepOriginal">是否保留源线路</param>
        public static void SplitLine(Context context, string sourceLineId, List<string> targetLineIds,
            Dictionary<string, string> dirMapping, string remainingTarget, bool keepOriginal)
        {
            // 校验源线路
            if (!context.Lines.ContainsKey(sourceLineId))
                throw new InvalidOperationException($"线路 '{sourceLineId}' 不存在");
            var sourceDirs = context.Directions.Where(kv => kv.Value.LineId == sourceLineId).Select(kv => kv.Key).ToList();
            if (sourceDirs.Count == 0)
                throw new InvalidOperationException($"线路 '{sourceLineId}' 没有方向，无法拆分");

            // 校验目标线路
            foreach (var id in targetLineIds)
                if (context.Lines.ContainsKey(id))
                    throw new InvalidOperationException($"目标线路 '{id}' 已存在");

            // 收集所有方向的归属
            var assigned = new Dictionary<string, string>();
            var remaining = new List<string>();

            foreach (var dirId in sourceDirs)
            {
                if (dirMapping.TryGetValue(dirId, out var target))
                    assigned[dirId] = target;
                else
                    remaining.Add(dirId);
            }

            // 处理 remaining
            if (remaining.Count > 0 && string.IsNullOrEmpty(remainingTarget))
                throw new InvalidOperationException($"有 {remaining.Count} 个方向未分配，且未指定 REMAINING 目标");
            if (!string.IsNullOrEmpty(remainingTarget))
            {
                if (!targetLineIds.Contains(remainingTarget))
                    throw new InvalidOperationException($"REMAINING 目标 '{remainingTarget}' 不在目标线路列表中");
                foreach (var dirId in remaining)
                    assigned[dirId] = remainingTarget;
            }

            // 分组
            var groups = assigned.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList());
            // 确保所有目标线路至少有一个方向
            foreach (var targetId in targetLineIds)
                if (!groups.ContainsKey(targetId))
                    throw new InvalidOperationException($"目标线路 '{targetId}' 没有分配到任何方向");

            // 复制线路属性
            var sourceLine = context.Lines[sourceLineId];
            foreach (var targetId in targetLineIds)
            {
                var newLine = new LineInfo
                {
                    Name = sourceLine.Name + "_" + targetId,
                    Color = sourceLine.Color,
                    City = sourceLine.City,
                    OpenYear = sourceLine.OpenYear,
                    Formation = sourceLine.Formation,
                    Maxspeed = sourceLine.Maxspeed,
                    PowerKw = sourceLine.PowerKw,
                    Brake = sourceLine.Brake,
                    Disabled = sourceLine.Disabled,
                    ImageBase64 = sourceLine.ImageBase64
                };
                context.Lines[targetId] = newLine;
            }

            // 复制方向，修改 LineId，处理 ID 冲突
            var oldToNewDirs = new Dictionary<string, string>(); // 旧方向ID -> 新方向ID
            var extKeyMap = new Dictionary<string, string>();    // 旧外部键 -> 新外部键

            foreach (var (targetId, dirIds) in groups)
            {
                foreach (var oldId in dirIds)
                {
                    var dirInfo = context.Directions[oldId];
                    // 生成新ID
                    string newId = oldId;
                    if (context.Directions.ContainsKey(newId) || oldToNewDirs.ContainsValue(newId))
                    {
                        int suffix = 1;
                        while (context.Directions.ContainsKey($"{oldId}_{suffix}") || oldToNewDirs.ContainsValue($"{oldId}_{suffix}"))
                            suffix++;
                        newId = $"{oldId}_{suffix}";
                    }
                    oldToNewDirs[oldId] = newId;

                    // 复制方向
                    var newDir = new DirectionInfo
                    {
                        LineId = targetId,
                        Stations = new List<string>(dirInfo.Stations),
                        Attrs = new HashSet<string>(dirInfo.Attrs),
                        DirectionType = dirInfo.DirectionType,
                        IsFuture = dirInfo.IsFuture,
                        ConnectStation = dirInfo.ConnectStation,
                        ViaDirections = new List<string>(dirInfo.ViaDirections),
                        SegmentTypes = dirInfo.SegmentTypes.Select(s => s.Clone()).ToList(),
                        ExpressStops = new List<string>(dirInfo.ExpressStops),
                        Routings = dirInfo.Routings.Select(r => r.Clone()).ToList()
                    };
                    context.Directions[newId] = newDir;

                    // 复制距离
                    if (context.Distances.TryGetValue(oldId, out var dist))
                        context.Distances[newId] = new List<double>(dist);

                    // 记录外部键映射（后续更新规则）
                    string oldExt = Utils.ConvertToExternalDirKey(context, oldId); // 此时 context 未变
                    string newExt = $"{targetId}|{newDir.Stations.First()}|{newDir.Stations.Last()}";
                    extKeyMap[oldExt] = newExt;
                }
            }

            // 更新 ViaDirections
            foreach (var dir in context.Directions.Values)
            {
                for (int i = 0; i < dir.ViaDirections.Count; i++)
                {
                    if (oldToNewDirs.TryGetValue(dir.ViaDirections[i], out var newId))
                        dir.ViaDirections[i] = newId;
                }
            }

            // 更新规则中的 DirectionKey
            foreach (var rule in context.CrowdRules)
                if (extKeyMap.TryGetValue(rule.DirectionKey, out var newKey))
                    rule.DirectionKey = newKey;
            foreach (var rule in context.SpeedLimits)
                if (extKeyMap.TryGetValue(rule.DirectionKey, out var newKey))
                    rule.DirectionKey = newKey;
            foreach (var depot in context.Depots)
                if (extKeyMap.TryGetValue(depot.DirectionKey, out var newKey))
                    depot.DirectionKey = newKey;

            // 对所有目标线路复制相同的数据（但 Depots 可能包含方向，需要映射）
            foreach (var targetId in targetLineIds)
            {
                // 复制 LineLinks
                if (context.LineLinks.TryGetValue(sourceLineId, out var link))
                    context.LineLinks[targetId] = link;

                // 复制 SpecialLineIds
                if (context.SpecialLineIds.Contains(sourceLineId))
                    context.SpecialLineIds.Add(targetId);

                // 复制 SpecialLines 票价配置
                foreach (var special in context.PriceConfig.SpecialLines.Where(s => s.LineId == sourceLineId).ToList())
                {
                    var newSpecial = new SpecialLine
                    {
                        LineId = targetId,
                        All = special.All,
                        Surcharge = special.Surcharge?.Select(s => new SurchargeItem { NodeA = s.NodeA, NodeB = s.NodeB, Price = s.Price }).ToList(),
                        UseLines = special.UseLines?.Select(l => l == sourceLineId ? targetId : l).ToList(),
                        DefaultFare = special.DefaultFare,
                        Segments = special.Segments?.Select(s => new Segment { RangeStart = s.RangeStart, RangeEnd = s.RangeEnd, Fare = s.Fare }).ToList(),
                        LinearExtension = special.LinearExtension != null ? new LinearExtension { Threshold = special.LinearExtension.Threshold, BaseFare = special.LinearExtension.BaseFare, RatePerKm = special.LinearExtension.RatePerKm } : null
                    };
                    context.PriceConfig.SpecialLines.Add(newSpecial);
                }

                // 复制 HeadwayRules
                foreach (var rule in context.HeadwayRules.Where(r => r.LineId == sourceLineId))
                    context.HeadwayRules.Add(new HeadwayRule { LineId = targetId, StartTime = rule.StartTime, EndTime = rule.EndTime, IntervalSeconds = rule.IntervalSeconds, Days = new List<DayOfWeek>(rule.Days) });

                // 复制 OpenTimes
                foreach (var rule in context.OpenTimes.Where(r => r.LineId == sourceLineId))
                    context.OpenTimes.Add(new OpenTimeRule { LineId = targetId, StartTime = rule.StartTime, EndTime = rule.EndTime, Days = new List<DayOfWeek>(rule.Days) });

                // 复制 Depots（需映射方向键）
                foreach (var depot in context.Depots.Where(d => d.LineId == sourceLineId))
                {
                    string newDirKey = depot.DirectionKey;
                    if (extKeyMap.TryGetValue(depot.DirectionKey, out var mappedKey))
                        newDirKey = mappedKey;
                    context.Depots.Add(new DepotInfo { LineId = targetId, Station = depot.Station, DirectionKey = newDirKey, DepartureTime = depot.DepartureTime });
                }

                // 复制 DwellTimes
                var dwellKeys = context.DwellTimes.Keys.Where(k => k.LineId == sourceLineId).ToList();
                foreach (var key in dwellKeys)
                {
                    // 但停站时间应该只属于那些有该车站的方向，但为了简单，全部复制到每个目标线路，后续可调整
                    context.DwellTimes[(key.Station, targetId)] = context.DwellTimes[key];
                }

                // 复制 StationStatus
                var statusKeys = context.StationStatus.Keys.Where(k => k.LineId == sourceLineId).ToList();
                foreach (var key in statusKeys)
                    context.StationStatus[(key.Station, targetId)] = context.StationStatus[key];
            }

            // 删除源线路（如果不保留）
            if (!keepOriginal)
                DeleteLine(context, sourceLineId);
        }

        /// <summary>
        /// 解析线路模板（支持多级继承），返回合并后的模板属性。
        /// </summary>
        /// <param name="context">编译上下文</param>
        /// <param name="templateId">模板 ID</param>
        /// <param name="visited">已访问的模板 ID 集合（用于循环检测）</param>
        /// <returns>解析后的线路模板</returns>
        public static LineTemplate ResolveLineTemplate(Context context, string templateId, HashSet<string> visited = null)
        {
            visited ??= new HashSet<string>();
            if (visited.Contains(templateId))
                throw new InvalidOperationException($"检测到循环继承：线路模板 '{templateId}'");
            visited.Add(templateId);

            if (!context.LineTemplates.TryGetValue(templateId, out var template))
                throw new InvalidOperationException($"线路模板 '{templateId}' 不存在");

            var result = new LineTemplate();
            // 先继承父模板
            if (!string.IsNullOrEmpty(template.Inherits))
            {
                var parent = ResolveLineTemplate(context, template.Inherits, visited);
                CopyLineTemplateProperties(parent, result);
            }
            // 再覆盖当前模板属性
            CopyLineTemplateProperties(template, result);
            return result;
        }

        private static void CopyLineTemplateProperties(LineTemplate source, LineTemplate target)
        {
            if (source.Name != null) target.Name = source.Name;
            if (source.Color != null) target.Color = source.Color;
            if (source.City != null) target.City = source.City;
            if (source.OpenYear.HasValue) target.OpenYear = source.OpenYear;
            if (source.Formation != null) target.Formation = source.Formation;
            if (source.Maxspeed.HasValue) target.Maxspeed = source.Maxspeed;
            if (source.PowerKw.HasValue) target.PowerKw = source.PowerKw;
            if (source.Brake.HasValue) target.Brake = source.Brake;
            if (source.Disabled.HasValue) target.Disabled = source.Disabled;
            if (source.ImageBase64 != null) target.ImageBase64 = source.ImageBase64;
            if (source.Link != null) target.Link = source.Link;
            if (source.IsSpecial.HasValue) target.IsSpecial = source.IsSpecial;
        }

        /// <summary>
        /// 解析方向模板（支持多级继承），返回合并后的模板属性。
        /// </summary>
        /// <param name="context">编译上下文</param>
        /// <param name="templateId">模板 ID</param>
        /// <param name="visited">已访问的模板 ID 集合（用于循环检测）</param>
        /// <returns>解析后的方向模板</returns>
        public static DirectionTemplate ResolveDirectionTemplate(Context context, string templateId, HashSet<string> visited = null)
        {
            visited ??= new HashSet<string>();
            if (visited.Contains(templateId))
                throw new InvalidOperationException($"检测到循环继承：方向模板 '{templateId}'");
            visited.Add(templateId);

            if (!context.DirectionTemplates.TryGetValue(templateId, out var template))
                throw new InvalidOperationException($"方向模板 '{templateId}' 不存在");

            var result = new DirectionTemplate();
            if (!string.IsNullOrEmpty(template.Inherits))
            {
                var parent = ResolveDirectionTemplate(context, template.Inherits, visited);
                CopyDirectionTemplateProperties(parent, result);
            }
            CopyDirectionTemplateProperties(template, result);
            return result;
        }

        private static void CopyDirectionTemplateProperties(DirectionTemplate source, DirectionTemplate target)
        {
            if (source.IsFuture.HasValue) target.IsFuture = source.IsFuture;
            if (source.ConnectStation != null) target.ConnectStation = source.ConnectStation;
            if (source.DirectionType != null) target.DirectionType = source.DirectionType;
            foreach (var attr in source.Attrs)
                target.Attrs.Add(attr);
        }
    }
}
