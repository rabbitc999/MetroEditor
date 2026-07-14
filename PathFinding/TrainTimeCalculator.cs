using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.PathFinding
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
    public static class TrainTimeCalculator
    {
        #region 车型配置常量
        /// <summary>有效车型集合（A/B/C/L/D型）</summary>
        private static readonly HashSet<char> ValidCarTypes = new HashSet<char> { 'A', 'B', 'C', 'L', 'D' };

        /// <summary>每节车厢质量（吨），基于行业标准数据</summary>
        private static readonly Dictionary<char, double> MassPerCar = new Dictionary<char, double>
        {
            {'A', 36.5},  // A型车：36.5吨/节（宽体大运量）
            {'B', 33.3},  // B型车：33.3吨/节（标准地铁）
            {'L', 30.0},  // L型车：30.0吨/节（直线电机）
            {'C', 34.0},  // C型车：34.0吨/节（轻轨）
            {'D', 37.5}   // D型车：37.5吨/节（大轴重货运）
        };

        /// <summary>计算结果缓存：ΔT（固定附加时间）</summary>
        private static readonly Dictionary<string, double> _deltaCache = new();

        /// <summary>短区间时间缓存（≤1.5km）</summary>
        private static readonly Dictionary<string, double> _shortTimeCache = new();
        #endregion

        #region 戴维斯阻力系数（原始值）
        /// <summary>
        /// 戴维斯阻力系数表（a, b, c）
        /// 公式：R = (a + b·v + c·v²) × 重量 × 重力加速度
        /// 其中：
        /// - a：机械摩擦系数（与速度无关）
        /// - b：线性阻力系数（轮轨滑动）
        /// - c：空气动力学系数（与速度平方成正比）
        /// 数据来源：美国铁路工程协会（AREA）标准
        /// </summary>
        private static readonly Dictionary<char, (double a, double b, double c)> DavisCoeffRaw =
            new Dictionary<char, (double, double, double)>
            {
                {'A', (2.0895, 0.0098, 0.006)},   // A型车：大断面，空气阻力显著
                {'B', (1.65,   0.0247, 0.00128)}, // B型车：标准断面
                {'L', (1.87,   0.0196, 0.00102)}, // L型车：直线电机，低阻力
                {'C', (1.80,   0.0200, 0.0015)},  // C型车：轻轨，中等阻力
                {'D', (2.3,    0.012,  0.007)}    // D型车：大轴重，高阻力
            };

        /// <summary>戴维斯系数放大因子（保守估计）</summary>
        private const double DavisScale = 1.2;

        /// <summary>重力加速度（m/s²）</summary>
        private const double Gravity = 9.81;
        #endregion

        #region 编组解析
        /// <summary>
        /// 解析编组字符串，提取车型和车厢数量
        /// </summary>
        /// <param name="consist">编组字符串（如"6A"、"8B"、"4L"等）</param>
        /// <returns>（车型字符，车厢数量）</returns>
        /// <remarks>
        /// 解析规则：
        /// 1. 数字部分：取所有数字的最大值（如"6A"→6，"4+4重联"→8）
        /// 2. 字母部分：取第一个字母，无效字母默认为'B'
        /// 3. 空输入或无效输入返回默认值（6B）
        /// </remarks>
        public static (char CarType, int CarCount) ParseConsist(string consist)
        {
            if (string.IsNullOrEmpty(consist)) return ('B', 6);

            // 提取数字部分（取最大值，支持重联编组）
            var numbers = System.Text.RegularExpressions.Regex.Matches(consist, @"\d+")
                        .Select(m => int.Parse(m.Value)).ToList();
            int carCount = numbers.Any() ? numbers.Max() : 6;

            // 提取字母部分（取第一个有效车型字母）
            char rawType = System.Text.RegularExpressions.Regex.Match(consist, @"[A-Za-z]").Value.FirstOrDefault();
            char carType = ValidCarTypes.Contains(rawType) ? char.ToUpper(rawType) : 'B';

            // 安全限制：车厢数不超过12节
            if (carCount > 12) carCount = 6;

            return (carType, carCount);
        }
        #endregion

        #region 阻力函数生成
        /// <summary>
        /// 生成阻力函数 R(v)：阻力随速度变化的函数
        /// </summary>
        /// <param name="carType">车型字符</param>
        /// <param name="massTon">列车总质量（吨）</param>
        /// <returns>阻力函数 R(v)，单位为牛顿(N)</returns>
        /// <remarks>
        /// 阻力函数形式：R(v) = (a + b·v + c·v²) × 重量(kN)
        /// 其中重量 = 质量(吨) × 重力加速度(9.81 m/s²)
        /// 函数返回值单位为牛顿(N)，可直接用于力学计算
        /// </remarks>
        private static Func<double, double> GetResistanceFunc(char carType, double massTon)
        {
            var coeff = DavisCoeffRaw[carType];
            // 应用放大系数，使结果偏保守（安全侧）
            double a = coeff.a * DavisScale;
            double b = coeff.b * DavisScale;
            double c = coeff.c * DavisScale;
            // 重量转换为千牛(kN)：1吨 = 9.81 kN
            double weightKN = massTon * Gravity;
            // 返回阻力函数
            return v => (a + b * v + c * v * v) * weightKN;
        }
        #endregion

        #region 功率限制速度求解
        /// <summary>
        /// 求解功率限制下的最大速度
        /// </summary>
        /// <param name="powerW">牵引功率（瓦特）</param>
        /// <param name="R">阻力函数 R(v)</param>
        /// <returns>功率限制速度（m/s）</returns>
        /// <remarks>
        /// 物理原理：当牵引力等于阻力时，达到功率限制速度
        /// 即：P = F·v = R(v)·v → v·R(v) = P
        /// 本方法在[0.1, 100] m/s范围内搜索满足条件的最小速度
        /// </remarks>
        private static double FindPowerLimitSpeed(double powerW, Func<double, double> R)
        {
            // 步长0.05 m/s进行搜索，平衡精度和性能
            for (double v = 0.1; v <= 100; v += 0.05)
            {
                if (v * R(v) > powerW)
                    return v;
            }
            return 50.0; // 搜索失败时的回退值
        }
        #endregion

        #region 数值积分（加速段）
        /// <summary>
        /// 计算加速段的时间和距离（数值积分）
        /// </summary>
        /// <param name="vTarget">目标速度（m/s）</param>
        /// <param name="massKg">列车质量（kg）</param>
        /// <param name="powerW">牵引功率（W）</param>
        /// <param name="R">阻力函数 R(v)</param>
        /// <returns>（加速时间，加速距离）</returns>
        /// <remarks>
        /// 物理原理：
        /// 1. 运动方程：m·dv/dt = (P/v) - R(v)
        /// 2. 变形得：dt/dv = m·v / (P - v·R(v))
        /// 3. 距离关系：ds/dv = v·dt/dv = m·v² / (P - v·R(v))
        /// 4. 积分求解：t = ∫dt/dv dv, s = ∫ds/dv dv
        /// 
        /// 数值方法：自适应辛普森积分，精度高且稳定
        /// </remarks>
        public static (double time, double dist) CalcAcceleration(double vTarget, double massKg, double powerW, Func<double, double> R)
        {
            if (vTarget <= 0) return (0, 0);

            // 使用99%目标速度，避免分母为零的数值不稳定
            double vEff = vTarget * 0.99;

            // 定义被积函数：dt/dv
            Func<double, double> integrandT = v =>
            {
                double denominator = powerW - v * R(v);
                return denominator > 1e-6 ? massKg * v / denominator : 0;
            };

            // 定义被积函数：ds/dv
            Func<double, double> integrandS = v =>
            {
                double denominator = powerW - v * R(v);
                return denominator > 1e-6 ? massKg * v * v / denominator : 0;
            };

            // 使用自适应辛普森积分
            double t = AdaptiveSimpson(integrandT, 0, vEff, 1e-6, 100);
            double s = AdaptiveSimpson(integrandS, 0, vEff, 1e-6, 100);
            return (t, s);
        }

        /// <summary>
        /// 自适应辛普森积分（主入口）
        /// </summary>
        /// <param name="f">被积函数</param>
        /// <param name="a">积分下限</param>
        /// <param name="b">积分上限</param>
        /// <param name="eps">精度要求</param>
        /// <param name="maxDepth">最大递归深度</param>
        /// <returns>积分近似值</returns>
        private static double AdaptiveSimpson(Func<double, double> f, double a, double b, double eps, int maxDepth)
        {
            if (a > b) { var temp = a; a = b; b = temp; }
            if (a == b) return 0.0;

            double mid = (a + b) / 2;
            double fa = f(a), fm = f(mid), fb = f(b);
            // 辛普森公式：∫≈(b-a)/6×(f(a)+4f(m)+f(b))
            double whole = (b - a) * (fa + 4 * fm + fb) / 6.0;

            return AdaptiveSimpsonRecursive(f, a, b, fa, fm, fb, whole, eps, maxDepth, 0);
        }

        /// <summary>
        /// 自适应辛普森积分（递归实现）
        /// </summary>
        private static double AdaptiveSimpsonRecursive(Func<double, double> f, double a, double b,
                                                       double fa, double fm, double fb,
                                                       double whole, double eps, int maxDepth, int depth)
        {
            double mid = (a + b) / 2;
            double leftMid = (a + mid) / 2;
            double rightMid = (mid + b) / 2;
            double fLeftMid = f(leftMid);
            double fRightMid = f(rightMid);

            // 左右子区间的辛普森近似
            double left = (mid - a) * (fa + 4 * fLeftMid + fm) / 6.0;
            double right = (b - mid) * (fm + 4 * fRightMid + fb) / 6.0;

            // 误差估计：|left+right - whole|
            double diff = left + right - whole;
            if (depth >= maxDepth || Math.Abs(diff) < 15 * eps)
            {
                // 达到精度要求，返回改进的近似结果
                return left + right + diff / 15.0;
            }

            // 递归细分，提高精度
            double eps2 = eps / 2.0;
            return AdaptiveSimpsonRecursive(f, a, mid, fa, fLeftMid, fm, left, eps2, maxDepth, depth + 1)
                 + AdaptiveSimpsonRecursive(f, mid, b, fm, fRightMid, fb, right, eps2, maxDepth, depth + 1);
        }
        #endregion

        #region 核心运行时间计算（精确模式）
        /// <summary>
        /// 计算区间运行时间（精确数值积分模式）
        /// </summary>
        /// <param name="distanceM">区间距离（米）</param>
        /// <param name="consist">编组字符串</param>
        /// <param name="vMaxKmh">最高速度（km/h）</param>
        /// <param name="powerKw">牵引功率（kW）</param>
        /// <param name="brakeDecel">制动减速度（m/s²）</param>
        /// <returns>运行时间（秒）</returns>
        /// <remarks>
        /// 运行过程分为三个阶段：
        /// 1. 加速段：从静止加速到巡航速度
        /// 2. 巡航段（可选）：匀速运行（当距离足够时）
        /// 3. 制动段：从巡航速度减速到零
        /// 
        /// 若距离不足以达到巡航速度，则采用"菱形"速度曲线：
        /// 加速到某个峰值速度后立即制动
        /// </remarks>
        public static double ComputeRunTimeExact(double distanceM, string consist, double vMaxKmh, double powerKw, double brakeDecel = 0.85)
        {
            // 解析编组
            var (carType, carCount) = ParseConsist(consist);
            double massTon = MassPerCar[carType] * carCount;
            double massKg = massTon * 1000;
            double powerW = powerKw * 1000;
            double vMax = vMaxKmh / 3.6;  // km/h → m/s

            // 生成阻力函数
            var R = GetResistanceFunc(carType, massTon);

            // 计算功率限制速度（考虑阻力影响）
            double vLimit = FindPowerLimitSpeed(powerW, R);
            double vReach = Math.Min(vMax, vLimit * 0.99);  // 留有余量

            // 计算加速段
            var (tAcc, sAcc) = CalcAcceleration(vReach, massKg, powerW, R);

            // 计算制动段（匀减速运动）
            double tBrake = vReach / brakeDecel;
            double sBrake = vReach * vReach / (2 * brakeDecel);

            // 判断是否能达到巡航速度
            if (sAcc + sBrake <= distanceM)
            {
                // 三段式：加速 + 巡航 + 制动
                double sCruise = distanceM - sAcc - sBrake;
                double tCruise = sCruise / vReach;
                return tAcc + tCruise + tBrake;
            }
            else
            {
                // 两段式：加速后立即制动（未达到巡航速度）
                // 使用二分法求解峰值速度
                double low = 0, high = vReach;
                for (int i = 0; i < 60; i++)
                {
                    double mid = (low + high) / 2;
                    var (tAccMid, sAccMid) = CalcAcceleration(mid, massKg, powerW, R);
                    double totalDist = sAccMid + mid * mid / (2 * brakeDecel);
                    if (totalDist < distanceM)
                        low = mid;
                    else
                        high = mid;
                }
                double vPeak = (low + high) / 2;
                var (tAccPeak, _) = CalcAcceleration(vPeak, massKg, powerW, R);
                double tBrakePeak = vPeak / brakeDecel;
                return tAccPeak + tBrakePeak;
            }
        }
        #endregion

        #region 固定附加时间（ΔT）缓存
        /// <summary>
        /// 获取固定附加时间 ΔT（秒）
        /// </summary>
        /// <param name="consist">编组字符串</param>
        /// <param name="powerKw">牵引功率（kW）</param>
        /// <param name="vMaxKmh">最高速度（km/h）</param>
        /// <param name="brakeDecel">制动减速度（m/s²）</param>
        /// <returns>固定附加时间 ΔT（秒）</returns>
        /// <remarks>
        /// ΔT的定义：在长距离（10km）运行中，实际运行时间与理想匀速运行时间的差值
        /// 公式：ΔT = t_ref - (L_ref / v_max)
        /// 物理意义：包含了加速、减速和阻力影响的等效时间损失
        /// 
        /// 缓存策略：相同编组、功率、限速和制动条件下的ΔT相同
        /// </remarks>
        public static double GetDeltaT(string consist, double powerKw, double vMaxKmh, double brakeDecel)
        {
            string key = $"{consist}|{powerKw:F0}|{vMaxKmh:F0}|{brakeDecel:F2}";
            if (_deltaCache.TryGetValue(key, out double delta))
                return delta;

            const double ReferenceDistance = 10000.0; // 10 km参考距离
            double tRef = ComputeRunTimeExact(ReferenceDistance, consist, vMaxKmh, powerKw, brakeDecel);
            double vMaxMs = vMaxKmh / 3.6;
            delta = tRef - ReferenceDistance / vMaxMs;
            _deltaCache[key] = delta;
            return delta;
        }
        #endregion

        #region 最终对外接口
        /// <summary>
        /// 计算一个区间（方向段）的运行时间
        /// </summary>
        /// <param name="distanceKm">区间距离（km）</param>
        /// <param name="consist">编组字符串</param>
        /// <param name="vMaxKmh">最高速度（km/h）</param>
        /// <param name="powerKw">牵引功率（kW）</param>
        /// <param name="brakeDecel">制动减速度（m/s²）</param>
        /// <returns>运行时间（秒）</returns>
        /// <remarks>
        /// 智能计算策略：
        /// 1. 短区间（≤1.5km）：使用精确数值积分（精度优先）
        /// 2. 长区间（＞1.5km）：使用线性近似 + ΔT修正（性能优先）
        /// 
        /// 线性近似公式：t = L/v_max + ΔT
        /// 其中ΔT为固定附加时间，通过缓存机制高效获取
        /// 
        /// 缓存策略：
        /// - 短区间：缓存精确计算结果
        /// - 长区间：ΔT缓存，不缓存最终结果（因距离可变）
        /// </remarks>
        public static double ComputeSegmentTime(double distanceKm, string consist, double vMaxKmh, double powerKw, double brakeDecel = 0.85)
        {
            if (distanceKm <= 0) return 0;
            if (brakeDecel <= 0) brakeDecel = 0.85; // 默认值保护

            // 短区间缓存：距离舍入到0.01米精度
            double roundedDist = Math.Round(distanceKm, 5, MidpointRounding.AwayFromZero);
            string cacheKey = string.Format(CultureInfo.InvariantCulture,
                                           "{0:F5}|{1}|{2:F0}|{3:F0}|{4:F2}",
                                           roundedDist, consist, powerKw, vMaxKmh, brakeDecel);

            // 检查短区间缓存
            if (_shortTimeCache.TryGetValue(cacheKey, out double cached))
                return cached;

            double distanceM = distanceKm * 1000;
            const double ShortThreshold = 1.5; // 短区间阈值（km）

            double result;
            if (distanceKm <= ShortThreshold)
            {
                // 短区间：精确计算
                result = ComputeRunTimeExact(distanceM, consist, vMaxKmh, powerKw, brakeDecel);
                _shortTimeCache[cacheKey] = result; // 缓存结果
            }
            else
            {
                // 长区间：线性近似 + ΔT修正
                double vMaxMs = vMaxKmh / 3.6;
                double delta = GetDeltaT(consist, powerKw, vMaxKmh, brakeDecel);
                result = distanceM / vMaxMs + delta;
                // 长区间不缓存最终结果（距离变化频繁）
            }

            return result;
        }
        #endregion
    }
}