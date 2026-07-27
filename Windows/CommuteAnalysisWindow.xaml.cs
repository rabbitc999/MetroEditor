using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.Simulator.Algorithms;
using MetroEditor.Simulator.Data;
using MetroEditor.Simulator.Models;
using MetroEditor.Simulator.Utils;
using Microsoft.Win32;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 通勤分析工具窗口
    /// 功能：以指定站点为起点，计算到全网所有站点的通勤指标（距离、时间、票价、换乘次数等）
    /// 应用场景：
    /// - 居住选址分析（家到公司的通勤时间）
    /// - 城市规划评估（职住平衡分析）
    /// - 路网可达性研究
    /// </summary>
    public partial class CommuteAnalysisWindow : Window
    {
        private readonly ParsedData _data;
        private readonly SimMetroGraph _graph;              // 路网图（用于路径计算）
        private readonly SimDataCache _cache;
        private readonly List<string> _allStations;      // 所有站点列表
        private ObservableCollection<CommuteResult> _results = new(); // 分析结果（绑定到DataGrid）

        public CommuteAnalysisWindow(JsonRoot data)
        {
            InitializeComponent();
            _cache = SimDataParser.Parse(data);
            _graph = new SimMetroGraph(_cache);
            _allStations = SimStationUtils.GetActiveStationList(_cache);

            // 绑定数据源
            StartStationCombo.ItemsSource = _allStations;
            ResultGrid.ItemsSource = _results;

            // 右键复制站名功能
            ResultGrid.PreviewMouseRightButtonDown += ResultGrid_PreviewMouseRightButtonDown;
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 右键点击行复制站名
        /// </summary>
        private void ResultGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var row = ItemsControl.ContainerFromElement(ResultGrid, e.OriginalSource as DependencyObject) as DataGridRow;
            if (row != null && row.Item is CommuteResult item)
            {
                Clipboard.SetText(item.Station);
                StatusText.Text = $"已复制站名：{item.Station}";
            }
        }

        /// <summary>
        /// 开始计算按钮点击事件（异步执行）
        /// </summary>
        private async void CalcButton_Click(object sender, RoutedEventArgs e)
        {
            string start = StartStationCombo.Text?.Trim();
            if (string.IsNullOrEmpty(start) || !_allStations.Contains(start))
            {
                MessageBox.Show("请选择有效的起点站", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 禁用UI控件，防止重复点击
            CalcButton.IsEnabled = false;
            ExportCsvButton.IsEnabled = false;
            StatusText.Text = "计算中...";
            _results.Clear();

            // 从注册表加载用户偏好的搜索权重
            double distW = ReadWeight(DistWeightBox, 0);
            double transW = ReadWeight(TransferWeightBox, 0);
            double moneyW = ReadWeight(MoneyWeightBox, 0);
            double timeW = ReadWeight(TimeWeightBox, 1);
            double stationW = ReadWeight(StationWeightBox, 0);
            double crowdW = ReadWeight(CrowdWeightBox, 0);

            var weight = new SimSearchWeight(distW, transW, moneyW, timeW, stationW, crowdW);

            // 在后台线程执行计算密集型任务
            var rows = await Task.Run(() =>
            {
                var list = new List<CommuteResult>();
                int total = _allStations.Count - 1; // 排除起点自身
                int processed = 0;

                foreach (var station in _allStations)
                {
                    if (station == start) continue;

                    // 执行路径搜索
                    var result = _graph.FindTransferPath(start, station, weight);
                    if (result.Transfers == -1) continue; // 不可达则跳过

                    var path = result.Path;
                    double distance = _graph.ComputePathDistance(path);
                    DateTime monday8am = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
                    long timestamp = new DateTimeOffset(monday8am).ToUnixTimeMilliseconds();
                    double timeSec = SimTimeCalculator.ComputeTotalTime(_cache, path, _graph, weight, timestamp);
                    int stationCount = path.Select(n => SimGraphKeyUtils.ParseNode(n)[1]).Distinct().Count() - 1;
                    string routeDesc = _graph.FormatRouteWithDirection(path);

                    list.Add(new CommuteResult
                    {
                        Station = station,
                        Distance = distance,
                        Transfers = result.Transfers,
                        Time = timeSec / 60.0, // 转换为分钟
                        Fare = result.TotalMoney,
                        Value = result.Cost,   // 综合代价
                        RouteDesc = routeDesc,
                        StationCount = stationCount
                    });

                    processed++;
                    // 更新UI进度（回到UI线程执行）
                    Dispatcher.Invoke(() => StatusText.Text = $"计算进度 {processed}/{total}");
                }
                // 按综合代价排序（从小到大）
                return list.OrderBy(r => r.Value).ToList();
            });

            // 填充结果并恢复UI状态
            int idx = 1;
            foreach (var row in rows)
            {
                row.Index = idx++;
                _results.Add(row);
            }

            StatusText.Text = $"完成，共 {_results.Count} 个可达站点";
            CalcButton.IsEnabled = true;
            ExportCsvButton.IsEnabled = _results.Count > 0;
        }

        // 添加辅助方法
        private double ReadWeight(TextBox box, double defaultValue)
        {
            if (double.TryParse(box.Text, out double val))
                return val;
            return defaultValue;
        }

        /// <summary>
        /// 导出CSV文件
        /// </summary>
        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (_results.Count == 0) return;

            var dialog = new SaveFileDialog
            {
                Filter = "CSV文件 (*.csv)|*.csv",
                FileName = $"通勤分析_{StartStationCombo.Text.Trim()}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };
            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                // CSV表头
                sb.AppendLine("序号,站点,距离(km),换乘次数,时间(分钟),站数,票价(元),综合代价,乘车指引");
                string start = StartStationCombo.Text.Trim();
                sb.AppendLine($"0,{start},0,0,0,0,0,0,起点");
                // CSV数据行
                foreach (var row in _results)
                {
                    sb.AppendLine($"{row.Index},{row.Station},{row.Distance:F3},{row.Transfers},{row.Time:F1},{row.StationCount},{row.Fare:F2},{row.Value:F3},\"{row.RouteDesc}\"");
                }
                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);

                // 提取综合代价列表并排序
                var values = _results.Select(r => r.Value).OrderBy(v => v).ToList();
                values.Sort();
                int n = values.Count;
                double Q1 = Percentile(values, 0.25);
                double Q2 = Percentile(values, 0.50);
                double Q3 = Percentile(values, 0.75);

                // 生成 MD 内容
                var md = new StringBuilder();
                md.AppendLine("# 通勤分析综合代价四分位数报告");
                md.AppendLine($"- **起点站**：{start}");
                md.AppendLine($"- **计算时间**：{DateTime.Now:yyyy-MM-dd HH:mm}");
                md.AppendLine($"- **可达站点总数**：{n}");
                md.AppendLine();
                md.AppendLine("## 综合代价四分位数");
                md.AppendLine("| 分位 | 数值 |");
                md.AppendLine("| :--- | ---: |");
                md.AppendLine($"| Q1 (25%) | {Q1:F2} |");
                md.AppendLine($"| Q2 (50%) | {Q2:F2} |");
                md.AppendLine($"| Q3 (75%) | {Q3:F2} |");
                md.AppendLine();
                md.AppendLine("## 通勤代价等级划分");
                md.AppendLine("| 等级 | 代价范围 | 建议 |");
                md.AppendLine("| :--- | :--- | :--- |");
                md.AppendLine($"| 🟢 低代价 | ≤ {Q1:F2} | 首选通勤圈 |");
                md.AppendLine($"| 🟡 中等偏低 | {Q1:F2} ~ {Q2:F2} | 可接受 |");
                md.AppendLine($"| 🟠 中等偏高 | {Q2:F2} ~ {Q3:F2} | 需权衡 |");
                md.AppendLine($"| 🔴 高代价 | > {Q3:F2} | 谨慎考虑 |");

                // 写入 .md 文件（与 CSV 同目录）
                string mdPath = Path.ChangeExtension(dialog.FileName, ".md");
                File.WriteAllText(mdPath, md.ToString(), Encoding.UTF8);

                MessageBox.Show($"导出成功：{dialog.FileName}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private double Percentile(List<double> sortedValues, double percentile)
        {
            int N = sortedValues.Count;
            double index = (N - 1) * percentile;
            int left = (int)Math.Floor(index);
            int right = (int)Math.Ceiling(index);
            if (left == right) return sortedValues[left];
            double frac = index - left;
            return sortedValues[left] * (1 - frac) + sortedValues[right] * frac;
        }

        /// <summary>
        /// ComboBox 获得焦点时自动展开下拉列表
        /// </summary>
        private void ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.IsDropDownOpen = true;
            }
        }

        private async void ExportAllMatrixBtn_Click(object sender, RoutedEventArgs e)
        {
            // 弹出保存对话框，让用户选择保存位置
            var dialog = new SaveFileDialog
            {
                Filter = "CSV文件 (*.csv)|*.csv",
                FileName = $"全站点距离矩阵_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dialog.ShowDialog() != true)
                return; // 用户取消，不执行计算

            ExportAllMatrixBtn.IsEnabled = false;
            StatusText.Text = "正在计算全站点最短路径...";

            // 从界面读取权重
            double distW = ReadWeight(DistWeightBox, 0);
            double transW = ReadWeight(TransferWeightBox, 0);
            double moneyW = ReadWeight(MoneyWeightBox, 0);
            double timeW = ReadWeight(TimeWeightBox, 1);
            double stationW = ReadWeight(StationWeightBox, 0);
            double crowdW = ReadWeight(CrowdWeightBox, 0);

            try
            {
                // 传入用户选择的文件路径
                await ExportAllPairsToCsvAsync(dialog.FileName, distW, transW, moneyW, timeW, stationW, crowdW);
                StatusText.Text = $"全矩阵导出完成，文件已保存至 {dialog.FileName}";
                MessageBox.Show($"全矩阵已导出至：\n{dialog.FileName}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "导出失败";
            }
            finally
            {
                ExportAllMatrixBtn.IsEnabled = true;
            }
        }

        /// <summary>
        /// 异步计算所有站点对的最短路径，并保存为用户指定的 CSV 文件
        /// </summary>
        /// <param name="outputFilePath">用户选择的 CSV 文件完整路径</param>
        /// <param name="distW">距离权重</param>
        /// <param name="transW">换乘权重</param>
        /// <param name="moneyW">票价权重</param>
        /// <param name="timeW">时间权重</param>
        /// <param name="stationW">站点数权重</param>
        /// <param name="crowdW">拥挤度权重</param>
        /// <summary>
        /// 异步计算所有站点对的最短路径，保存为 CSV 并生成统计摘要（性能优化版）
        /// </summary>
        /// <summary>
        /// 异步计算所有站点对的最短路径（极致性能版）
        /// </summary>
        private async Task ExportAllPairsToCsvAsync(string outputFilePath,
            double distW, double transW, double moneyW, double timeW, double stationW, double crowdW)
        {
            var allStations = _allStations;
            int n = allStations.Count;
            if (n < 2)
            {
                MessageBox.Show("站点数量不足", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var weight = new SimSearchWeight(distW, transW, moneyW, timeW, stationW, crowdW);
            DateTime monday8am = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
            long timestamp = new DateTimeOffset(monday8am).ToUnixTimeMilliseconds();

            int totalPairs = n * n - n;

            // 预分配容量，减少动态扩容
            var results = new ConcurrentBag<AllPairResult>();

            // 禁用精确计算以提升性能
            SimTrainTimeUtils.DisableExcatlyCalc = true;

            // 获取最优并行度（物理核心数，而非逻辑核心）
            int physicalCoreCount = Environment.ProcessorCount;
            // 如果是超线程，可以尝试使用物理核心数
            // int physicalCoreCount = GetPhysicalCoreCount();

            // 使用线程池饱和策略
            ThreadPool.GetMinThreads(out int minWorker, out int minIOC);
            ThreadPool.SetMinThreads(Math.Max(minWorker, physicalCoreCount * 2), minIOC);

            // 创建分区器，优化负载均衡
            var partitioner = Partitioner.Create(0, n, Math.Max(1, n / (physicalCoreCount * 4)));

            // 进度追踪（使用低锁策略）
            long processed = 0;
            int lastReportedProgress = 0;

            await Task.Run(() =>
            {
                // 使用 Parallel.ForEach 配合自定义分区器
                Parallel.ForEach(partitioner,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = physicalCoreCount,
                        TaskScheduler = TaskScheduler.Default
                    },
                    () => new ThreadLocalStorage(), // 每个线程的本地存储
                    (range, loopState, threadLocal) =>
                    {
                        var localResults = threadLocal.Results;
                        var localCache = threadLocal.Cache ??= new Dictionary<string, object>();

                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            string from = allStations[i];

                            // 内循环优化：避免重复查找
                            for (int j = 0; j < n; j++)
                            {
                                if (i == j) continue;

                                string to = allStations[j];

                                // 复用计算结果（如果适用）
                                var cacheKey = $"{from}->{to}";
                                if (!localCache.TryGetValue(cacheKey, out var cachedResult))
                                {
                                    var result = _graph.FindTransferPath(from, to, weight);
                                    if (result.Transfers != -1)
                                    {
                                        var path = result.Path;

                                        // 批量计算，减少方法调用开销
                                        double distanceKm = _graph.ComputePathDistance(path);
                                        double timeSec = SimTimeCalculator.ComputeTotalTime(
                                            _cache, path, _graph, weight, timestamp);
                                        double timeMin = timeSec / 60.0;
                                        var segments = _graph.GetRouteSegments(path);
                                        string routeDesc = _graph.FormatRouteWithDirection(path);
                                        int stationCount = segments.Sum(seg => seg.StationCount);
                                        double fareYuan = result.TotalMoney;

                                        var pairResult = new AllPairResult
                                        {
                                            From = from,
                                            To = to,
                                            DistanceKm = distanceKm,
                                            TimeMin = timeMin,
                                            Transfers = result.Transfers,
                                            StationCount = stationCount,
                                            Fare = fareYuan,
                                            Cost = result.Cost,
                                            RouteDesc = routeDesc
                                        };

                                        localResults.Add(pairResult);
                                        localCache[cacheKey] = pairResult;
                                    }
                                    else
                                    {
                                        localCache[cacheKey] = null;
                                    }
                                }
                                else if (cachedResult != null)
                                {
                                    localResults.Add((AllPairResult)cachedResult);
                                }

                                // 进度更新（无锁批量更新）
                                long current = Interlocked.Increment(ref processed);

                                // 自适应进度报告：初期频繁，后期稀疏
                                int reportInterval = current < totalPairs * 0.1 ? 1000 :
                                                   current < totalPairs * 0.5 ? 2000 : 5000;

                                int currentProgress = (int)(current / reportInterval);
                                if (currentProgress > lastReportedProgress)
                                {
                                    lastReportedProgress = currentProgress;
                                    int currentSnapshot = (int)current;

                                    // 异步更新UI，不阻塞计算
                                    Task.Run(() =>
                                    {
                                        Dispatcher.InvokeAsync(() =>
                                            StatusText.Text = $"进度 {currentSnapshot}/{totalPairs} ({(double)currentSnapshot / totalPairs * 100:F1}%)"
                                        , DispatcherPriority.Background);
                                    });
                                }
                            }
                        }
                        return threadLocal;
                    },
                    threadLocal =>
                    {
                        // 合并线程本地结果到全局集合
                        foreach (var result in threadLocal.Results)
                        {
                            results.Add(result);
                        }

                        // 清理线程本地资源
                        threadLocal.Results.Clear();
                        threadLocal.Cache?.Clear();
                    });
            });

            SimTrainTimeUtils.DisableExcatlyCalc = false;

            if (results.IsEmpty)
                throw new InvalidOperationException("未找到任何可达站点对");

            // ---- 写入 CSV（使用缓冲流提升IO性能）----
            await WriteCsvOptimizedAsync(outputFilePath, results, totalPairs);

            // ---- 生成统计摘要（并行计算统计值）----
            await GenerateStatisticsParallelAsync(outputFilePath, results, n);
        }

        /// <summary>
        /// 线程本地存储结构
        /// </summary>
        private class ThreadLocalStorage
        {
            public List<AllPairResult> Results { get; } = new List<AllPairResult>(10000);
            public Dictionary<string, object> Cache { get; set; }
        }

        /// <summary>
        /// 优化的CSV写入
        /// </summary>
        private async Task WriteCsvOptimizedAsync(string outputFilePath, ConcurrentBag<AllPairResult> results, int totalPairs)
        {
            // 使用更大的缓冲区
            const int bufferSize = 1024 * 1024; // 1MB缓冲区

            await using (var fs = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write,
                               FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var sw = new StreamWriter(fs, Encoding.UTF8, bufferSize))
            {
                // 写入BOM以提高Excel兼容性
                await sw.WriteAsync('\uFEFF');

                // 表头
                await sw.WriteLineAsync("序号,起点站,终点站,距离(km),换乘次数,时间(分钟),站数,票价(元),综合代价,乘车指引");

                // 并行排序后写入（使用ArrayPool减少GC）
                var sortedResults = results.OrderBy(r => r.From).ThenBy(r => r.To).ToArray();

                int index = 1;
                var lineBuffer = new StringBuilder(4096);

                foreach (var r in sortedResults)
                {
                    lineBuffer.Clear();
                    string desc = r.RouteDesc?.Replace("\"", "\"\"") ?? "";

                    lineBuffer.Append(index++).Append(',')
                             .Append(r.From).Append(',')
                             .Append(r.To).Append(',')
                             .Append(r.DistanceKm.ToString("F3")).Append(',')
                             .Append(r.Transfers).Append(',')
                             .Append(r.TimeMin.ToString("F1")).Append(',')
                             .Append(r.StationCount).Append(',')
                             .Append(r.Fare.ToString("F2")).Append(',')
                             .Append(r.Cost.ToString("F3")).Append(',')
                             .Append('"').Append(desc).Append('"');

                    await sw.WriteLineAsync(lineBuffer);
                }
            }
        }

        /// <summary>
        /// 并行生成统计摘要
        /// </summary>
        private async Task GenerateStatisticsParallelAsync(string outputFilePath, ConcurrentBag<AllPairResult> results, int n)
        {
            var list = results.ToList();

            // 并行计算各项统计
            var statsTasks = new[]
            {
        Task.Run(() => ComputeStats(list.Select(r => r.DistanceKm))),
        Task.Run(() => ComputeStats(list.Select(r => r.TimeMin))),
        Task.Run(() => ComputeStats(list.Select(r => (double)r.Transfers))),
        Task.Run(() => ComputeStats(list.Select(r => r.Fare))),
        Task.Run(() => ComputeStats(list.Select(r => r.Cost)))
    };

            var stats = await Task.WhenAll(statsTasks);

            var md = new StringBuilder(8192);
            md.AppendLine("# 全站点对通勤统计摘要");
            md.AppendLine($"- **总记录数**：{list.Count}");
            md.AppendLine($"- **站点总数**：{n}");
            md.AppendLine();
            md.AppendLine("## 距离分布 (km)");
            md.Append(stats[0]);
            md.AppendLine("## 时间分布 (分钟)");
            md.Append(stats[1]);
            md.AppendLine("## 换乘次数分布");
            md.Append(stats[2]);
            md.AppendLine("## 票价分布 (元)");
            md.Append(stats[3]);
            md.AppendLine("## 综合代价分布");
            md.Append(stats[4]);

            string mdPath = Path.ChangeExtension(outputFilePath, ".md");
            await File.WriteAllTextAsync(mdPath, md.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 并行计算统计值
        /// </summary>
        private string ComputeStats(IEnumerable<double> values)
        {
            var sorted = values.ToList();
            sorted.Sort();

            if (sorted.Count == 0) return "- 无数据\n";

            var sb = new StringBuilder();
            sb.AppendLine($"- 最小值：{sorted.Min():F2}");
            sb.AppendLine($"- 最大值：{sorted.Max():F2}");
            sb.AppendLine($"- 平均值：{sorted.Average():F2}");
            sb.AppendLine($"- 中位数：{Percentile(sorted, 0.50):F2}");
            sb.AppendLine($"- Q1 (25%)：{Percentile(sorted, 0.25):F2}");
            sb.AppendLine($"- Q3 (75%)：{Percentile(sorted, 0.75):F2}");
            sb.AppendLine();

            return sb.ToString();
        }

        // 可选：获取物理核心数（需要P/Invoke）
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLogicalProcessorInformation(
            [Out] SYSTEM_LOGICAL_PROCESSOR_INFORMATION[] buffer,
            ref uint returnLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION
        {
            public ulong ProcessorMask;
            public uint Relationship;
            public uint Flags;
        }

        private int GetPhysicalCoreCount()
        {
            try
            {
                uint length = 0;
                GetLogicalProcessorInformation(null, ref length);
                var buffer = new SYSTEM_LOGICAL_PROCESSOR_INFORMATION[length / Marshal.SizeOf<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>()];
                GetLogicalProcessorInformation(buffer, ref length);

                return buffer.Count(info => info.Relationship == 0); // RelationProcessorCore
            }
            catch
            {
                return Environment.ProcessorCount;
            }
        }

        /// <summary>
        /// 辅助数据类
        /// </summary>
        private class AllPairResult
        {
            public string From { get; set; }
            public string To { get; set; }
            public double DistanceKm { get; set; }
            public double TimeMin { get; set; }
            public int Transfers { get; set; }
            public int StationCount { get; set; }
            public double Fare { get; set; }
            public double Cost { get; set; }
            public string RouteDesc { get; set; }
        }
    }

    /// <summary>
    /// 通勤分析结果数据模型
    /// </summary>
    public class CommuteResult : INotifyPropertyChanged
    {
        private int _index;
        private string _station;
        private double _distance;
        private int _transfers;
        private double _time;
        private double _fare;
        private double _value;
        private string _routeDesc;
        private int _stationCount;

        public int Index { get => _index; set { _index = value; OnPropertyChanged(); } }
        public string Station { get => _station; set { _station = value; OnPropertyChanged(); } }
        public double Distance { get => _distance; set { _distance = value; OnPropertyChanged(); } }
        public int Transfers { get => _transfers; set { _transfers = value; OnPropertyChanged(); } }
        public double Time { get => _time; set { _time = value; OnPropertyChanged(); } }
        public double Fare { get => _fare; set { _fare = value; OnPropertyChanged(); } }
        public double Value { get => _value; set { _value = value; OnPropertyChanged(); } }
        public string RouteDesc { get => _routeDesc; set { _routeDesc = value; OnPropertyChanged(); } }
        public int StationCount { get => _stationCount; set { _stationCount = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}