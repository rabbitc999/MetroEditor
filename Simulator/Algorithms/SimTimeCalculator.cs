using System;
using System.Collections.Generic;
using MetroEditor.Simulator.Data;
using MetroEditor.Simulator.Models;
using MetroEditor.Simulator.Utils;

namespace MetroEditor.Simulator.Algorithms
{
    /// <summary>
    /// 时间计算工具类（从 Android TimeCalculator 转换）
    /// </summary>
    public static class SimTimeCalculator
    {
        // ---------- 公共API：总时间计算 ----------
        public static double ComputeTotalTime(SimDataCache cache, List<string> path, SimMetroGraph metroGraph,
                                              SimSearchWeight weight, long travelTimeMillis)
        {
            double entryTime = cache.AvgEntryTime;
            double trainTime = ComputeTrainTime(cache, path, metroGraph);
            double transferTime = ComputeTotalTransferTime(cache, path, travelTimeMillis);
            double stopTime = ComputeStopTime(cache, path, metroGraph);

            double startWaitTime = 0.0;
            if (path.Count > 0)
            {
                string firstNode = path[0];
                string dirKey = SimGraphKeyUtils.ParseNode(firstNode)[0];
                startWaitTime = ComputeTransferWaitTime(cache, dirKey, travelTimeMillis);
            }

            return trainTime + transferTime + stopTime + entryTime * 2 + startWaitTime;
        }

        // ---------- 行驶时间 ----------
        private static double ComputeTrainTime(SimDataCache cache, List<string> path, SimMetroGraph metroGraph)
        {
            double trainTime = 0.0;
            string? currentDirKey = null;
            string? prevStation = null;

            for (int i = 0; i < path.Count; i++)
            {
                string[] parts = SimGraphKeyUtils.ParseNode(path[i]);
                string dirKey = parts[0];
                string station = parts[1];

                if (i == 0)
                {
                    currentDirKey = dirKey;
                    prevStation = station;
                    continue;
                }

                if (dirKey != currentDirKey)
                {
                    currentDirKey = dirKey;
                    prevStation = station;
                    continue;
                }

                double distance = metroGraph.GetDistanceInDirection(dirKey, prevStation!, station);
                if (distance > 0)
                {
                    string lineId = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey)!;
                    int maxSpeed = GetLineMaxSpeed(cache, lineId);

                    // 应用限速
                    var speedLimits = cache.SpeedLimits;
                    if (speedLimits != null)
                    {
                        foreach (var sl in speedLimits)
                        {
                            if (sl.DirectionKey == dirKey &&
                                sl.FromStation == prevStation &&
                                sl.ToStation == station)
                            {
                                maxSpeed = sl.SpeedKmh;
                                break;
                            }
                        }
                    }

                    // 获取编组、功率、制动
                    string formation = cache.LineFormationMap.TryGetValue(lineId, out string? f) ? f : "6B";
                    double powerKw = cache.LinePowerKwMap.TryGetValue(lineId, out double p) ? p : 2000.0;
                    double brake = cache.LineBrakeMap.TryGetValue(lineId, out double b) ? b : 0.85;

                    trainTime += SimTrainTimeUtils.ComputeSegmentTime(
                        distance, formation, maxSpeed, powerKw, brake);
                }
                prevStation = station;
            }
            return trainTime;
        }

        private static int GetLineMaxSpeed(SimDataCache cache, string lineId)
        {
            return cache.LineMaxSpeedMap.TryGetValue(lineId, out int speed) ? speed : 80;
        }

