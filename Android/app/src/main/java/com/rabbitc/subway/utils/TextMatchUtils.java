package com.rabbitc.subway.utils;

import android.text.TextUtils;

import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.regex.Pattern;

/**
 * 文本匹配工具类。
 * <p>
 * 提供 Levenshtein 编辑距离计算、线路名称前缀与数字提取、
 * 线路名称排序比较及站点名称模糊匹配等功能，
 * 用于站点搜索自动补全与线路列表排序等场景。
 * </p>
 */
public class TextMatchUtils {
    /**
     * 计算两个字符串的 Levenshtein 编辑距离。
     *
     * @param s1 字符串1
     * @param s2 字符串2
     * @return 编辑距离
     */
    public static int levenshteinDistance(String s1, String s2) {
        int len1 = s1.length(), len2 = s2.length();
        int[][] dp = new int[len1 + 1][len2 + 1];
        for (int i = 0; i <= len1; i++) dp[i][0] = i;
        for (int j = 0; j <= len2; j++) dp[0][j] = j;
        for (int i = 1; i <= len1; i++) {
            char c1 = s1.charAt(i - 1);
            for (int j = 1; j <= len2; j++) {
                char c2 = s2.charAt(j - 1);
                int cost = (c1 == c2) ? 0 : 1;
                dp[i][j] = Math.min(Math.min(dp[i - 1][j] + 1, dp[i][j - 1] + 1), dp[i - 1][j - 1] + cost);
            }
        }
        return dp[len1][len2];
    }

    /**
     * 提取线路名称的纯汉字前缀（去除数字和"号线"）。
     * 例如："1号线" -> ""， "佛山1号线" -> "佛山"。
     *
     * @param lineName 线路名称
     * @return 前缀字符串
     */
    public static String extractPrefix(String lineName) {
        Pattern p = Pattern.compile("^(\\D*?)(\\d+)");
        java.util.regex.Matcher m = p.matcher(lineName);
        String prefix = "";
        if (m.find()) {
            prefix = m.group(1);
        }
        if (prefix.endsWith("号线")) {
            prefix = prefix.substring(0, prefix.length() - 2);
        }
        return prefix;
    }

    /**
     * 从线路名称中提取第一个数字。
     * 例如 "1号线/八通线" -> 1, "10号线" -> 10, "佛山1号线" -> 1。
     *
     * @param lineName 线路名称
     * @return 数字，若未找到返回 Integer.MAX_VALUE
     */
    public static int extractLineNumber(String lineName) {
        Pattern p = Pattern.compile("\\d+");
        java.util.regex.Matcher m = p.matcher(lineName);
        if (m.find()) {
            return Integer.parseInt(m.group());
        }
        return Integer.MAX_VALUE;
    }

    /**
     * 比较两条线路名称，按数字、前缀排序（用于列表排序）。
     *
     * @param nameA 线路名称A
     * @param nameB 线路名称B
     * @return 比较结果
     */
    public static int compareLineName(String nameA, String nameB) {
        String prefixA = extractPrefix(nameA);
        String prefixB = extractPrefix(nameB);
        boolean emptyA = prefixA.isEmpty();
        boolean emptyB = prefixB.isEmpty();
        if (emptyA != emptyB) {
            return emptyA ? -1 : 1;
        }
        int cmpPrefix = prefixA.compareTo(prefixB);
        if (cmpPrefix != 0) {
            return cmpPrefix;
        }
        int numA = extractLineNumber(nameA);
        int numB = extractLineNumber(nameB);
        if (numA != numB) {
            return Integer.compare(numA, numB);
        }
        return nameA.compareTo(nameB);
    }

    /**
     * 在站点列表中寻找与输入最匹配的站点名称。
     * 规则：精确匹配 → 前缀匹配 → 模糊匹配（至少一个共同汉字，编辑距离≤2）。
     *
     * @param input       输入字符串
     * @param stationList 候选站点列表
     * @return 最佳匹配站名，若无则返回 null
     */
    public static String findBestMatch(String input, List<String> stationList) {
        if (TextUtils.isEmpty(input)) return "";
        String trimmed = input.trim();

        // 1. 精确匹配
        for (String s : stationList) {
            if (s.equals(trimmed)) return s;
        }

        // 2. 前缀匹配
        for (String s : stationList) {
            if (s.startsWith(trimmed)) return s;
        }

        // 3. 模糊匹配
        Set<Character> inputChars = new HashSet<>();
        for (char c : trimmed.toCharArray()) inputChars.add(c);
        String best = "";
        int bestDist = Integer.MAX_VALUE;

        for (String station : stationList) {
            boolean hasCommon = false;
            for (char c : station.toCharArray()) {
                if (inputChars.contains(c)) {
                    hasCommon = true;
                    break;
                }
            }
            if (!hasCommon) continue;
            int dist = levenshteinDistance(trimmed, station);
            if (dist <= 2 && dist < bestDist) {
                bestDist = dist;
                best = station;
            }
        }
        return best;
    }
}
