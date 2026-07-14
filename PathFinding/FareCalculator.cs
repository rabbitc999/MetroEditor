using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.PathFinding
{
    // ========================================================================
    // 票价计算器（基于 ParsedData）
    // 职责：根据路径计算总票价，支持普通票价和特殊线路票价规则
    // 核心特性：分段计费、特殊线路叠加、区间附加费、线性延伸计费
    // ========================================================================
    public static class FareCalculator
    {
        #region 票价结果模型
        /// <summary>
        /// 票价计算结果
        /// </summary>
        public class FareResult
        {
            /// <summary>普通票价总额（元）</summary>
            /// <remarks>不含特殊线路附加费，适用于普通地铁线路</remarks>
            public double TotalMoney { get; set; }

            /// <summary>特殊线路附加费总额（元）</summary>
            /// <remarks>机场线、磁悬浮等特殊线路的额外费用</remarks>
            public double SpecialMoney { get; set; }

            /// <summary>实际总票价（元）</summary>
            /// <remarks>普通票价与特殊附加费之和</remarks>
            public double ActualTotal => TotalMoney + SpecialMoney;
        }
        #endregion

        #region 主计算接口
        /// <summary>
        /// 计算路径票价（不含特殊线路单独计费，但包含附加费）
        /// </summary>
        /// <param name="path">路径节点序列</param>
        /// <param name="graph">路网图</param>
        /// <returns>票价结果</returns>
        /// <remarks>
        /// 计算策略：
        /// 1. 将路径分解为连续的方向段（FareSegmentInfo）
        /// 2. 对每个方向段分别计费：
        ///    - 特殊线路：按特殊规则计费（统一票价/分段票价/附加费）
        ///    - 普通线路：累计里程，按总里程计费
        /// 3. 汇总所有费用
        /// 
        /// 注意：此方法已包含特殊线路的附加费计算，但不对特殊线路单独输出票价明细
        /// </remarks>
        public static FareResult CalcFareWithoutSpecial(List<string> path, MetroGraph graph)
        {
            if (path == null || path.Count < 2)
                return new FareResult { TotalMoney = 2.0, SpecialMoney = 0 };

            // 构建计费分段
            var segments = BuildSegments(path, graph);
            if (!segments.Any())
                return new FareResult { TotalMoney = 2.0, SpecialMoney = 0 };

            // 获取路网数据（通过反射访问私有字段，需谨慎）
            var data = graph.GetType()
                .GetField("_data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(graph) as ParsedData;
            if (data == null)
                return new FareResult { TotalMoney = 2.0, SpecialMoney = 0 };

            double totalDistance = 0;      // 普通线路总里程
            double specialTotal = 0;       // 特殊线路总票价
            double specialSurcharge = 0;   // 特殊线路附加费

            foreach (var segment in segments)
            {
                // 查找匹配的特殊线路规则
                var specialLine = FindSpecialLineForLine(segment.LineId, data);

                if (specialLine != null)
                {
                    // 情况1：统一票价（ALL）
                    if (specialLine.All.HasValue)
                    {
                        specialTotal += specialLine.All.Value;
                        continue;
                    }

                    // 情况2：完整票价配置（分段或起步价）
                    if (HasFullFareConfig(specialLine))
                    {
                        double fare = CalcFareFromSpecialConfig(segment.Distance, specialLine);
                        specialTotal += fare;
                        continue; // 该分段不参与普通票价计算
                    }

                    // 情况3：区间附加费（Surcharge）
                    if (specialLine.Surcharge != null && specialLine.Surcharge.Any())
                    {
                        specialSurcharge += CalcSurchargeForSegment(segment, specialLine, data);
                        continue;
                    }
                }

                // 无特殊规则，累计里程用于普通票价计算
                totalDistance += segment.Distance;
            }

            // 计算普通票价（基于总里程）
            double baseFare = CalcNormalFare(totalDistance, data);
            double total = baseFare + specialTotal + specialSurcharge;

            return new FareResult
            {
                TotalMoney = total,
                SpecialMoney = specialTotal + specialSurcharge
            };
        }

        /// <summary>
        /// 计算路径票价（包含特殊线路单独计费）
        /// </summary>
        /// <param name="path">路径节点序列</param>
        /// <param name="graph">路网图</param>
        /// <returns>票价结果</returns>
        /// <remarks>
        /// 当前实现与CalcFareWithoutSpecial完全相同
        /// 预留接口，用于未来支持更复杂的特殊线路计费逻辑
        /// </remarks>
        public static FareResult CalcFareWithSpecial(List<string> path, MetroGraph graph)
            => CalcFareWithoutSpecial(path, graph);
        #endregion

        #region 特殊线路匹配
        /// <summary>
        /// 查找匹配指定线路的SpecialLine规则
        /// </summary>
        /// <param name="lineId">线路ID</param>
        /// <param name="data">路网数据</param>
        /// <returns>匹配的SpecialLine规则，未找到返回null</returns>
        /// <remarks>
        /// 匹配优先级：
        /// 1. UseLines集合中包含该线路ID（新规范）
        /// 2. LineId字段直接匹配（旧规范，向后兼容）
        /// 
        /// 设计考虑：支持一条特殊规则应用于多条线路（如机场线网络）
        /// </remarks>
        private static SpecialLine FindSpecialLineForLine(string lineId, ParsedData data)
        {
            foreach (var specialLine in data.SpecialLines)
            {
                // 新规范：通过UseLines集合匹配
                if (specialLine.UseLines != null && specialLine.UseLines.Contains(lineId))
                    return specialLine;

                // 旧规范：直接匹配LineId（向后兼容）
                if (specialLine.LineId == lineId)
                    return specialLine;
            }
            return null;
        }

        /// <summary>
        /// 检查特殊线路是否配置了完整票价规则
        /// </summary>
        /// <param name="specialLine">特殊线路规则</param>
        /// <returns>是否包含完整票价配置</returns>
        /// <remarks>
        /// 完整票价配置包括：
        /// - 起步价（DefaultFare）
        /// - 分段票价（Segments非空）
        /// 
        /// 若包含完整配置，则该线路不参与普通里程累计计费
        /// </remarks>
        private static bool HasFullFareConfig(SpecialLine specialLine)
        {
            return specialLine.DefaultFare.HasValue ||
                  (specialLine.Segments != null && specialLine.Segments.Any());
        }
        #endregion

        #region 特殊票价计算
        /// <summary>
        /// 根据特殊线路配置计算分段票价
        /// </summary>
        /// <param name="distance">分段里程（千米）</param>
        /// <param name="special">特殊线路规则</param>
        /// <returns>计算出的票价（元）</returns>
        /// <remarks>
        /// 计算逻辑：
        /// 1. 按里程分段匹配（Segments）
        /// 2. 若里程超过最高分段，按线性延伸规则计算
        /// 3. 若无分段规则，使用起步价（DefaultFare）
        /// 
        /// 线性延伸公式：总票价 = 基础票价 + (超程里程 / 计费单位) × 单位加价
        /// </remarks>
        private static double CalcFareFromSpecialConfig(double distance, SpecialLine special)
        {
            if (distance <= 0) return 0;

            double startPrice = special.DefaultFare ?? 2.0;
            var segments = special.Segments?
                .OrderBy(s => s.RangeStart)
                .ToList() ?? new List<Segment>();

            // 匹配分段规则
            foreach (var segment in segments)
            {
                if (distance >= segment.RangeStart && distance < segment.RangeEnd)
                    return segment.Fare;
            }

            // 里程超过最高分段，应用线性延伸
            double threshold = segments.Any() ? segments.Last().RangeEnd : 0;
            double baseFare = segments.Any() ? segments.Last().Fare : startPrice;

            if (distance >= threshold &&
                special.LinearExtension != null &&
                special.LinearExtension.RatePerKm > 0)
            {
                double extraDistance = distance - threshold;
                double extraFare = extraDistance * special.LinearExtension.RatePerKm;
                return baseFare + extraFare;
            }

            // 无线性延伸规则，返回基础票价
            return startPrice;
        }

        /// <summary>
        /// 计算区间附加费
        /// </summary>
        /// <param name="segment">计费分段</param>
        /// <param name="special">特殊线路规则</param>
        /// <param name="data">路网数据</param>
        /// <returns>附加费金额（元）</returns>
        /// <remarks>
        /// 附加费逻辑：当分段同时经过NodeA和NodeB时，收取指定附加费
        /// 当前实现为简化版本：只要分段包含这两个站点即收费
        /// 
        /// 改进方向：应检查站点顺序（NodeA在前，NodeB在后）
        /// </remarks>
        private static double CalcSurchargeForSegment(FareSegmentInfo segment,
                                                     SpecialLine special,
                                                     ParsedData data)
        {
            if (special.Surcharge == null) return 0;

            foreach (var surcharge in special.Surcharge)
            {
                // 检查分段是否同时包含NodeA和NodeB
                if (segment.Stations.Contains(surcharge.NodeA) &&
                    segment.Stations.Contains(surcharge.NodeB))
                {
                    return surcharge.Price;
                }
            }
            return 0;
        }
        #endregion

        #region 普通票价计算
        /// <summary>
        /// 构建计费分段（将路径按方向键分组）
        /// </summary>
        /// <param name="path">路径节点序列</param>
        /// <param name="graph">路网图</param>
        /// <returns>计费分段列表</returns>
        /// <remarks>
        /// 分段规则：连续在同一方向键上的节点归为一个分段
        /// 每个分段包含：方向键、线路ID、站点序列、总里程
        /// </remarks>
        private static List<FareSegmentInfo> BuildSegments(List<string> path, MetroGraph graph)
        {
            var segments = new List<FareSegmentInfo>();
            string currentDirKey = null;
            var currentStations = new List<string>();

            foreach (var node in path)
            {
                var parts = GraphKeyUtils.ParseNode(node);
                string dirKey = parts[0];
                string station = parts[1];

                if (currentDirKey == null)
                {
                    // 初始化第一个分段
                    currentDirKey = dirKey;
                    currentStations.Add(station);
                }
                else if (currentDirKey == dirKey)
                {
                    // 同一方向，继续添加站点
                    currentStations.Add(station);
                }
                else
                {
                    // 方向改变，完成当前分段并开始新分段
                    if (currentStations.Any())
                        segments.Add(CreateSegment(currentDirKey, currentStations, graph));

                    currentDirKey = dirKey;
                    currentStations = new List<string> { station };
                }
            }

            // 添加最后一个分段
            if (currentStations.Any())
                segments.Add(CreateSegment(currentDirKey, currentStations, graph));

            return segments;
        }

        /// <summary>
        /// 创建单个计费分段
        /// </summary>
        /// <param name="dirKey">方向键</param>
        /// <param name="stations">站点序列</param>
        /// <param name="graph">路网图</param>
        /// <returns>计费分段信息</returns>
        private static FareSegmentInfo CreateSegment(string dirKey,
                                                     List<string> stations,
                                                     MetroGraph graph)
        {
            var segment = new FareSegmentInfo
            {
                DirKey = dirKey,
                Stations = stations,
                LineId = GraphKeyUtils.GetLineIdFromDirKey(dirKey)
            };

            // 计算分段总里程
            double distance = 0;
            for (int i = 0; i < stations.Count - 1; i++)
            {
                double segmentDistance = graph.GetDistanceInDirection(
                    dirKey, stations[i], stations[i + 1]);
                if (segmentDistance > 0)
                    distance += segmentDistance;
            }
            segment.Distance = distance;

            return segment;
        }

        /// <summary>
        /// 计算普通里程票价
        /// </summary>
        /// <param name="distance">总里程（千米）</param>
        /// <param name="data">路网数据</param>
        /// <returns>计算出的票价（元）</returns>
        /// <remarks>
        /// 计算逻辑（基于MoneyRules）：
        /// 1. 起步价（MoneyStartRule）
        /// 2. 分段票价（MoneySegmentRule）
        /// 3. 线性延伸（MoneyCalcRule）
        /// 
        /// 公式：总票价 = 匹配分段票价 或 基础票价 + 超程加价
        /// </remarks>
        private static double CalcNormalFare(double distance, ParsedData data)
        {
            if (distance <= 0) return 0;

            double startPrice = 2.0;
            double distanceUnit = 8.0;    // 计费单位（千米）
            double pricePerUnit = 1.0;    // 单位加价（元）
            var segments = new List<(double Start, double End, double Price)>();

            // 解析票价规则
            foreach (var rule in data.MoneyRules)
            {
                switch (rule)
                {
                    case MoneyStartRule startRule:
                        startPrice = startRule.Price;
                        break;

                    case MoneySegmentRule segmentRule:
                        segments.Add((segmentRule.Start, segmentRule.End, segmentRule.Price));
                        break;

                    case MoneyCalcRule calcRule:
                        distanceUnit = calcRule.DistSegment;
                        pricePerUnit = calcRule.AddPrice;
                        break;
                }
            }

            // 按里程排序分段规则
            segments = segments.OrderBy(s => s.Start).ToList();

            // 匹配分段票价
            foreach (var segment in segments)
            {
                if (distance >= segment.Start && distance < segment.End)
                    return segment.Price;
            }

            // 里程超过最高分段，应用线性延伸
            double threshold = segments.Any() ? segments.Last().End : 0;
            double baseFare = segments.Any() ? segments.Last().Price : startPrice;

            if (distance >= threshold && distanceUnit > 0)
            {
                double extraDistance = distance - threshold;
                double extraUnits = Math.Ceiling(extraDistance / distanceUnit);
                return baseFare + extraUnits * pricePerUnit;
            }

            // 无线性延伸规则，返回起步价
            return startPrice;
        }
        #endregion

        #region 内部数据模型
        /// <summary>
        /// 计费分段信息（内部使用）
        /// </summary>
        private class FareSegmentInfo
        {
            /// <summary>方向键</summary>
            public string DirKey { get; set; }

            /// <summary>线路ID</summary>
            public string LineId { get; set; }

            /// <summary>站点序列</summary>
            public List<string> Stations { get; set; } = new List<string>();

            /// <summary>分段总里程（千米）</summary>
            public double Distance { get; set; }
        }
        #endregion
    }
}