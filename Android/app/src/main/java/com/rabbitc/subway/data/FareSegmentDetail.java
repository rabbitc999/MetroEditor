package com.rabbitc.subway.data;

import android.annotation.SuppressLint;

import androidx.annotation.NonNull;

import java.util.List;

/**
 * 票价段明细，表示路径中一段连续乘坐区间的票价构成。
 */
public class FareSegmentDetail {
    /** 线路ID */
    public String lineId;
    /** 显示名称（含城市前缀） */
    public String lineDisplayName;
    /** 本段起点站 */
    public String startStation;
    /** 本段终点站 */
    public String endStation;
    /** 途径站点列表（含起终点） */
    public List<String> stations;
    /** 本段里程（公里） */
    public double distance;
    /** 是否为特殊计价线路 */
    public boolean isSpecial;
    /** 基础票价（按距离阶梯计算） */
    public double baseFare;
    /** 附加费（特殊线路额外费用） */
    public double surcharge;
    /** 本段总票价 = baseFare + surcharge */
    public double totalFare;

    @SuppressLint("DefaultLocale")
    @NonNull
    @Override
    public String toString() {
        return String.format("【%s】%s → %s，%.1f km，%s票价：基础 %.2f + 附加 %.2f = %.2f 元",
                lineDisplayName, startStation, endStation, distance,
                isSpecial ? "特殊" : "普通", baseFare, surcharge, totalFare);
    }
}