        // ---------- 换乘时间 ----------
        public static double ComputeTotalTransferTime(SimDataCache cache, List<string> path, long travelTimeMillis)
        {
            double total = 0.0;
            string? prevDir = null;
            for (int i = 0; i < path.Count; i++)
            {
                string[] parsed = SimGraphKeyUtils.ParseNode(path[i]);
                string dirKey = parsed[0];
                if (i == 0) { prevDir = dirKey; continue; }
                if (dirKey != prevDir)
                {
                    string transferStation = SimGraphKeyUtils.ParseNode(path[i - 1])[1];
                    string targetLineId = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey)!;
                    double walkTime = SimStationUtils.GetTransferTime(cache, transferStation, prevDir!, dirKey);
                    double waitTime = ComputeTransferWaitTime(cache, dirKey, travelTimeMillis);
                    total += walkTime + waitTime;
                    prevDir = dirKey;
                }
            }
            return total;
        }

        // ---------- 停站时间 ----------
        private static double ComputeStopTime(SimDataCache cache, List<string> path, SimMetroGraph metroGraph)
        {
            if (path == null || path.Count < 2) return 0;

            double avgStopTime = cache.AvgStopTime;
            double totalStopTime = 0.0;

            for (int i = 0; i < path.Count - 1; i++)
            {
                string node = path[i];
                string[] parts = SimGraphKeyUtils.ParseNode(node);
                if (parts.Length < 2) continue;
                string dirKey = parts[0];
                string stationName = parts[1];

                string key = stationName + "|" + dirKey;
                if (cache.StationStopTimeMap.TryGetValue(key, out double customTime) && customTime > 0)
                    totalStopTime += customTime;
                else
                    totalStopTime += avgStopTime;
            }
            return totalStopTime;
        }

        // ---------- 辅助：发车间隔 ----------
        private static int FindHeadway(SimDataCache cache, string lineId, long travelTimeMillis)
        {
            var rules = cache.HeadwayRules;
            if (rules == null || rules.Count == 0) return 120;

            DateTime dt = DateTimeOffset.FromUnixTimeMilliseconds(travelTimeMillis).LocalDateTime;
            int dayOfWeek = (int)dt.DayOfWeek; // 0=Sunday, 1=Monday, ...
            int minutesOfDay = dt.Hour * 60 + dt.Minute;

            foreach (var rule in rules)
            {
                if (rule.LineId != lineId) continue;
                if (rule.Days != null && rule.Days.Count > 0)
                {
                    string dayName = dt.DayOfWeek.ToString();
                    if (!rule.Days.Contains(dayName)) continue;
                }
                int start = ParseTimeToMinutes(rule.StartTime);
                int end = ParseTimeToMinutes(rule.EndTime);
                if (minutesOfDay >= start && minutesOfDay < end)
                    return rule.IntervalSeconds;
            }
            return 120;
        }

        // ---------- 辅助：拥挤度因子 ----------
        public static double FindCrowdFactor(SimDataCache cache, string dirKey, long travelTimeMillis)
        {
            var rules = cache.CrowdRules;
            if (rules == null || rules.Count == 0) return 1.0;

            DateTime dt = DateTimeOffset.FromUnixTimeMilliseconds(travelTimeMillis).LocalDateTime;
            int dayOfWeek = (int)dt.DayOfWeek;
            int minutesOfDay = dt.Hour * 60 + dt.Minute;

            foreach (var rule in rules)
            {
                if (rule.DirectionKey != dirKey) continue;
                if (rule.Days != null && rule.Days.Count > 0)
                {
                    string dayName = dt.DayOfWeek.ToString();
                    if (!rule.Days.Contains(dayName)) continue;
                }
                int start = ParseTimeToMinutes(rule.StartTime);
                int end = ParseTimeToMinutes(rule.EndTime);
                if (minutesOfDay >= start && minutesOfDay < end)
                    return rule.Factor;
            }
            return 1.0;
        }

        private static int ParseTimeToMinutes(string time)
        {
            var parts = time.Split(':');
            return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
        }

        /// <summary>
        /// 计算在换乘站等待下一趟车的时间（秒）
        /// </summary>
        public static double ComputeTransferWaitTime(SimDataCache cache, string targetDirKey, long travelTimeMillis)
        {
            string targetLineId = SimGraphKeyUtils.GetLineIdFromDirKey(targetDirKey)!;
            int headway = FindHeadway(cache, targetLineId, travelTimeMillis);
            double crowdFactor = FindCrowdFactor(cache, targetDirKey, travelTimeMillis);
            double boardTime = cache.DefaultBoardTime;
            return headway / 2.0 + boardTime + (crowdFactor - 1) * headway;
        }
    }
}