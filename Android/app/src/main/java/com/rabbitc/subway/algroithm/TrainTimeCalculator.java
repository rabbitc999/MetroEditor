package com.rabbitc.subway.algroithm;

import android.annotation.SuppressLint;

import java.util.HashMap;
import java.util.HashSet;
import java.util.Locale;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * 功率模型列车运行时间计算器。
 * 基于 Davis 阻力公式和恒定功率牵引，使用自适应辛普森积分求解加速段。
 */
public final class TrainTimeCalculator {

    private static final HashSet<Character> VALID_CAR_TYPES = new HashSet<>() {{
        add('A');
        add('B');
        add('C');
        add('L');
        add('D');
    }};

    private static final Map<Character, Double> MASS_PER_CAR = new HashMap<>() {{
        put('A', 36.5);
        put('B', 33.3);
        put('L', 30.0);
        put('C', 34.0);
        put('D', 37.5);
    }};

    // Davis 阻力系数 (a, b, c) 原始值
    private static final Map<Character, double[]> DAVIS_COEFF_RAW = new HashMap<>() {{
        put('A', new double[]{2.0895, 0.0098, 0.006});
        put('B', new double[]{1.65, 0.0247, 0.00128});
        put('L', new double[]{1.87, 0.0196, 0.00102});
        put('C', new double[]{1.80, 0.0200, 0.0015});
        put('D', new double[]{2.3, 0.012, 0.007});
    }};

    private static final double DAVIS_SCALE = 1.2;
    private static final double GRAVITY = 9.81;
    private static final double DEFAULT_BRAKE_DECEL = 0.85; // m/s²

    // ΔT 缓存: key = "编组|功率|限速|制动减速度"
    private static final Map<String, Double> DELTA_CACHE = new HashMap<>();
    private static final Map<String, Double> SHORT_TIME_CACHE = new HashMap<>();

    private TrainTimeCalculator() {
        // 工具类禁止实例化
    }

    // -------------------- 编组解析 --------------------
    public static class ConsistInfo {
        public char carType;
        public int carCount;

        public ConsistInfo(char carType, int carCount) {
            this.carType = carType;
            this.carCount = carCount;
        }
    }

    private static ConsistInfo parseConsist(String consist) {
        if (consist == null || consist.isEmpty()) {
            return new ConsistInfo('B', 6);
        }

        // 编组字符串可能包含车型和节数之外的文字，因此分别提取数字与字母。
        // 提取数字（取最大值）
        Pattern numPattern = Pattern.compile("\\d+");
        Matcher m = numPattern.matcher(consist);
        int carCount = 6;
        while (m.find()) {
            int val = Integer.parseInt(m.group());
            if (val > carCount) carCount = val;
        }
        if (carCount > 12) carCount = 6; // 超限保护

        // 提取字母
        Pattern letterPattern = Pattern.compile("[A-Za-z]");
        m = letterPattern.matcher(consist);
        char rawType = 'B';
        if (m.find()) {
            rawType = Character.toUpperCase(m.group().charAt(0));
        }
        char carType = VALID_CAR_TYPES.contains(rawType) ? rawType : 'B';

        return new ConsistInfo(carType, carCount);
    }

    // -------------------- 阻力函数 --------------------
    private static FuncDouble getResistanceFunc(char carType, double massTon) {
        double[] coeff = DAVIS_COEFF_RAW.get(carType);
        double a = coeff[0] * DAVIS_SCALE;
        double b = coeff[1] * DAVIS_SCALE;
        double c = coeff[2] * DAVIS_SCALE;
        double weightKN = massTon * GRAVITY;

        return v -> (a + b * v + c * v * v) * weightKN; // v in m/s, result in Newton
    }

    // -------------------- 功率限制速度求解 --------------------
    private static double findPowerLimitSpeed(double powerW, FuncDouble R) {
        // 牵引功率等于阻力功率时达到功率上限，离散扫描结果作为后续积分的速度上界。
        // 在 0.1 ~ 100 m/s 内寻找使 v*R(v) > powerW 的第一个点
        for (double v = 0.1; v <= 100.0; v += 0.05) {
            if (v * R.eval(v) > powerW) {
                return v;
            }
        }
        return 50.0; // fallback
    }

