using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MetroEditor.Simulator.Utils
{
    /// <summary>
    /// 列车运行时间计算器（物理模型驱动）
    /// 基于戴维斯阻力公式、牛顿力学和数值积分计算列车在区间内的运行时间
    /// 核心特性：
    /// 1. 支持多种车型（A/B/C/L/D型）的物理特性建模
    /// 2. 考虑空气阻力、机械阻力和重力影响
    /// 3. 使用自适应辛普森积分法进行高精度数值计算
    /// 4. 智能缓存机制优化性能
    /// </summary>
    /// <remarks>
    /// 物理模型假设：
    /// - 列车为质点模型，忽略车厢间相互作用
    /// - 阻力遵循戴维斯公式：R = a + b·v + c·v²
    /// - 牵引力恒定（功率恒定模式）
    /// - 制动力恒定（减速度恒定）
    /// - 忽略坡度和曲线影响（可在后续扩展）
    /// </remarks>
    public static class SimTrainTimeUtils
    {
        private static readonly HashSet<char> ValidCarTypes = new() { 'A', 'B', 'C', 'L', 'D' };
        public static bool DisableExcatlyCalc { get; set; } = false;

        private static readonly Dictionary<char, double> MassPerCar = new()
        {
            { 'A', 36.5 },
            { 'B', 33.3 },
            { 'L', 30.0 },
            { 'C', 34.0 },
            { 'D', 37.5 }
        };

        private static readonly Dictionary<char, double[]> DavisCoeffRaw = new()
        {
            { 'A', new double[] { 2.0895, 0.0098, 0.006 } },
            { 'B', new double[] { 1.65, 0.0247, 0.00128 } },
            { 'L', new double[] { 1.87, 0.0196, 0.00102 } },
            { 'C', new double[] { 1.80, 0.0200, 0.0015 } },
            { 'D', new double[] { 2.3, 0.012, 0.007 } }
        };

        private const double DavisScale = 1.2;
        private const double Gravity = 9.81;
        private const double DefaultBrakeDecel = 0.85;

        // ΔT 缓存（线程安全）
        private static readonly ConcurrentDictionary<string, double> DeltaCache = new();
        private static readonly ConcurrentDictionary<string, double> ShortTimeCache = new();

        private class ConsistInfo
        {
            public char CarType { get; }
            public int CarCount { get; }

            public ConsistInfo(char carType, int carCount)
            {
                CarType = carType;
                CarCount = carCount;
            }
        }

        private static ConsistInfo ParseConsist(string consist)
        {
            if (string.IsNullOrEmpty(consist))
                return new ConsistInfo('B', 6);

            // 提取数字（取最大值）
            int carCount = 6;
            foreach (Match m in Regex.Matches(consist, @"\d+"))
            {
                if (int.TryParse(m.Value, out int val) && val > carCount)
                    carCount = val;
            }
            if (carCount > 12) carCount = 6;

            // 提取字母
            char rawType = 'B';
            var letterMatch = Regex.Match(consist, @"[A-Za-z]");
            if (letterMatch.Success)
                rawType = char.ToUpperInvariant(letterMatch.Value[0]);

            char carType = ValidCarTypes.Contains(rawType) ? rawType : 'B';
            return new ConsistInfo(carType, carCount);
        }

        private delegate double FuncDouble(double x);

        private static FuncDouble GetResistanceFunc(char carType, double massTon)
        {
            double[] coeff = DavisCoeffRaw[carType];
            double a = coeff[0] * DavisScale;
            double b = coeff[1] * DavisScale;
            double c = coeff[2] * DavisScale;
            double weightKN = massTon * Gravity;

            return v => (a + b * v + c * v * v) * weightKN; // v in m/s, result in Newton
        }

        private static double FindPowerLimitSpeed(double powerW, FuncDouble R)
        {
            for (double v = 0.1; v <= 100.0; v += 0.05)
            {
                if (v * R(v) > powerW)
                    return v;
            }
            return 50.0;
        }

        private static double AdaptiveSimpson(FuncDouble f, double a, double b,
                                              double eps, int maxDepth)
        {
            if (a > b) (a, b) = (b, a);
            if (a == b) return 0.0;

            double mid = (a + b) / 2;
            double fa = f(a);
            double fm = f(mid);
            double fb = f(b);
            double whole = (b - a) * (fa + 4 * fm + fb) / 6.0;

            return AdaptiveSimpsonRecursive(f, a, b, fa, fm, fb, whole, eps, maxDepth, 0);
        }

        private static double AdaptiveSimpsonRecursive(FuncDouble f, double a, double b,
                                                       double fa, double fm, double fb,
                                                       double whole, double eps, int maxDepth, int depth)
        {
            double mid = (a + b) / 2;
            double leftMid = (a + mid) / 2;
            double rightMid = (mid + b) / 2;
            double fLeftMid = f(leftMid);
            double fRightMid = f(rightMid);

            double left = (mid - a) * (fa + 4 * fLeftMid + fm) / 6.0;
            double right = (b - mid) * (fm + 4 * fRightMid + fb) / 6.0;

            double diff = left + right - whole;
            if (depth >= maxDepth || Math.Abs(diff) < 15 * eps)
                return left + right + diff / 15.0;

            double eps2 = eps / 2.0;
            return AdaptiveSimpsonRecursive(f, a, mid, fa, fLeftMid, fm, left, eps2, maxDepth, depth + 1)
                 + AdaptiveSimpsonRecursive(f, mid, b, fm, fRightMid, fb, right, eps2, maxDepth, depth + 1);
        }

        private static double[] CalcAcceleration(double vTarget, double massKg,
                                                 double powerW, FuncDouble R)
        {
            if (vTarget <= 0) return new double[] { 0, 0 };

            double vEff = vTarget * 0.99;

            FuncDouble integrandT = v =>
            {
                double denom = powerW - v * R(v);
                return denom > 1e-6 ? massKg * v / denom : 0;
            };
            FuncDouble integrandS = v =>
            {
                double denom = powerW - v * R(v);
                return denom > 1e-6 ? massKg * v * v / denom : 0;
            };

            double eps = 1e-6;
            int maxDepth = 200;
            double t = AdaptiveSimpson(integrandT, 0, vEff, eps, maxDepth);
            double s = AdaptiveSimpson(integrandS, 0, vEff, eps, maxDepth);
            return new double[] { t, s };
        }

        private static double ComputeRunTimeExact(double distanceM, string consist,
                                                  double vMaxKmh, double powerKw,
                                                  double brakeDecel)
        {
            ConsistInfo info = ParseConsist(consist);
            double massTon = MassPerCar[info.CarType] * info.CarCount;
            double massKg = massTon * 1000.0;
            double powerW = powerKw * 1000.0;
            double vMax = vMaxKmh / 3.6;

            FuncDouble R = GetResistanceFunc(info.CarType, massTon);

            double vLimit = FindPowerLimitSpeed(powerW, R);
            double vReach = Math.Min(vMax, vLimit * 0.99);

            double[] accResult = CalcAcceleration(vReach, massKg, powerW, R);
            double tAcc = accResult[0];
            double sAcc = accResult[1];

            double tBrake = vReach / brakeDecel;
            double sBrake = vReach * vReach / (2 * brakeDecel);

            if (sAcc + sBrake <= distanceM)
            {
                double sCruise = distanceM - sAcc - sBrake;
                double tCruise = sCruise / vReach;
                return tAcc + tCruise + tBrake;
            }
            else
            {
                double lo = 0, hi = vReach;
                for (int i = 0; i < 60; i++)
                {
                    double mid = (lo + hi) / 2;
                    double[] accMid = CalcAcceleration(mid, massKg, powerW, R);
                    double totalDist = accMid[1] + mid * mid / (2 * brakeDecel);
                    if (totalDist < distanceM)
                        lo = mid;
                    else
                        hi = mid;
                }
                double vPeak = (lo + hi) / 2;
                double[] accPeak = CalcAcceleration(vPeak, massKg, powerW, R);
                double tBrakePeak = vPeak / brakeDecel;
                return accPeak[0] + tBrakePeak;
            }
        }

        public static double GetDeltaT(string consist, double powerKw,
                                        double vMaxKmh, double brakeDecel)
        {
            string key = $"{consist}|{powerKw:F0}|{vMaxKmh:F0}|{brakeDecel:F2}";
            if (DeltaCache.TryGetValue(key, out double cached))
                return cached;

            const double REF_DIST = 10000.0;
            double tRef = ComputeRunTimeExact(REF_DIST, consist, vMaxKmh, powerKw, brakeDecel);
            double vMaxMs = vMaxKmh / 3.6;
            double delta = tRef - REF_DIST / vMaxMs;
            DeltaCache[key] = delta;
            return delta;
        }

        /// <summary>
        /// 计算区间运行时间（秒）
        /// </summary>
        /// <param name="distanceKm">区间距离（千米）</param>
        /// <param name="consist">编组，如 "6B"</param>
        /// <param name="vMaxKmh">最高限速（km/h）</param>
        /// <param name="powerKw">牵引功率（kW）</param>
        /// <param name="brakeDecel">制动减速度（m/s²），若 ≤0 则使用默认 0.85</param>
        /// <returns>运行时间（秒）</returns>
        public static double ComputeSegmentTime(double distanceKm, string consist,
                                                double vMaxKmh, double powerKw,
                                                double brakeDecel)
        {
            if (distanceKm <= 0) return 0;
            if (brakeDecel <= 0) brakeDecel = DefaultBrakeDecel;
            double distanceM = distanceKm * 1000.0;

            if (DisableExcatlyCalc)
            {
                double vMaxMs = vMaxKmh / 3.6;
                return distanceM / vMaxMs + 60;  // 默认加速间隔60秒
            }

            double roundedDist = Math.Round(distanceKm * 100000.0) / 100000.0;
            string key = $"{roundedDist:F5}|{consist}|{powerKw:F0}|{vMaxKmh:F0}|{brakeDecel:F2}";

            if (ShortTimeCache.TryGetValue(key, out double cached))
                return cached;
            const double shortThreshold = 1.5;

            double result;
            if (distanceKm <= shortThreshold)
                result = ComputeRunTimeExact(distanceM, consist, vMaxKmh, powerKw, brakeDecel);
            else
            {
                double vMaxMs = vMaxKmh / 3.6;
                double delta = GetDeltaT(consist, powerKw, vMaxKmh, brakeDecel);
                result = distanceM / vMaxMs + delta;
            }

            if (distanceKm <= shortThreshold)
                ShortTimeCache[key] = result;

            return result;
        }
    }
}