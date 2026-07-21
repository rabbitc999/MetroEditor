using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.Simulator.Algorithms;
using MetroEditor.Simulator.Data;
using MetroEditor.Simulator.Models;
using MetroEditor.Simulator.Utils;
using Microsoft.Win32;
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
                    var segments = _graph.GetRouteSegments(path);
                    string routeDesc = BuildRouteDescription(segments);

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
        /// 构建乘车指引描述
        /// </summary>
        /// <param name="segments">路径分段</param>
        /// <returns>人类可读的乘车指引</returns>
        private string BuildRouteDescription(List<SimRouteSegment> segments)
        {
            if (segments == null || segments.Count == 0) return "";
            var parts = new List<string>();
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                string line = seg.LineName;
                string dir = seg.Direction;
                int count = seg.StationCount;
                if (i == 0)
                    parts.Add($"乘坐{line}（{dir}方向）{count}站");
                else
                    parts.Add($"换乘{line}（{dir}方向）{count}站");
            }
            return string.Join(" → ", parts);
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