    // -------------------- 自适应辛普森积分 --------------------
    private static double adaptiveSimpson(FuncDouble f, double a, double b,
                                          double eps, int maxDepth) {
        // 先用一次 Simpson 估计整体区间，再递归细分误差较大的子区间。
        if (a > b) { double tmp = a; a = b; b = tmp; }
        if (a == b) return 0.0;

        double mid = (a + b) / 2;
        double fa = f.eval(a);
        double fm = f.eval(mid);
        double fb = f.eval(b);
        double whole = (b - a) * (fa + 4 * fm + fb) / 6.0;

        return adaptiveSimpsonRecursive(f, a, b, fa, fm, fb, whole, eps, maxDepth, 0);
    }

    private static double adaptiveSimpsonRecursive(FuncDouble f, double a, double b,
                                                   double fa, double fm, double fb,
                                                   double whole, double eps, int maxDepth, int depth) {
        double mid = (a + b) / 2;
        double leftMid = (a + mid) / 2;
        double rightMid = (mid + b) / 2;
        double fLeftMid = f.eval(leftMid);
        double fRightMid = f.eval(rightMid);

        double left = (mid - a) * (fa + 4 * fLeftMid + fm) / 6.0;
        double right = (b - mid) * (fm + 4 * fRightMid + fb) / 6.0;

        double diff = left + right - whole;
        if (depth >= maxDepth || Math.abs(diff) < 15 * eps) {
            return left + right + diff / 15.0;
        }

        double eps2 = eps / 2.0;
        return adaptiveSimpsonRecursive(f, a, mid, fa, fLeftMid, fm, left, eps2, maxDepth, depth + 1)
                + adaptiveSimpsonRecursive(f, mid, b, fm, fRightMid, fb, right, eps2, maxDepth, depth + 1);
    }

    // -------------------- 加速段计算 (时间、距离) --------------------
    private static double[] calcAcceleration(double vTarget, double massKg,
                                             double powerW, FuncDouble R) {
        if (vTarget <= 0) return new double[]{0, 0};

        // 对速度积分可同时得到加速时间和加速距离，避免使用固定加速度近似。
        double vEff = vTarget * 0.99; // 避免奇点

        // 被积函数 dt/dv = m*v / (P - v*R(v))
        FuncDouble integrandT = v -> {
            double denom = powerW - v * R.eval(v);
            return denom > 1e-6 ? massKg * v / denom : 0;
        };
        // ds/dv = v * dt/dv
        FuncDouble integrandS = v -> {
            double denom = powerW - v * R.eval(v);
            return denom > 1e-6 ? massKg * v * v / denom : 0;
        };

        double eps = 1e-6;
        int maxDepth = 200;
        double t = adaptiveSimpson(integrandT, 0, vEff, eps, maxDepth);
        double s = adaptiveSimpson(integrandS, 0, vEff, eps, maxDepth);
        return new double[]{t, s};
    }

    // -------------------- 精确运行时间计算 --------------------
    public static double computeRunTimeExact(double distanceM, String consist,
                                             double vMaxKmh, double powerKw,
                                             double brakeDecel) {
        ConsistInfo info = parseConsist(consist);
        double massTon = MASS_PER_CAR.get(info.carType) * info.carCount;
        double massKg = massTon * 1000.0;
        double powerW = powerKw * 1000.0;
        double vMax = vMaxKmh / 3.6;

        FuncDouble R = getResistanceFunc(info.carType, massTon);

        // 先限制可达最高速度，再分别计算加速、巡航和制动阶段。
        // 功率限制速度
        double vLimit = findPowerLimitSpeed(powerW, R);
        double vReach = Math.min(vMax, vLimit * 0.99);

        // 加速段
        double[] accResult = calcAcceleration(vReach, massKg, powerW, R);
        double tAcc = accResult[0];
        double sAcc = accResult[1];

        // 制动段（恒定减速度）
        double tBrake = vReach / brakeDecel;
        double sBrake = vReach * vReach / (2 * brakeDecel);

        if (sAcc + sBrake <= distanceM) {
            // 有巡航段
            double sCruise = distanceM - sAcc - sBrake;
            double tCruise = sCruise / vReach;
            return tAcc + tCruise + tBrake;
        } else {
            // 短区间无法达到最高速，二分法求“加速距离 + 制动距离”恰好覆盖区间的峰值速度。
            double lo = 0, hi = vReach;
            for (int i = 0; i < 60; i++) {
                double mid = (lo + hi) / 2;
                double[] accMid = calcAcceleration(mid, massKg, powerW, R);
                double totalDist = accMid[1] + mid * mid / (2 * brakeDecel);
                if (totalDist < distanceM) {
                    lo = mid;
                } else {
                    hi = mid;
                }
            }
            double vPeak = (lo + hi) / 2;
            double[] accPeak = calcAcceleration(vPeak, massKg, powerW, R);
            double tBrakePeak = vPeak / brakeDecel;
            return accPeak[0] + tBrakePeak;
        }
    }

