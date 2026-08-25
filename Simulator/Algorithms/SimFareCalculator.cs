using System;
using System.Collections.Generic;
using MetroEditor.Simulator.Data;
using MetroEditor.Simulator.Models;
using MetroEditor.Simulator.Utils;

namespace MetroEditor.Simulator.Algorithms
{
    /// <summary>
    /// 票价计算工具类。
    /// <para>支持普通里程计费（起步价 + 分段票价 + 线性扩展）和特殊线路票价
    /// （统一票价、区间附加费、独立分段计价），可处理 <c>UseLines</c> 线路组。</para>
    /// <para>计算流程：将路径按方向分段 → 区分普通段和特殊段 → 分别计算票价 → 汇总。</para>
    /// <para>该类从 Android 版 FareCalculator 移植。</para>
    /// </summary>
    public static class SimFareCalculator
    {
        /// <summary>
        /// 计算路径总票价（含特殊线路票价）。
        /// </summary>
        /// <param name="cache">数据缓存</param>
        /// <param name="path">路径节点列表</param>
        /// <param name="metroGraph">地铁图实例</param>
        /// <returns>(总票价, 特殊线路票价部分)</returns>
        public static (double Total, double Special) CalculateFareWithSpecial(
            SimDataCache cache, List<string> path, SimMetroGraph metroGraph)
        {
            if (path == null || path.Count < 2)
                return (cache.DefaultFare ?? 0.0, 0.0);

            var details = CalculateFareDetails(cache, path, metroGraph, false);
            double total = 0.0, special = 0.0;
            foreach (var d in details)
            {
                total += d.TotalFare;
                if (d.IsSpecial)
                    special += d.TotalFare;
                else
                    special += d.Surcharge;
            }
            return (total, special);
        }

        /// <summary>
        /// 计算路径总票价（排除特殊线路，仅计算普通里程票价）。
        /// </summary>
        /// <param name="cache">数据缓存</param>
        /// <param name="path">路径节点列表</param>
        /// <param name="metroGraph">地铁图实例</param>
        /// <returns>(总票价, 0)</returns>
        public static (double Total, double Special) CalculateFareWithoutSpecial(
            SimDataCache cache, List<string> path, SimMetroGraph metroGraph)
        {
            if (path == null || path.Count < 2)
                return (cache.DefaultFare ?? 0.0, 0.0);

            var details = CalculateFareDetails(cache, path, metroGraph, true);
            double total = 0.0;
            foreach (var d in details)
                total += d.TotalFare;
            return (total, 0.0);
        }

