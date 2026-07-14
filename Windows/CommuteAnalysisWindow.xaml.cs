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
using MetroEditor.PathFinding;
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
        private readonly MetroGraph _graph;              // 路网图（用于路径计算）
        private readonly List<string> _allStations;      // 所有站点列表
        private ObservableCollection<CommuteResult> _results = new(); // 分析结果（绑定到DataGrid）

        public CommuteAnalysisWindow(ParsedData data)
        {
            InitializeComponent();
            _data = data;
            _graph = new MetroGraph(data);
            _allStations = data.Stations.Keys.OrderBy(x => x).ToList();

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
            var weight = SearchWeight.LoadFromRegistry();

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
                    var result = _graph.FindPath(start, station, weight);
                    if (result.Transfers == -1) continue; // 不可达则跳过

                    var path = result.Path;
                    double distance = _graph.ComputePathDistance(path);
                    double timeSec = TimeCalculator.ComputeTotalTime(path, _graph, _data);
                    int stationCount = path.Select(n => GraphKeyUtils.ParseNode(n)[1]).Distinct().Count() - 1;
                    var segments = _graph.GetRouteSegments(path);
                    string routeDesc = BuildRouteDescription(segments);

                    list.Add(new CommuteResult
                    {
                        Station = station,
                        Distance = distance,
                        Transfers = result.Transfers,
                        Time = timeSec / 60.0, // 转换为分钟
                        Fare = result.TotalMoney,
                        Value = result.Cost,   // 综合代价（用于排序）
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

        /// <summary>
        /// 构建乘车指引描述
        /// </summary>
        /// <param name="segments">路径分段</param>
        /// <returns>人类可读的乘车指引</returns>
        private string BuildRouteDescription(List<RouteSegment> segments)
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
                MessageBox.Show($"导出成功：{dialog.FileName}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
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