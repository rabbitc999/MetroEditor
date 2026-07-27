using MetroEditor.Simulator.Utils;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 区间运行时间计算器窗口
    /// 功能：基于 SimTrainTimeCalculator 的物理模型，可视化分析区间长度、限速、功率对运行时间的影响
    /// 核心图表：
    /// 1. 区间长度 vs 运行时间（验证线性模型 T = S/V + ΔT）
    /// 2. 限速 vs 运行时间（分析不同区间长度下的速度敏感性）
    /// 3. 功率 vs ΔT/加速时间（分析牵引性能对固定附加时间的影响）
    /// </summary>
    public partial class IntervalTimeCalculatorWindow : Window
    {
        // 默认列车参数（基于典型地铁车辆）
        private const string DefaultConsist = "6B";
        private const double DefaultPowerKw = 2000.0;
        private const double DefaultVMaxKmh = 80.0;
        private const double DefaultBrakeDecel = 0.85;

        // 取消令牌源，用于取消正在进行的计算
        private CancellationTokenSource? _cts;

        public IntervalTimeCalculatorWindow()
        {
            InitializeComponent();
            LoadDefaultValues();
            // 首次打开时自动计算（异步执行）
            CalcButton_Click(null, null);
            Loaded += (o, e) => Topmost = false;
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

        #region 计算与绘图（异步、可取消）

        /// <summary>
        /// 计算按钮点击事件：取消旧任务，启动新异步计算
        /// </summary>
        private async void CalcButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. 取消正在进行的旧任务
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // 2. 解析并验证输入参数
            if (!TryParseInputs(out string consist, out double powerKw, out double vMaxKmh, out double brakeDecel))
                return;

            // 3. UI 进入计算状态
            CalcButton.IsEnabled = false;
            ResultText.Text = "计算中，请稍候...";
            ResultText.Foreground = System.Windows.Media.Brushes.Blue;

            try
            {
                // 4. 在后台线程执行计算
                var result = await Task.Run(() => ComputeAll(consist, powerKw, vMaxKmh, brakeDecel, token), token);

                // 5. 更新 UI（必须在主线程）
                await Dispatcher.InvokeAsync(() =>
                {
                    // 显示文本结果
                    ResultText.Text = $"ΔT = {result.DeltaT:F2} s，加速时间 ≈ {result.AccTime:F2} s（加速距离 ≈ {result.AccDist:F2} m）";
                    ResultText.Foreground = System.Windows.Media.Brushes.Black;

                    // 更新三个图表
                    _PlotLengthVsTime.Model = result.PlotLengthVsTime;
                    _PlotSpeedVsTime.Model = result.PlotSpeedVsTime;
                    _PlotPowerVsDelta.Model = result.PlotPowerVsDelta;

                    // 刷新显示
                    _PlotLengthVsTime.InvalidatePlot(true);
                    _PlotSpeedVsTime.InvalidatePlot(true);
                    _PlotPowerVsDelta.InvalidatePlot(true);
                });
            }
            catch (OperationCanceledException)
            {
                // 任务被取消，不报错
                await Dispatcher.InvokeAsync(() =>
                {
                    ResultText.Text = "计算已取消";
                    ResultText.Foreground = System.Windows.Media.Brushes.Gray;
                });
            }
            catch (Exception ex)
            {
                // 其他异常
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"计算出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    ResultText.Text = "计算失败";
                    ResultText.Foreground = System.Windows.Media.Brushes.Red;
                });
            }
            finally
            {
                // 恢复 UI 状态
                await Dispatcher.InvokeAsync(() =>
                {
                    CalcButton.IsEnabled = true;
                });
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// 封装所有计算逻辑，供后台线程调用
        /// </summary>
        private ComputationResult ComputeAll(string consist, double powerKw, double vMaxKmh, double brakeDecel, CancellationToken ct)
        {
            // 1. 计算关键参数
            double deltaT = SimTrainTimeUtils.GetDeltaT(consist, powerKw, vMaxKmh, brakeDecel);
            double accTime = CalculateAccelerationTime(consist, powerKw, vMaxKmh, brakeDecel);
            double accDist = CalculateAccelerationDistance(consist, powerKw, vMaxKmh, brakeDecel);

            // 2. 绘制三个分析图表（在后台线程创建 PlotModel）
            var plotLength = PlotLengthVsTime(consist, powerKw, vMaxKmh, brakeDecel, ct);
            var plotSpeed = PlotSpeedVsTime(consist, powerKw, vMaxKmh, brakeDecel, ct);
            var plotPower = PlotPowerVsDelta(consist, vMaxKmh, brakeDecel, ct);

            return new ComputationResult
            {
                DeltaT = deltaT,
                AccTime = accTime,
                AccDist = accDist,
                PlotLengthVsTime = plotLength,
                PlotSpeedVsTime = plotSpeed,
                PlotPowerVsDelta = plotPower
            };
        }

        /// <summary>
        /// 估算加速时间（使用极短距离近似）
        /// </summary>
        private double CalculateAccelerationTime(string consist, double powerKw, double vMaxKmh, double brakeDecel)
        {
            const double tinyDistKm = 0.001; // 1米
            return SimTrainTimeUtils.ComputeSegmentTime(tinyDistKm, consist, vMaxKmh, powerKw, brakeDecel);
        }

        /// <summary>
        /// 估算加速距离（基于匀加速运动公式）
        /// </summary>
        private double CalculateAccelerationDistance(string consist, double powerKw, double vMaxKmh, double brakeDecel)
        {
            double t = CalculateAccelerationTime(consist, powerKw, vMaxKmh, brakeDecel);
            double vMs = vMaxKmh / 3.6;
            return 0.5 * vMs * t;
        }

        #endregion

        #region 图表绘制方法（可取消）

        /// <summary>
        /// 绘制：区间长度 vs 运行时间（支持取消）
        /// </summary>
        private PlotModel PlotLengthVsTime(string consist, double powerKw, double vMaxKmh, double brakeDecel, CancellationToken ct)
        {
            var plot = new PlotModel { Title = "区间长度 vs 运行时间" };
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "区间长度 (km)" });
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "运行时间 (s)" });

            var points = new List<DataPoint>();
            // 计算0-180秒内对应的距离（每次迭代检查取消）
            for (double t = 0; t <= 180; t += 2)
            {
                ct.ThrowIfCancellationRequested();
                double s = FindDistanceForTime(t, consist, powerKw, vMaxKmh, brakeDecel, ct);
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

            double deltaT = SimTrainTimeUtils.GetDeltaT(consist, powerKw, vMaxKmh, brakeDecel);
            plot.Annotations.Add(new OxyPlot.Annotations.TextAnnotation
            {
                Text = $"ΔT = {deltaT:F2}s",
                TextPosition = new DataPoint(2, deltaT + 5),
                FontSize = 12,
                TextColor = OxyColors.Red
            });

            return plot;
        }

        /// <summary>
        /// 二分法求解：给定目标时间，反向查找对应的区间长度（支持取消）
        /// </summary>
        private double FindDistanceForTime(double targetTime, string consist, double powerKw, double vMaxKmh, double brakeDecel, CancellationToken ct)
        {
            double lo = 0, hi = 1000;
            for (int i = 0; i < 80; i++)
            {
                ct.ThrowIfCancellationRequested();
                double mid = (lo + hi) / 2;
                double t = SimTrainTimeUtils.ComputeSegmentTime(mid, consist, vMaxKmh, powerKw, brakeDecel);
                if (t < targetTime)
                    lo = mid;
                else
                    hi = mid;
            }
            return (lo + hi) / 2;
        }

        /// <summary>
        /// 绘制：限速 vs 运行时间（固定区间长度，支持取消）
        /// </summary>
        private PlotModel PlotSpeedVsTime(string consist, double powerKw, double vMaxKmh, double brakeDecel, CancellationToken ct)
        {
            var plot = new PlotModel { Title = "限速 vs 运行时间 (固定区间长度)" };
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "限速 (km/h)" });
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "运行时间 (s)" });

            double[] distances = { 2, 5, 10, 20 };
            OxyColor[] colors = { OxyColors.Green, OxyColors.Blue, OxyColors.Orange, OxyColors.Red };
            for (int idx = 0; idx < distances.Length; idx++)
            {
                ct.ThrowIfCancellationRequested();
                double dist = distances[idx];
                var points = new List<DataPoint>();
                for (double v = 10; v <= 220; v += 5)
                {
                    ct.ThrowIfCancellationRequested();
                    double t = SimTrainTimeUtils.ComputeSegmentTime(dist, consist, v, powerKw, brakeDecel);
                    points.Add(new DataPoint(v, t));
                }
                var series = new LineSeries
                {
                    Title = $"区间 {dist} km",
                    StrokeThickness = 2,
                    Color = colors[idx]
                };
                foreach (var p in points) series.Points.Add(p);
                plot.Series.Add(series);
            }

            return plot;
        }

        /// <summary>
        /// 绘制：功率 vs ΔT/加速时间（支持取消）
        /// </summary>
        private PlotModel PlotPowerVsDelta(string consist, double vMaxKmh, double brakeDecel, CancellationToken ct)
        {
            var plot = new PlotModel { Title = "ΔT / 加速时间 vs 功率" };
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "功率 (kW)" });
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "时间 (s)" });

            var deltaPoints = new List<DataPoint>();
            var accPoints = new List<DataPoint>();
            for (double p = 100; p <= 10000; p += 100)
            {
                ct.ThrowIfCancellationRequested();
                double delta = SimTrainTimeUtils.GetDeltaT(consist, p, vMaxKmh, brakeDecel);
                deltaPoints.Add(new DataPoint(p, delta));

                double tinyDist = 0.001;
                double tAcc = SimTrainTimeUtils.ComputeSegmentTime(tinyDist, consist, vMaxKmh, p, brakeDecel);
                accPoints.Add(new DataPoint(p, tAcc));
            }

            var seriesDelta = new LineSeries
            {
                Title = "ΔT",
                StrokeThickness = 2,
                Color = OxyColors.DarkRed
            };
            foreach (var p in deltaPoints) seriesDelta.Points.Add(p);
            plot.Series.Add(seriesDelta);

            var seriesAcc = new LineSeries
            {
                Title = "加速时间 (近似)",
                StrokeThickness = 2,
                Color = OxyColors.DarkGreen,
                LineStyle = LineStyle.Dash
            };
            foreach (var p in accPoints) seriesAcc.Points.Add(p);
            plot.Series.Add(seriesAcc);

            return plot;
        }

        #endregion

        #region 输入解析

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

        #endregion

        #region 辅助结构

        private class ComputationResult
        {
            public double DeltaT { get; set; }
            public double AccTime { get; set; }
            public double AccDist { get; set; }
            public PlotModel? PlotLengthVsTime { get; set; }
            public PlotModel? PlotSpeedVsTime { get; set; }
            public PlotModel? PlotPowerVsDelta { get; set; }
        }

        #endregion
    }
}