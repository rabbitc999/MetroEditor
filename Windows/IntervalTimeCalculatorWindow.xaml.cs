using MetroEditor.PathFinding;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 区间运行时间计算器窗口
    /// 功能：基于 TrainTimeCalculator 的物理模型，可视化分析区间长度、限速、功率对运行时间的影响
    /// 核心图表：
    /// 1. 区间长度 vs 运行时间（验证线性模型 T = S/V + ΔT）
    /// 2. 限速 vs 运行时间（分析不同区间长度下的速度敏感性）
    /// 3. 功率 vs ΔT/加速时间（分析牵引性能对固定附加时间的影响）
    /// </summary>
    /// <remarks>
    /// 应用场景：
    /// - 校准物理模型参数（ΔT）
    /// - 分析不同编组和功率下的运行效率
    /// - 验证长区间运行时间的合理性
    /// </remarks>
    public partial class IntervalTimeCalculatorWindow : Window
    {
        // 默认列车参数（基于典型地铁车辆）
        private const string DefaultConsist = "6B";       // 6节B型车
        private const double DefaultPowerKw = 2000.0;    // 2000kW牵引功率
        private const double DefaultVMaxKmh = 80.0;     // 80km/h最高速度
        private const double DefaultBrakeDecel = 0.85;   // 0.85m/s²制动减速度

        public IntervalTimeCalculatorWindow()
        {
            InitializeComponent();
            LoadDefaultValues();
            // 首次打开时自动计算并显示图表
            CalcButton_Click(null, null);
        }

        /// <summary>
        /// 加载默认参数到输入框
        /// </summary>
        private void LoadDefaultValues()
        {
            ConsistBox.Text = DefaultConsist;
            PowerBox.Text = DefaultPowerKw.ToString("F0");
            SpeedBox.Text = DefaultVMaxKmh.ToString("F0");
            BrakeBox.Text = DefaultBrakeDecel.ToString("F2");
        }

        #region 计算与绘图
        /// <summary>
        /// 计算按钮点击事件：执行计算并更新所有图表
        /// </summary>
        private void CalcButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. 解析并验证输入参数
            if (!TryParseInputs(out string consist, out double powerKw, out double vMaxKmh, out double brakeDecel))
                return;

            // 2. 计算关键参数
            double deltaT = TrainTimeCalculator.GetDeltaT(consist, powerKw, vMaxKmh, brakeDecel);
            double accTime = CalculateAccelerationTime(consist, powerKw, vMaxKmh, brakeDecel);
            double accDist = CalculateAccelerationDistance(consist, powerKw, vMaxKmh, brakeDecel);

            // 显示文本结果
            ResultText.Text = $"ΔT = {deltaT:F2} s，加速时间 ≈ {accTime:F2} s（加速距离 ≈ {accDist:F2} m）";

            // 3. 绘制三个分析图表
            PlotLengthVsTime(consist, powerKw, vMaxKmh, brakeDecel);
            PlotSpeedVsTime(consist, powerKw, vMaxKmh, brakeDecel);
            PlotPowerVsDelta(consist, vMaxKmh, brakeDecel);
        }

        /// <summary>
        /// 解析输入参数并进行验证
        /// </summary>
        private bool TryParseInputs(out string consist, out double powerKw, out double vMaxKmh, out double brakeDecel)
        {
            consist = ConsistBox.Text.Trim();
            if (string.IsNullOrEmpty(consist)) consist = DefaultConsist;

            if (!double.TryParse(PowerBox.Text, out powerKw) || powerKw <= 0)
            {
                MessageBox.Show("功率必须为正数。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                brakeDecel = 0; vMaxKmh = 0;
                return false;
            }
            if (!double.TryParse(SpeedBox.Text, out vMaxKmh) || vMaxKmh <= 0)
            {
                MessageBox.Show("限速必须为正数。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                brakeDecel = 0;
                return false;
            }
            if (!double.TryParse(BrakeBox.Text, out brakeDecel) || brakeDecel <= 0)
            {
                MessageBox.Show("制动减速度必须为正数。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 估算加速时间（使用极短距离近似）
        /// </summary>
        private double CalculateAccelerationTime(string consist, double powerKw, double vMaxKmh, double brakeDecel)
        {
            const double tinyDistKm = 0.001; // 1米，足够短以近似纯加速过程
            return TrainTimeCalculator.ComputeSegmentTime(tinyDistKm, consist, vMaxKmh, powerKw, brakeDecel);
        }

        /// <summary>
        /// 估算加速距离（基于匀加速运动公式）
        /// </summary>
        private double CalculateAccelerationDistance(string consist, double powerKw, double vMaxKmh, double brakeDecel)
        {
            double t = CalculateAccelerationTime(consist, powerKw, vMaxKmh, brakeDecel);
            double vMs = vMaxKmh / 3.6; // km/h 转 m/s
            return 0.5 * vMs * t; // s = 0.5 * v * t
        }
        #endregion

        #region 图表绘制方法
        /// <summary>
        /// 绘制：区间长度 vs 运行时间
        /// 目的：验证线性模型 T = S/V + ΔT 的有效性
        /// </summary>
        private void PlotLengthVsTime(string consist, double powerKw, double vMaxKmh, double brakeDecel)
        {
            var plot = new PlotModel { Title = "区间长度 vs 运行时间" };
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "区间长度 (km)" });
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "运行时间 (s)" });

            // 计算0-180秒内对应的距离（使用二分法反向求解）
            var points = new List<DataPoint>();
            for (double t = 0; t <= 180; t += 2)
            {
                double s = FindDistanceForTime(t, consist, powerKw, vMaxKmh, brakeDecel);
                points.Add(new DataPoint(s, t));
            }

            var series = new LineSeries
            {
                Title = $"编组{consist}, P={powerKw}kW, Vmax={vMaxKmh}km/h",
                StrokeThickness = 2,
                Color = OxyColors.DarkBlue
            };
            foreach (var p in points) series.Points.Add(p);
            plot.Series.Add(series);

            // 添加ΔT标注
            double deltaT = TrainTimeCalculator.GetDeltaT(consist, powerKw, vMaxKmh, brakeDecel);
            plot.Annotations.Add(new OxyPlot.Annotations.TextAnnotation
            {
                Text = $"ΔT = {deltaT:F2}s",
                TextPosition = new DataPoint(2, deltaT + 5),
                FontSize = 12,
                TextColor = OxyColors.Red
            });

            _PlotLengthVsTime.Model = plot;
        }

        /// <summary>
        /// 二分法求解：给定目标时间，反向查找对应的区间长度
        /// </summary>
        private double FindDistanceForTime(double targetTime, string consist, double powerKw, double vMaxKmh, double brakeDecel)
        {
            double lo = 0, hi = 1000; // 上限100km
            for (int i = 0; i < 80; i++) // 80次迭代，精度足够
            {
                double mid = (lo + hi) / 2;
                double t = TrainTimeCalculator.ComputeSegmentTime(mid, consist, vMaxKmh, powerKw, brakeDecel);
                if (t < targetTime)
                    lo = mid;
                else
                    hi = mid;
            }
            return (lo + hi) / 2;
        }

        /// <summary>
        /// 绘制：限速 vs 运行时间（固定区间长度）
        /// 目的：分析速度限制对不同长度区间的影响程度
        /// </summary>
        private void PlotSpeedVsTime(string consist, double powerKw, double vMaxKmh, double brakeDecel)
        {
            var plot = new PlotModel { Title = "限速 vs 运行时间 (固定区间长度)" };
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "限速 (km/h)" });
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "运行时间 (s)" });

            // 分析四种典型区间长度
            double[] distances = { 2, 5, 10, 20 };
            foreach (var dist in distances)
            {
                var points = new List<DataPoint>();
                for (double v = 10; v <= 220; v += 5) // 覆盖地铁到城际速度范围
                {
                    double t = TrainTimeCalculator.ComputeSegmentTime(dist, consist, powerKw, v, brakeDecel);
                    points.Add(new DataPoint(v, t));
                }
                var series = new LineSeries
                {
                    Title = $"区间 {dist} km",
                    StrokeThickness = 2,
                };
                foreach (var p in points) series.Points.Add(p);
                // 分配不同颜色以便区分
                if (dist == 2) series.Color = OxyColors.Green;
                else if (dist == 5) series.Color = OxyColors.Blue;
                else if (dist == 10) series.Color = OxyColors.Orange;
                else if (dist == 20) series.Color = OxyColors.Red;
                plot.Series.Add(series);
            }

            _PlotSpeedVsTime.Model = plot;
        }

        /// <summary>
        /// 绘制：功率 vs ΔT/加速时间
        /// 目的：分析牵引功率对固定附加时间和加速性能的影响
        /// </summary>
        private void PlotPowerVsDelta(string consist, double vMaxKmh, double brakeDecel)
        {
            var plot = new PlotModel { Title = "ΔT / 加速时间 vs 功率" };
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "功率 (kW)" });
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "时间 (s)" });

            // 功率范围：100kW 到 10000kW（覆盖轻轨到高铁）
            var deltaPoints = new List<DataPoint>();
            var accPoints = new List<DataPoint>();
            for (double p = 100; p <= 10000; p += 100)
            {
                double delta = TrainTimeCalculator.GetDeltaT(consist, p, vMaxKmh, brakeDecel);
                deltaPoints.Add(new DataPoint(p, delta));

                // 加速时间估算（使用1米距离近似）
                double tinyDist = 0.001;
                double tAcc = TrainTimeCalculator.ComputeSegmentTime(tinyDist, consist, vMaxKmh, p, brakeDecel);
                accPoints.Add(new DataPoint(p, tAcc));
            }

            // ΔT 曲线
            var seriesDelta = new LineSeries
            {
                Title = "ΔT",
                StrokeThickness = 2,
                Color = OxyColors.DarkRed
            };
            foreach (var p in deltaPoints) seriesDelta.Points.Add(p);
            plot.Series.Add(seriesDelta);

            // 加速时间曲线（虚线）
            var seriesAcc = new LineSeries
            {
                Title = "加速时间 (近似)",
                StrokeThickness = 2,
                Color = OxyColors.DarkGreen,
                LineStyle = LineStyle.Dash
            };
            foreach (var p in accPoints) seriesAcc.Points.Add(p);
            plot.Series.Add(seriesAcc);

            _PlotPowerVsDelta.Model = plot;
        }
        #endregion
    }
}