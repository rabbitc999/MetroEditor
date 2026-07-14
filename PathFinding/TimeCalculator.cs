using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.PathFinding
{
    // ========================================================================
    // 路径时间计算器（基于 ParsedData 业务模型）
    // 职责：协调各类时间组件，计算完整出行链的耗时
    // 核心公式：总时间 = 列车运行时间 + 换乘等待时间 + 停站时间 + 进站时间
    // ========================================================================
    public static class TimeCalculator
    {
        #region 默认物理参数
        /// <summary>默认牵引功率（kW），用于未配置功率的线路</summary>
        private const double DefaultPowerKw = 2000.0;

        /// <summary>默认制动减速度（m/s²），用于未配置制动的线路</summary>
        private const double DefaultBrakeDecel = 0.85;
        #endregion

        #region 总时间计算（对外主入口）
        /// <summary>
        /// 计算完整出行路径的总耗时
        /// </summary>
        /// <param name="path">路径节点序列（格式："方向键|车站名"）</param>
        /// <param name="graph">路网图结构（用于查询距离）</param>
        /// <param name="data">解析后的路网业务数据</param>
        /// <param name="travelTime">出行起始时间（用于匹配时刻表和拥挤度）</param>
        /// <returns>总耗时（秒）</returns>
        /// <remarks>
        /// 时间构成：
        /// 1. 列车运行时间：区间运行时间之和
        /// 2. 换乘等待时间：包含发车间隔、拥挤度影响
        /// 3. 停站时间：各站停靠时间之和
        /// 4. 进站时间：起始站进站耗时（默认计算两次，起点和终点各一次）
        /// 
        /// 设计约定：travelTime 默认为早8点，模拟早高峰出行场景
        /// </remarks>
        public static double ComputeTotalTime(List<string> path, MetroGraph graph, ParsedData data, DateTime? travelTime = null)
        {
            if (path == null || path.Count < 2) return 0;

            // 默认出行时间：今日08:00（典型通勤时段）
            DateTime actualTravelTime = travelTime ?? DateTime.Today.AddHours(8);

            // 分项计算
            double trainTime = ComputeTrainTime(path, graph, data);           // 区间运行
            double transferTime = ComputeTotalInnerTransferTime(path, data, actualTravelTime); // 换乘等待
            double dwellTime = ComputeDwellTime(path, data);                 // 停站
            double entryTime = data.DefaultEntryTime;                       // 进站

            // 进站时间通常计算两次（起点进站 + 终点出站）
            return trainTime + transferTime + dwellTime + entryTime * 2;
        }
        #endregion

        #region 发车间隔计算
        /// <summary>
        /// 获取指定线路在指定时刻的发车间隔
        /// </summary>
        /// <param name="lineId">线路ID</param>
        /// <param name="time">查询时刻</param>
        /// <param name="data">路网数据</param>
        /// <returns>发车间隔（秒）</returns>
        /// <remarks>
        /// 匹配逻辑：
        /// 1. 按线路ID筛选
        /// 2. 按星期筛选（周一到周日）
        /// 3. 按时间范围筛选（如07:00-09:00）
        /// 4. 若有多个规则匹配，取间隔最大的（保守估计）
        /// 
        /// 默认返回值：120秒（2分钟），适用于无配置或夜间时段
        /// </remarks>
        public static int GetHeadway(string lineId, DateTime time, ParsedData data)
        {
            var rules = data.HeadwayRules
                .Where(r => r.LineId == lineId)
                .OrderByDescending(r => r.IntervalSeconds) // 优先匹配更精确的时间段
                .ToList();

            var dayOfWeek = time.DayOfWeek;
            var timeOfDay = time.TimeOfDay;

            foreach (var rule in rules)
            {
                // 检查星期匹配
                if (rule.Days != null && rule.Days.Any() && !rule.Days.Contains(dayOfWeek))
                    continue;

                // 检查时间范围匹配
                if (timeOfDay >= rule.StartTime && timeOfDay < rule.EndTime)
                    return rule.IntervalSeconds;
            }

            // 默认返回 120 秒（2分钟）
            return 120;
        }
        #endregion

        #region 列车运行时间计算
        /// <summary>
        /// 计算路径中所有区间的运行时间总和
        /// </summary>
        /// <param name="path">路径节点序列</param>
        /// <param name="graph">路网图</param>
        /// <param name="data">路网数据</param>
        /// <returns>总运行时间（秒）</returns>
        /// <remarks>
        /// 处理逻辑：
        /// 1. 遍历路径节点，识别连续在同一方向上的区间
        /// 2. 从图中查询区间距离
        /// 3. 调用物理模型计算每个区间的运行时间
        /// 4. 累加得到总运行时间
        /// 
        /// 注意：方向切换（换乘）时不计算运行时间，由换乘模块处理
        /// </remarks>
        private static double ComputeTrainTime(List<string> path, MetroGraph graph, ParsedData data)
        {
            double time = 0;
            string currentDirection = null;
            string previousStation = null;

            foreach (var node in path)
            {
                var parts = GraphKeyUtils.ParseNode(node);
                string directionKey = parts[0];
                string station = parts[1];

                // 初始化第一个节点
                if (currentDirection == null)
                {
                    currentDirection = directionKey;
                    previousStation = station;
                    continue;
                }

                // 方向切换（换乘），重置状态
                if (directionKey != currentDirection)
                {
                    currentDirection = directionKey;
                    previousStation = station;
                    continue;
                }

                // 计算同方向内的区间运行时间
                double distance = graph.GetDistanceInDirection(directionKey, previousStation, station);
                if (distance > 0)
                {
                    string lineId = GraphKeyUtils.GetLineIdFromDirKey(directionKey);
                    int maxSpeed = data.Lines.TryGetValue(lineId, out var lineInfo)
                        ? (int)(lineInfo.Maxspeed ?? 80)
                        : 80;

                    time += ComputeSegmentTime(distance, maxSpeed, lineId, directionKey,
                                             previousStation, station, data);
                }
                previousStation = station;
            }
            return time;
        }

        /// <summary>
        /// 计算单个区间的运行时间（基于物理功率模型）
        /// </summary>
        /// <param name="distance">区间距离（千米）</param>
        /// <param name="defaultMaxSpeed">默认最高速度（km/h，备用）</param>
        /// <param name="lineId">线路ID</param>
        /// <param name="directionKey">方向键</param>
        /// <param name="fromStation">起点站</param>
        /// <param name="toStation">终点站</param>
        /// <param name="data">路网数据</param>
        /// <returns>区间运行时间（秒）</returns>
        /// <remarks>
        /// 计算流程：
        /// 1. 查询区间限速（SpeedLimits），覆盖线路默认限速
        /// 2. 获取线路配置：编组、功率、制动性能
        /// 3. 调用 TrainTimeCalculator 的物理模型计算
        /// 
        /// 限速优先级：区间限速 > 线路最高速度
        /// 配置回退：未配置参数使用默认值
        /// </remarks>
        public static double ComputeSegmentTime(
            double distance,              // 单位：千米
            int defaultMaxSpeed,          // 单位：km/h（备用）
            string lineId,
            string directionKey,
            string fromStation,
            string toStation,
            ParsedData data)
        {
            if (distance <= 0) return 0;

            // ---------- 1. 确定区间限速 ----------
            int maxSpeed = defaultMaxSpeed;
            foreach (var limit in data.SpeedLimits)
            {
                // 不区分方向匹配（往返限速相同）
                if (limit.DirectionKey == directionKey &&
                   ((limit.FromStation == fromStation && limit.ToStation == toStation) ||
                    (limit.FromStation == toStation && limit.ToStation == fromStation)))
                {
                    maxSpeed = limit.SpeedKmh;
                    break;
                }
            }

            // ---------- 2. 获取线路配置 ----------
            data.Lines.TryGetValue(lineId, out var lineInfo);

            // 编组（默认6B）
            string consist = lineInfo?.Formation ?? "6B";

            // 牵引功率（默认2000kW）
            double powerKw = lineInfo?.PowerKw ?? DefaultPowerKw;

            // 制动减速度（默认0.85m/s²）
            double brakeDecel = lineInfo?.Brake ?? DefaultBrakeDecel;

            // ---------- 3. 调用物理模型计算 ----------
            return TrainTimeCalculator.ComputeSegmentTime(
                distance,          // 单位：千米
                consist,
                maxSpeed,          // 单位：km/h
                powerKw,
                brakeDecel
            );
        }
        #endregion

        #region 换乘等待时间计算
        /// <summary>
        /// 计算路径中所有换乘站的总等待时间
        /// </summary>
        /// <param name="path">路径节点序列</param>
        /// <param name="data">路网数据</param>
        /// <param name="travelTime">出行时间</param>
        /// <returns>总换乘等待时间（秒）</returns>
        /// <remarks>
        /// 换乘等待时间包含：
        /// 1. 平均候车时间（发车间隔的一半）
        /// 2. 上车时间（开关门、乘客上下车）
        /// 3. 拥挤度惩罚（高峰时段额外等待）
        /// 4. 出站换乘额外时间（如虚拟换乘）
        /// </remarks>
        private static double ComputeTotalInnerTransferTime(List<string> path, ParsedData data, DateTime? travelTime = null)
        {
            DateTime actualTravelTime = travelTime ?? DateTime.Today.AddHours(8);
            double total = 0;
            string previousDirection = null;

            for (int i = 0; i < path.Count; i++)
            {
                var parts = GraphKeyUtils.ParseNode(path[i]);
                string directionKey = parts[0];
                string station = parts[1];

                if (i == 0)
                {
                    previousDirection = directionKey;
                    continue;
                }

                // 检测到方向切换（换乘发生）
                if (directionKey != previousDirection)
                {
                    // 换乘站在前一个节点
                    string transferStation = GraphKeyUtils.ParseNode(path[i - 1])[1];
                    double wait = ComputeTransferWaitTime(transferStation, previousDirection,
                                                         directionKey, actualTravelTime, data);
                    total += wait;
                    previousDirection = directionKey;
                }
            }

            return total;
        }

        /// <summary>
        /// 获取指定方向的拥挤度系数
        /// </summary>
        /// <param name="targetDirectionKey">目标方向键</param>
        /// <param name="time">查询时刻</param>
        /// <param name="data">路网数据</param>
        /// <returns>拥挤度系数（≥1.0）</returns>
        /// <remarks>
        /// 拥挤度影响：
        /// - 1.0：无拥挤，正常候车
        /// - >1.0：拥挤，候车时间延长（如1.5表示延长50%）
        /// 
        /// 匹配逻辑类似发车间隔：按方向、星期、时间范围匹配
        /// </remarks>
        public static double GetCrowdFactor(string targetDirectionKey, DateTime time, ParsedData data)
        {
            var rules = data.CrowdRules
                .Where(r => r.DirectionKey == targetDirectionKey)
                .OrderByDescending(r => r.Factor)
                .ToList();

            var dayOfWeek = time.DayOfWeek;
            var timeOfDay = time.TimeOfDay;

            foreach (var rule in rules)
            {
                if (rule.Days != null && rule.Days.Any() && !rule.Days.Contains(dayOfWeek))
                    continue;

                if (timeOfDay >= rule.StartTime && timeOfDay < rule.EndTime)
                    return rule.Factor;
            }

            // 默认无拥挤
            return 1.0;
        }

        /// <summary>
        /// 计算单次换乘的等待时间（核心公式）
        /// </summary>
        /// <param name="transferStation">换乘站名</param>
        /// <param name="fromDirKey">下车方向键</param>
        /// <param name="toDirKey">换乘后方向键</param>
        /// <param name="time">出行时间</param>
        /// <param name="data">路网数据</param>
        /// <returns>换乘等待时间（秒）</returns>
        /// <remarks>
        /// 计算公式：E[W] = T/2 + t + (C-1)*T
        /// 其中：
        /// - T：发车间隔（秒）
        /// - t：上车时间（秒，开关门及乘客上下车）
        /// - C：拥挤度系数
        /// 
        /// 物理意义：
        /// 1. T/2：平均候车时间（均匀分布假设）
        /// 2. t：基本作业时间
        /// 3. (C-1)*T：拥挤度惩罚（高峰时段额外等待）
        /// 
        /// 特殊场景：
        /// - 出站换乘（out_station）：额外增加出站步行/安检时间
        /// </remarks>
        public static double ComputeTransferWaitTime(
            string transferStation,
            string fromDirKey,
            string toDirKey,
            DateTime time,
            ParsedData data)
        {
            // 1. 获取目标线路ID
            string targetLineId = GraphKeyUtils.GetLineIdFromDirKey(toDirKey);

            // 2. 获取发车间隔 T
            double headway = GetHeadway(targetLineId, time, data);

            // 3. 获取拥挤度系数 C
            double crowdFactor = GetCrowdFactor(toDirKey, time, data);

            // 4. 获取上车时间 t
            double boardTime = data.DefaultBoardTime;

            // 5. 应用核心公式：E[W] = T/2 + t + (C-1)*T
            double waitTime = headway / 2.0 + boardTime + (crowdFactor - 1) * headway;

            // 6. 出站换乘额外处理
            var status = StationUtils.GetTransferStatus(transferStation, fromDirKey, toDirKey, data);
            if (status == "out_station")
            {
                // 增加出站步行、安检等时间
                waitTime += data.DefaultOutStationTime;
            }

            return waitTime;
        }
        #endregion

        #region 停站时间计算
        /// <summary>
        /// 计算路径中所有车站的停站时间总和
        /// </summary>
        /// <param name="path">路径节点序列</param>
        /// <param name="data">路网数据</param>
        /// <returns>总停站时间（秒）</returns>
        /// <remarks>
        /// 停站时间策略：
        /// 1. 优先使用车站-线路特定的停站时间配置
        /// 2. 未配置时使用线路默认停站时间
        /// 3. 终点站通常不计入（由调用方控制）
        /// </remarks>
        private static double ComputeDwellTime(List<string> path, ParsedData data)
        {
            if (path == null || path.Count < 2) return 0;
            double total = 0;

            for (int i = 0; i < path.Count - 1; i++)  // 排除终点站
            {
                var parts = GraphKeyUtils.ParseNode(path[i]);
                string directionKey = parts[0];
                string station = parts[1];
                string lineId = GraphKeyUtils.GetLineIdFromDirKey(directionKey);

                // 查询车站-线路特定的停站时间
                double? customDwell = null;
                if (data.DwellTimes.TryGetValue((station, lineId), out double dwellSeconds))
                    customDwell = dwellSeconds;

                // 使用自定义时间或默认值
                total += customDwell ?? data.DefaultDwellTime;
            }
            return total;
        }
        #endregion
    }
}