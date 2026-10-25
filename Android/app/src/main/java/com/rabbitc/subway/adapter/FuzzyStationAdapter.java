package com.rabbitc.subway.adapter;

import android.content.Context;
import android.os.Build;
import android.util.Pair;
import android.widget.ArrayAdapter;
import android.widget.Filter;

import androidx.annotation.NonNull;

import com.rabbitc.subway.utils.TextMatchUtils;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashSet;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

/**
 * 支持模糊匹配的站点输入适配器。
 * <p>
 * 匹配策略：
 * 1. 优先前缀匹配（站名以输入开头）
 * 2. 若无前缀匹配，则进行模糊匹配：
 *    - 站名必须包含输入中的至少一个汉字（避免无关推荐）
 *    - 计算编辑距离，距离 ≤ 1 的站点被推荐，并按距离升序排列
 * </p>
 */
public class FuzzyStationAdapter extends ArrayAdapter<String> {

    // ---------- 成员变量 ----------
    private final List<String> allStations;

    // ---------- 构造方法 ----------
    /**
     * 构造模糊匹配站点适配器。
     *
     * @param context  上下文
     * @param stations 所有可选的站点名称列表
     */
    public FuzzyStationAdapter(Context context, List<String> stations) {
        super(context, android.R.layout.simple_dropdown_item_1line, stations);
        this.allStations = new ArrayList<>(stations);
    }

    // ---------- 核心：自定义过滤器 ----------
    /**
     * 返回自定义 {@link Filter}，支持前缀优先匹配和基于编辑距离的模糊匹配。
     * <p>
     * 过滤逻辑：先尝试前缀匹配，若无结果则进行模糊匹配（要求站名包含输入中的至少一个字符，且编辑距离 ≤ 2）。
     * </p>
     *
     * @return 自定义 Filter 实例
     */
    @NonNull
    @Override
    public Filter getFilter() {
        return new Filter() {

            @Override
            protected FilterResults performFiltering(CharSequence constraint) {
                FilterResults results = new FilterResults();
                if (constraint == null || constraint.length() == 0) {
                    results.values = allStations;
                    results.count = allStations.size();
                    return results;
                }

                String input = constraint.toString().trim();
                Set<String> matched = new LinkedHashSet<>();

                // 先给出确定性最高的前缀结果，再追加编辑距离匹配结果。
                // 1. 优先前缀匹配
                for (String station : allStations) {
                    if (station.startsWith(input)) {
                        matched.add(station);
                    }
                }

                // 2. 若无前缀匹配，进入模糊匹配
                matched.addAll(performFuzzyMatch(input, allStations));

                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                    results.values = matched.stream().toList();
                }
                results.count = matched.size();
                return results;
            }

            @Override
            protected void publishResults(CharSequence constraint, FilterResults results) {
                clear();
                if (results.values != null) {
                    addAll((List<String>) results.values);
                }
                notifyDataSetChanged();
            }
        };
    }

    // ---------- 模糊匹配逻辑 ----------
    /**
     * 执行模糊匹配：筛选出与输入字符串编辑距离 ≤ 2 且包含共同字符的站点。
     * <p>
     * 候选项按编辑距离升序排列，距离越小匹配度越高。
     * </p>
     *
     * @param input 用户输入的搜索字符串
     * @return 按匹配度排序的匹配站点列表
     * @see TextMatchUtils#levenshteinDistance(String, String)
     */
    public static List<String> performFuzzyMatch(String input, List<String> allStations) {
        Set<Character> inputCharSet = new HashSet<>();
        for (char c : input.toCharArray()) {
            inputCharSet.add(c);
        }

        List<Pair<String, Integer>> candidates = new ArrayList<>();

        for (String station : allStations) {
            // 【关键过滤】站名必须至少包含输入中的一个汉字
            boolean hasCommonChar = false;
            for (char c : station.toCharArray()) {
                if (inputCharSet.contains(c)) {
                    hasCommonChar = true;
                    break;
                }
            }
            if (!hasCommonChar) {
                continue; // 完全无关，不推荐
            }

            // 计算编辑距离，距离 ≤ 2 才推荐
            int distance = TextMatchUtils.levenshteinDistance(input, station);
            if (distance <= 2) {
                candidates.add(new Pair<>(station, distance));
            }
        }

        // 先按编辑距离排序，距离相同的站点保持原始数据顺序。
        // 按编辑距离升序排序
        candidates.sort(Comparator.comparingInt(p -> p.second));

        List<String> result = new ArrayList<>();
        for (Pair<String, Integer> p : candidates) {
            result.add(p.first);
        }
        return result;
    }
}