        /// <summary>
        /// 计算路径的详细票价分段信息，区分普通段和特殊段。
        /// </summary>
        /// <param name="cache">数据缓存</param>
        /// <param name="path">路径节点列表</param>
        /// <param name="metroGraph">地铁图实例</param>
        /// <param name="excludeSpecial">是否排除特殊线路</param>
        /// <returns>每段的票价明细列表</returns>
        public static List<SimFareSegmentDetail> CalculateFareDetails(
            SimDataCache cache, List<string> path, SimMetroGraph metroGraph, bool excludeSpecial)
        {
            var details = new List<SimFareSegmentDetail>();
            if (path == null || path.Count < 2)
                return details;

            var segments = BuildSegments(path, metroGraph);
            if (segments.Count == 0)
                return details;

            var lineNameMap = cache.LineNameMap;

            double normalDist = 0.0;
            double normalSurcharge = 0.0;
            string? normalLineId = null;
            string? normalStart = null;
            string? normalEnd = null;

            foreach (var seg in segments)
            {
                if (excludeSpecial)
                {
                    normalDist += seg.Distance;
                    if (normalLineId == null)
                    {
                        normalLineId = seg.LineId;
                        normalStart = seg.Stations[0];
                        normalEnd = seg.Stations[^1];
                    }
                    else
                    {
                        normalEnd = seg.Stations[^1];
                    }
                    continue;
                }

                var rule = FindSpecialRule(cache, seg.LineId);
                if (rule != null)
                {
                    // 完整票价配置 (defaultFare / segments)
                    if (rule.DefaultFare.HasValue || (rule.Segments != null && rule.Segments.Count > 0))
                    {
                        if (normalDist > 0)
                        {
                            details.AddRange(FinishNormalSegment(cache, normalLineId!, normalStart!, normalEnd!,
                                normalDist, normalSurcharge, lineNameMap));
                            normalDist = 0;
                            normalSurcharge = 0;
                            normalLineId = null;
                            normalStart = null;
                            normalEnd = null;
                        }
                        double fare = CalcFareFromSpecialConfig(seg.Distance, rule);
                        details.Add(CreateSpecialDetail(seg, fare, 0, lineNameMap));
                        continue;
                    }

                    // 统一票价 all
                    if (rule.All.HasValue)
                    {
                        if (normalDist > 0)
                        {
                            details.AddRange(FinishNormalSegment(cache, normalLineId!, normalStart!, normalEnd!,
                                normalDist, normalSurcharge, lineNameMap));
                            normalDist = 0;
                            normalSurcharge = 0;
                            normalLineId = null;
                            normalStart = null;
                            normalEnd = null;
                        }
                        details.Add(CreateSpecialDetail(seg, rule.All.Value, 0, lineNameMap));
                        continue;
                    }

                    // 区间附加费 surcharge
                    if (rule.Surcharge != null && rule.Surcharge.Count > 0)
                    {
                        normalSurcharge += CalcSurchargeForSegment(seg, rule);
                        if (normalLineId == null)
                        {
                            normalLineId = seg.LineId;
                            normalStart = seg.Stations[0];
                            normalEnd = seg.Stations[^1];
                        }
                        else
                        {
                            normalEnd = seg.Stations[^1];
                        }
                        continue;
                    }
                }

                // 普通段
                normalDist += seg.Distance;
                if (normalLineId == null)
                {
                    normalLineId = seg.LineId;
                    normalStart = seg.Stations[0];
                    normalEnd = seg.Stations[^1];
                }
                else
                {
                    normalEnd = seg.Stations[^1];
                }
            }

            if (normalDist > 0)
            {
                details.AddRange(FinishNormalSegment(cache, normalLineId!, normalStart!, normalEnd!,
                    normalDist, normalSurcharge, lineNameMap));
            }

            return details;
        }

        // ---------- 辅助：普通段结算 ----------
        private static List<SimFareSegmentDetail> FinishNormalSegment(
            SimDataCache cache, string lineId, string start, string end, double dist, 
            double surcharge, IReadOnlyDictionary<string, string> lineNameMap)
        {
            var list = new List<SimFareSegmentDetail>();
            if (dist <= 0 || string.IsNullOrEmpty(lineId)) return list;

            double baseFare = CalcBaseFare(cache, dist);
            double total = baseFare + surcharge;
            string displayName = lineNameMap.TryGetValue(lineId, out string? name) ? name : lineId;

            list.Add(new SimFareSegmentDetail
            {
                LineId = lineId,
                LineDisplayName = displayName,
                StartStation = start,
                EndStation = end,
                Stations = null,
                Distance = dist,
                IsSpecial = false,
                BaseFare = baseFare,
                Surcharge = surcharge,
                TotalFare = total
            });
            return list;
        }

        // ---------- 辅助：创建特殊段明细 ----------
        private static SimFareSegmentDetail CreateSpecialDetail(
            SimFareSegmentInfo seg, double fare, double surcharge,
            IReadOnlyDictionary<string, string> lineNameMap)
        {
            string displayName = lineNameMap.TryGetValue(seg.LineId, out string? name) ? name : seg.LineId;
            return new SimFareSegmentDetail
            {
                LineId = seg.LineId,
                LineDisplayName = displayName,
                StartStation = seg.Stations[0],
                EndStation = seg.Stations[^1],
                Stations = seg.Stations,
                Distance = seg.Distance,
                IsSpecial = true,
                BaseFare = fare,
                Surcharge = surcharge,
                TotalFare = fare + surcharge
            };
        }

        // ---------- 计算区间附加费 ----------
        private static double CalcSurchargeForSegment(SimFareSegmentInfo seg, SimSpecialLineRule rule)
        {
            if (rule.Surcharge == null) return 0.0;
            string start = seg.Stations[0];
            string end = seg.Stations[^1];
            foreach (var sur in rule.Surcharge)
            {
                if (sur.Node_a == start && sur.Node_b == end)
                    return sur.Price;
            }
            return 0.0;
        }

