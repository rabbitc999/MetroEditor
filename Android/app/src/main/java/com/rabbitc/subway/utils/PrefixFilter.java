package com.rabbitc.subway.utils;

import java.util.ArrayList;
import java.util.List;

/**
 * 前缀过滤工具类。
 * <p>
 * 用于自动补全场景中对字符串列表按前缀分块过滤，
 * 移除连续相同前缀块中长度为 1 的孤立元素，保留多元素前缀块。
 * </p>
 */
public class PrefixFilter {
    /**
     * 过滤结果封装类，包含过滤后的列表和被移除的元素数量。
     */
    public static class FilterResult {
        private final List<String> filteredList;
        private final int removedCount;

        public FilterResult(List<String> filteredList, int removedCount) {
            this.filteredList = filteredList;
            this.removedCount = removedCount;
        }

        /**
         * 获取过滤后的字符串列表。
         *
         * @return 过滤后的列表
         */
        public List<String> getFilteredList() {
            return filteredList;
        }

        /**
         * 获取被移除的元素数量。
         *
         * @return 被移除的数量
         */
        public int getRemovedCount() {
            return removedCount;
        }
    }

    /**
     * 过滤掉连续相同前缀块中长度为1的元素。
     * 例如：[G3|a, G3|b, G1|c, G3|d] → 保留 G3|a, G3|b, G3|d（删除 G1|c）
     *
     * @param original 原始字符串列表
     * @return 过滤后的新列表
     */
    public static FilterResult filterUniquePrefixBlocksWithCount(List<String> original) {
        if (original == null || original.isEmpty()) {
            return new FilterResult(new ArrayList<>(), 0);
        }

        List<String> result = new ArrayList<>();
        int removed = 0;
        int i = 0;
        while (i < original.size()) {
            String currentPrefix = extractPrefix(original.get(i));
            int j = i;
            while (j < original.size() && extractPrefix(original.get(j)).equals(currentPrefix)) {
                j++;
            }
            int blockSize = j - i;
            if (blockSize > 1) {
                result.addAll(original.subList(i, j));
            } else {
                removed += blockSize; // blockSize == 1
            }
            i = j;
        }
        return new FilterResult(result, removed);
    }

    /** 提取 "|" 前的部分 */
    private static String extractPrefix(String s) {
        int pipeIndex = s.indexOf('|');
        return pipeIndex == -1 ? s : s.substring(0, pipeIndex);
    }
}
