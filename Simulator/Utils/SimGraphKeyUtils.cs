using System;
using System.Collections.Generic;
using System.Linq;
using MetroEditor.Simulator.Data;

namespace MetroEditor.Simulator.Utils
{
    /// <summary>
    /// 方向键和节点操作工具类（从 Android GraphKeyUtils 转换）
    /// </summary>
    public static class SimGraphKeyUtils
    {
        public static string MakeDirKey(string lineId, string start, string end)
        {
            return $"{lineId}|{start}|{end}";
        }

        public static string[] ParseDirKey(string dirKey)
        {
            return dirKey.Split('|');
        }

        public static string MakeNode(string dirKey, string station)
        {
            return $"{dirKey}|{station}";
        }

        public static string[] ParseNode(string node)
        {
            int idx = node.LastIndexOf('|');
            return new[] { node.Substring(0, idx), node.Substring(idx + 1) };
        }

        public static string? GetLineIdFromDirKey(string dirKey)
        {
            var parts = ParseDirKey(dirKey);
            return parts.Length > 0 ? parts[0] : null;
        }

        /// <summary>
        /// 判断两个方向键是否表示同一线路的同一方向（包含正向和反向）。
        /// </summary>
        public static bool IsEndEqual(SimDataCache cache, string dir1, string dir2)
        {
            string lineId1 = GetLineIdFromDirKey(dir1);
            string lineId2 = GetLineIdFromDirKey(dir2);
            if (lineId1 != lineId2) return false;

            var parts1 = ParseDirKey(dir1);
            var parts2 = ParseDirKey(dir2);
            if (parts1.Length < 3 || parts2.Length < 3) return false;

            string start1 = parts1[1], end1 = parts1[2];
            string start2 = parts2[1], end2 = parts2[2];

            // 检查是否环线（从缓存中获取）
            bool loop1 = cache.DirLoopMap.TryGetValue(dir1, out bool l1) && l1;
            bool loop2 = cache.DirLoopMap.TryGetValue(dir2, out bool l2) && l2;

            if (loop1 && loop2) return true;

            bool isSameDir = end1 == end2;
            bool isRevDir = start1 == end2 || start2 == end1;
            return isSameDir || isRevDir;
        }

        public static int CountTransfers(List<string> path)
        {
            if (path == null || path.Count < 2) return 0;
            int transfers = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                string dir1 = ParseNode(path[i])[0];
                string dir2 = ParseNode(path[i + 1])[0];
                if (dir1 != dir2) transfers++;
            }
            return transfers;
        }

        /// <summary>
        /// 判断两个站点列表是否互为逆序，或（当 isLoop=true 时）站点集合相同。
        /// </summary>
        public static bool IsDuplicateDirection(List<string> a, List<string> b, bool isLoop)
        {
            if (a.Count != b.Count) return false;
            if (isLoop)
            {
                var setA = new HashSet<string>(a);
                var setB = new HashSet<string>(b);
                return setA.SetEquals(setB);
            }
            else
            {
                int n = a.Count;
                for (int i = 0; i < n; i++)
                {
                    if (a[i] != b[n - 1 - i]) return false;
                }
                return true;
            }
        }
    }
}