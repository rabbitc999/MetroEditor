using System;
using System.Collections.Generic;
using System.Linq;
using MetroEditor.Simulator.Data;

namespace MetroEditor.Simulator.Utils
{
    /// <summary>
    /// 站点相关辅助工具（从 Android StationUtils 转换）
    /// </summary>
    public static class SimStationUtils
    {
        public static string GetStationLineStatus(SimDataCache cache, string station, string lineId)
        {
            if (cache.StationLineStatusMap.TryGetValue(station, out var lineMap))
            {
                return lineMap.TryGetValue(lineId, out string? status) ? status : "operating";
            }
            return "operating";
        }

        public static bool IsClosed(string status)
        {
            return !(status == null || status == "operating" || status == "out_station");
        }

        public static bool IsOutStation(string status)
        {
            return status == "out_station";
        }

        public static string GetTransferStatus(SimDataCache cache, string station, string dirKey1, string dirKey2)
        {
            if (!cache.TransferStatusMap.TryGetValue(station, out var transferMap))
                return "operating";

            string? line1 = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey1);
            string? line2 = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey2);
            if (string.IsNullOrEmpty(line1) || string.IsNullOrEmpty(line2))
                return "operating";

            string key = string.Compare(line1, line2) < 0 ? $"{line1}|{line2}" : $"{line2}|{line1}";
            return transferMap.TryGetValue(key, out string? status) ? status : "operating";
        }

        public static string GetTransferStatusForStation(SimDataCache cache, string stationName, string lineA, string lineB)
        {
            if (!cache.TransferStatusMap.TryGetValue(stationName, out var transferMap))
                return "operating";

            string key = string.Compare(lineA, lineB) < 0 ? $"{lineA}|{lineB}" : $"{lineB}|{lineA}";
            return transferMap.TryGetValue(key, out string? status) ? status : "operating";
        }

        public static bool IsSpecialDirKey(SimDataCache cache, string dirKey)
        {
            string? lineId = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey);
            if (string.IsNullOrEmpty(lineId)) return false;
            return cache.LineSpecialMap.TryGetValue(lineId, out bool isSpecial) && isSpecial;
        }

        public static HashSet<string> FilterOutSpecialDirs(SimDataCache cache, HashSet<string> dirKeys)
        {
            if (dirKeys == null || dirKeys.Count == 0)
                return new HashSet<string>();

            var result = new HashSet<string>();
            foreach (var dk in dirKeys)
            {
                if (!IsSpecialDirKey(cache, dk))
                    result.Add(dk);
            }
            return result;
        }

        public static double GetTransferTime(SimDataCache cache, string station, string dirKey1, string dirKey2)
        {
            string? line1 = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey1);
            string? line2 = SimGraphKeyUtils.GetLineIdFromDirKey(dirKey2);
            if (string.IsNullOrEmpty(line1) || string.IsNullOrEmpty(line2))
                return cache.AvgTransferTime;

            // 检查特殊换乘时间
            string fullKey1 = $"{station}|{line1}|{line2}";
            string fullKey2 = $"{station}|{line2}|{line1}";
            if (cache.SpecialTransferTimeMap.TryGetValue(fullKey1, out double specialTime) && specialTime > 0)
                return specialTime;
            if (cache.SpecialTransferTimeMap.TryGetValue(fullKey2, out specialTime) && specialTime > 0)
                return specialTime;

            // 根据换乘状态返回默认值
            string status = GetTransferStatus(cache, station, dirKey1, dirKey2);
            if (status == "out_station")
                return cache.OutStationTransferTime;
            else
                return cache.AvgTransferTime;
        }

        public static List<string> GetStationList(SimDataCache cache)
        {
            var list = new List<string>(cache.StationCoords.Keys);
            list.Sort();
            return list;
        }

        public static bool IsFutureStation(SimDataCache cache, string station)
        {
            var dirStations = cache.DirStations;
            var dirFutureMap = cache.DirFutureMap;

            // 找出该站点经过的所有方向键
            var dirKeys = new HashSet<string>();
            foreach (var kv in dirStations)
            {
                if (kv.Value.Contains(station))
                    dirKeys.Add(kv.Key);
            }
            if (dirKeys.Count == 0) return false;

            // 1. 检查所有方向是否均为 is_future
            bool allFuture = true;
            foreach (var dk in dirKeys)
            {
                if (!dirFutureMap.TryGetValue(dk, out bool isFuture) || !isFuture)
                {
                    allFuture = false;
                    break;
                }
            }
            if (allFuture) return true;

            // 2. 检查该站点经过的所有线路在该站的状态是否均为 pending
            if (!cache.StationLineStatusMap.TryGetValue(station, out var lineStatusMap))
                return false;

            // 获取经过的线路ID集合
            var lineIds = new HashSet<string>();
            foreach (var dk in dirKeys)
            {
                string? lineId = SimGraphKeyUtils.GetLineIdFromDirKey(dk);
                if (!string.IsNullOrEmpty(lineId))
                    lineIds.Add(lineId);
            }
            if (lineIds.Count == 0) return false;

            foreach (var lineId in lineIds)
            {
                if (!lineStatusMap.TryGetValue(lineId, out string? status) || status != "pending")
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 判断方向键是否为未来方向（已开通）
        /// </summary>
        public static bool IsFutureDirection(SimDataCache cache, string dirKey)
        {
            // 原 Java 逻辑: return !dirFutureMap.getOrDefault(dirKey, false);
            // 即如果未标记为未来（false）则为已开通（返回 true）
            return !cache.DirFutureMap.TryGetValue(dirKey, out bool isFuture) || !isFuture;
        }

        public static List<string> GetActiveStationList(SimDataCache cache)
        {
            var all = GetStationList(cache);
            var active = new List<string>();
            foreach (var station in all)
            {
                if (!IsFutureStation(cache, station))
                    active.Add(station);
            }
            return active;
        }
    }
}