using System.Collections.Generic;

namespace MetroEditor.Simulator.Utils
{
    /// <summary>
    /// 前缀过滤工具（从 Android PrefixFilter 转换）
    /// </summary>
    public static class SimPrefixFilter
    {
        public class FilterResult
        {
            public List<string> FilteredList { get; }
            public int RemovedCount { get; }

            public FilterResult(List<string> filteredList, int removedCount)
            {
                FilteredList = filteredList;
                RemovedCount = removedCount;
            }
        }

        /// <summary>
        /// 过滤掉连续相同前缀块中长度为1的元素。
        /// </summary>
        public static FilterResult FilterUniquePrefixBlocksWithCount(List<string> original)
        {
            if (original == null || original.Count == 0)
                return new FilterResult(new List<string>(), 0);

            var result = new List<string>();
            int removed = 0;
            int i = 0;
            while (i < original.Count)
            {
                string currentPrefix = ExtractPrefix(original[i]);
                int j = i;
                while (j < original.Count && ExtractPrefix(original[j]) == currentPrefix)
                    j++;

                int blockSize = j - i;
                if (blockSize > 1)
                    result.AddRange(original.GetRange(i, blockSize));
                else
                    removed += blockSize; // blockSize == 1

                i = j;
            }
            return new FilterResult(result, removed);
        }

        private static string ExtractPrefix(string s)
        {
            int pipeIndex = s.IndexOf('|');
            return pipeIndex == -1 ? s : s.Substring(0, pipeIndex);
        }
    }
}