    // -------------------- ΔT 缓存 --------------------
    @SuppressLint("DefaultLocale")
    private static double getDeltaT(String consist, double powerKw,
                                    double vMaxKmh, double brakeDecel) {
        String key = String.format("%s|%.0f|%.0f|%.2f", consist, powerKw, vMaxKmh, brakeDecel);
        Double cached = DELTA_CACHE.get(key);
        if (cached != null) return cached;

        // 以 10 km 参考距离计算相对匀速运行的额外时间，后续按区间长度复用缓存。
        final double REF_DIST = 10000.0; // 10 km
        double tRef = computeRunTimeExact(REF_DIST, consist, vMaxKmh, powerKw, brakeDecel);
        double vMaxMs = vMaxKmh / 3.6;
        double delta = tRef - REF_DIST / vMaxMs;
        DELTA_CACHE.put(key, delta);
        return delta;
    }

    // -------------------- 对外接口 --------------------
    /**
     * 计算区间运行时间（秒）。
     *
     * @param distanceKm 区间距离（千米）
     * @param consist    编组，如 "6B"、"8A"
     * @param vMaxKmh    最高限速（千米/小时）
     * @param powerKw    牵引功率（千瓦）
     * @param brakeDecel 制动减速度（m/s²），若 <=0 则使用默认 0.85
     * @return 运行时间（秒）
     */
    public static double computeSegmentTime(double distanceKm, String consist,
                                            double vMaxKmh, double powerKw,
                                            double brakeDecel) {
        if (distanceKm <= 0) return 0;
        if (brakeDecel <= 0) brakeDecel = DEFAULT_BRAKE_DECEL;

        // 1. 构建缓存 Key
        // 将距离舍入到小数点后 5 位（即 0.00001 km = 0.01 米）
        // 在 30 m/s 速度下，0.01 米耗时 ≈ 0.00033 秒 < 0.001 秒，满足精度要求
        double roundedDist = Math.round(distanceKm * 100000.0) / 100000.0;
        @SuppressLint("DefaultLocale")
        String key = String.format(Locale.US, "%.5f|%s|%.0f|%.0f|%.2f",
                roundedDist, consist, powerKw, vMaxKmh, brakeDecel);

        // 2. 命中缓存则直接返回
        Double cached = SHORT_TIME_CACHE.get(key);
        if (cached != null) {
            return cached;
        }

        double distanceM = distanceKm * 1000.0;
        double shortThreshold = 1.5; // km

        double result;
        if (distanceKm <= shortThreshold) {
            // 短区间：精确积分计算
            result = computeRunTimeExact(distanceM, consist, vMaxKmh, powerKw, brakeDecel);
        } else {
            // 长区间：使用 ΔT 线性近似（已有缓存）
            double vMaxMs = vMaxKmh / 3.6;
            double delta = getDeltaT(consist, powerKw, vMaxKmh, brakeDecel);
            result = distanceM / vMaxMs + delta;
        }

        // 3. 仅对短区间结果进行缓存（长区间已经由 getDeltaT 缓存了 ΔT）
        if (distanceKm <= shortThreshold) {
            SHORT_TIME_CACHE.put(key, result);
        }

        return result;
    }

    // -------------------- 函数接口（辅助） --------------------
    private interface FuncDouble {
        double eval(double x);
    }
}
