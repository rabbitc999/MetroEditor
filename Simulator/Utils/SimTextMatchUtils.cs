using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MetroEditor.Simulator.Utils
{
    /// <summary>
    /// 文本匹配和线路名称排序工具（从 Android TextMatchUtils 转换）
    /// </summary>
    public static class SimTextMatchUtils
    {
        /// <summary>
        /// 计算 Levenshtein 编辑距离
        /// </summary>
        public static int LevenshteinDistance(string s1, string s2)
        {
            int len1 = s1.Length, len2 = s2.Length;
            var dp = new int[len1 + 1, len2 + 1];
            for (int i = 0; i <= len1; i++) dp[i, 0] = i;
            for (int j = 0; j <= len2; j++) dp[0, j] = j;

            for (int i = 1; i <= len1; i++)
            {
                char c1 = s1[i - 1];
                for (int j = 1; j <= len2; j++)
                {
                    char c2 = s2[j - 1];
                    int cost = (c1 == c2) ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost
                    );
                }
            }
            return dp[len1, len2];
        }

        /// <summary>
        /// 提取线路名称的纯汉字前缀（去除数字和"号线"）
        /// </summary>
        public static string ExtractPrefix(string lineName)
        {
            var match = Regex.Match(lineName, @"^(\D*?)(\d+)");
            string prefix = match.Success ? match.Groups[1].Value : "";
            if (prefix.EndsWith("号线"))
                prefix = prefix.Substring(0, prefix.Length - 2);
            return prefix;
        }

        /// <summary>
        /// 提取第一个数字
        /// </summary>
        public static int ExtractLineNumber(string lineName)
        {
            var match = Regex.Match(lineName, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int num))
                return num;
            return int.MaxValue;
        }

        /// <summary>
        /// 比较两条线路名称（按数字、前缀排序）
        /// </summary>
        public static int CompareLineName(string nameA, string nameB)
        {
            string prefixA = ExtractPrefix(nameA);
            string prefixB = ExtractPrefix(nameB);
            bool emptyA = string.IsNullOrEmpty(prefixA);
            bool emptyB = string.IsNullOrEmpty(prefixB);
            if (emptyA != emptyB)
                return emptyA ? -1 : 1;

            int cmpPrefix = string.Compare(prefixA, prefixB, StringComparison.Ordinal);
            if (cmpPrefix != 0) return cmpPrefix;

            int numA = ExtractLineNumber(nameA);
            int numB = ExtractLineNumber(nameB);
            if (numA != numB)
                return numA.CompareTo(numB);

            return string.Compare(nameA, nameB, StringComparison.Ordinal);
        }

        /// <summary>
        /// 在站点列表中寻找与输入最匹配的站点名称
        /// </summary>
        public static string? FindBestMatch(string input, List<string> stationList)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            string trimmed = input.Trim();

            // 1. 精确匹配
            foreach (var s in stationList)
                if (s == trimmed) return s;

            // 2. 前缀匹配
            foreach (var s in stationList)
                if (s.StartsWith(trimmed, StringComparison.Ordinal)) return s;

            // 3. 模糊匹配：至少一个共同汉字，编辑距离 ≤ 2
            var inputChars = new HashSet<char>(trimmed);
            string best = "";
            int bestDist = int.MaxValue;

            foreach (var station in stationList)
            {
                bool hasCommon = false;
                foreach (char c in station)
                {
                    if (inputChars.Contains(c))
                    {
                        hasCommon = true;
                        break;
                    }
                }
                if (!hasCommon) continue;

                int dist = LevenshteinDistance(trimmed, station);
                if (dist <= 2 && dist < bestDist)
                {
                    bestDist = dist;
                    best = station;
                }
            }
            return string.IsNullOrEmpty(best) ? null : best;
        }
    }
}