        /// <summary>
        /// 将路径节点列表按方向分段，每段包含方向键、站点列表、线路 ID 和累计距离。
        /// </summary>
        /// <param name="path">路径节点列表</param>
        /// <param name="metroGraph">地铁图实例</param>
        /// <returns>票价分段信息列表</returns>
        public static List<SimFareSegmentInfo> BuildSegments(List<string> path, SimMetroGraph metroGraph)
        {
            var segments = new List<SimFareSegmentInfo>();
            string? curDirKey = null;
            var curStations = new List<string>();

            foreach (string node in path)
            {
                string[] parts = SimGraphKeyUtils.ParseNode(node);
                string dirKey = parts[0];
                string station = parts[1];

                if (curDirKey == null)
                {
                    curDirKey = dirKey;
                    curStations.Add(station);
                }
                else if (curDirKey == dirKey)
                {
                    curStations.Add(station);
                }
                else
                {
                    if (curStations.Count > 0)
                        segments.Add(CreateSegment(curDirKey, curStations, metroGraph));
                    curDirKey = dirKey;
                    curStations = new List<string> { station };
                }
            }
            if (curStations.Count > 0)
                segments.Add(CreateSegment(curDirKey!, curStations, metroGraph));

            return segments;
        }

        private static SimFareSegmentInfo CreateSegment(string dirKey, List<string> stations, SimMetroGraph metroGraph)
        {
            var seg = new SimFareSegmentInfo
            {
                DirKey = dirKey,
                Stations = stations,
                LineId = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey)!
            };

            double dist = 0.0;
            for (int i = 0; i < stations.Count - 1; i++)
            {
                double d = metroGraph.GetDistanceInDirection(dirKey, stations[i], stations[i + 1]);
                if (d > 0) dist += d;
            }
            seg.Distance = dist;
            return seg;
        }

        /// <summary>
        /// 计算基础里程票价：先查阶梯区间，再用线性扩展公式计算超出部分。
        /// </summary>
        /// <param name="cache">数据缓存</param>
        /// <param name="distance">行驶距离（公里）</param>
        /// <returns>基础票价</returns>
        public static double CalcBaseFare(SimDataCache cache, double distance)
        {
            if (distance <= 0) return 0.0;

            // 阶梯区间
            var segments = cache.FareSegments;
            if (segments != null)
            {
                foreach (var fs in segments)
                {
                    if (distance >= fs.RangeStart && distance < fs.RangeEnd)
                        return fs.Fare;
                }
            }

            // 线性扩展
            var le = cache.LinearExtension;
            if (le != null && distance >= le.Threshold)
                return le.BaseFare + (distance - le.Threshold) * le.RatePerKm;

            return cache.DefaultFare ?? 0.0;
        }

        // ---------- 特殊配置票价 ----------
        private static double CalcFareFromSpecialConfig(double distance, SimSpecialLineRule rule)
        {
            if (distance <= 0) return 0.0;
            double startPrice = rule.DefaultFare ?? 2.0;
            var segments = rule.Segments;
            if (segments != null && segments.Count > 0)
            {
                double maxFare = 0.0;
                foreach (var fs in segments)
                {
                    if (distance >= fs.RangeStart && distance < fs.RangeEnd)
                        return fs.Fare;
                    if (fs.Fare > maxFare)
                        maxFare = fs.Fare;
                }
                return maxFare;
            }
            return startPrice;
        }

        // ---------- 查找特殊规则 ----------
        private static SimSpecialLineRule? FindSpecialRule(SimDataCache cache, string lineId)
        {
            var rules = cache.SpecialLineRules;
            if (rules == null) return null;

            // 优先匹配 useLines
            foreach (var r in rules)
            {
                if (r.UseLines != null && r.UseLines.Contains(lineId))
                    return r;
            }
            // 回退到 lineId 精确匹配
            foreach (var r in rules)
            {
                if (r.LineId == lineId)
                    return r;
            }
            return null;
        }
    }
}