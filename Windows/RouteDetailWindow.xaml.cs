using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.Simulator.Algorithms;
using MetroEditor.Simulator.Data;
using MetroEditor.Simulator.Models;
using MetroEditor.Simulator.Utils;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 路径详情查看器窗口
    /// 功能：查询并显示两点间的详细路径规划结果
    /// 核心展示内容：
    /// 1. 路径摘要（起终点、距离、时间、票价）
    /// 2. 乘车指引（分段显示线路、方向、站数）
    /// 3. 站点列表（每段包含的详细站点）
    /// </summary>
    /// <remarks>
    /// 应用场景：
    /// - 验证路径规划算法的正确性
    /// - 分析特定OD对的出行方案
    /// - 调试换乘逻辑和票价计算
    /// 
    /// 技术特点：
    /// - 模糊匹配：支持不完整的站点名称输入
    /// - 实时计算：基于当前的路网数据和权重配置
    /// - 可视化展示：使用颜色区分不同线路
    /// </remarks>
    public partial class RouteDetailWindow : Window
    {
        private readonly SimDataCache _cache;
        private readonly SimMetroGraph _graph;              // 路网图（用于路径搜索）
        private readonly List<string> _allStations;         // 所有站点列表

        public RouteDetailWindow(JsonRoot data)
        {
            InitializeComponent();
            _cache = SimDataParser.Parse(data);
            _graph = new SimMetroGraph(_cache);
            _allStations = SimStationUtils.GetActiveStationList(_cache);

            // 填充下拉框
            StartCombo.ItemsSource = _allStations;
            EndCombo.ItemsSource = _allStations;

            DayOfWeekCombo.IsEnabled = false;
            HourBox.IsEnabled = false;
            MinuteBox.IsEnabled = false;
            Loaded += (o, e) => Topmost = false;
        }

        #region 查询逻辑
        /// <summary>
        /// 获取模拟时间的时间戳（毫秒）
        /// </summary>
        private long GetSimulatedTimestamp()
        {
            // 如果勾选了"使用当前时间"，则返回当前时间
            if (UseCurrentTimeCheckBox.IsChecked == true)
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 解析星期
            int dayOffset = DayOfWeekCombo.SelectedIndex; // 0=周一, 1=周二, ...
                                                          // 2026-07-20 是周一，以此为基准
            DateTime baseDate = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
            DateTime targetDate = baseDate.AddDays(dayOffset);

            // 解析小时
            if (!int.TryParse(HourBox.Text, out int hour) || hour < 0 || hour > 23)
                hour = 8;
            // 解析分钟
            if (!int.TryParse(MinuteBox.Text, out int minute) || minute < 0 || minute > 59)
                minute = 0;

            DateTime result = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day,
                                           hour, minute, 0, DateTimeKind.Utc);
            return new DateTimeOffset(result).ToUnixTimeMilliseconds();
        }

        private void UseCurrentTime_Changed(object sender, RoutedEventArgs e)
        {
            bool useCurrent = UseCurrentTimeCheckBox.IsChecked == true;
            DayOfWeekCombo.IsEnabled = !useCurrent;
            HourBox.IsEnabled = !useCurrent;
            MinuteBox.IsEnabled = !useCurrent;
        }

        /// <summary>
        /// 查询按钮点击事件：执行路径规划
        /// </summary>
        private void QueryBtn_Click(object sender, RoutedEventArgs e)
        {
            string start = StartCombo.Text?.Trim();
            string end = EndCombo.Text?.Trim();

            if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end))
            {
                MessageBox.Show("请选择起点和终点", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 模糊匹配站点名称
            string matchedStart = FindBestMatch(start, _allStations);
            string matchedEnd = FindBestMatch(end, _allStations);
            if (matchedStart == null || matchedEnd == null)
            {
                MessageBox.Show("站点不存在，请检查输入", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 更新输入框为匹配值
            StartCombo.Text = matchedStart;
            EndCombo.Text = matchedEnd;

            // 执行路径搜索（使用注册表中的权重配置）
            double distW = ReadWeight(DistWeightBox, 0);
            double transW = ReadWeight(TransferWeightBox, 0);
            double moneyW = ReadWeight(MoneyWeightBox, 0);
            double timeW = ReadWeight(TimeWeightBox, 1);
            double stationW = ReadWeight(StationWeightBox, 0);
            double crowdW = ReadWeight(CrowdWeightBox, 0);

            var weight = new SimSearchWeight(distW, transW, moneyW, timeW, stationW, crowdW);
            var result = _graph.FindTransferPath(matchedStart, matchedEnd, weight);

            if (result.Transfers == -1)
            {
                MessageBox.Show("无法规划路线，请确认线路是否连通", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                ClearSummary();
                SegmentsItemsControl.ItemsSource = null;
                return;
            }

            // 计算各项指标
            var path = result.Path;
            double distance = _graph.ComputePathDistance(path);
            double timeSec = SimTimeCalculator.ComputeTotalTime(_cache, path, _graph, weight, GetSimulatedTimestamp());
            double timeMin = timeSec / 60.0;
            double fare = result.TotalMoney;

            // 获取路径分段
            var segments = _graph.GetRouteSegments(path);

            // 更新摘要信息
            StartText.Text = matchedStart;
            EndText.Text = matchedEnd;
            DistanceText.Text = $"{distance:F2} km";
            TimeText.Text = $"{timeMin:F1} 分钟";
            FareText.Text = $"{fare:F2} 元";

            // 构建分段显示项
            var items = BuildSegmentDisplayItems(segments);
            // 提取每段包含的站点列表
            var segmentStations = ExtractSegmentStations(path, segments);
            for (int i = 0; i < items.Count && i < segmentStations.Count; i++)
            {
                items[i].Stations = segmentStations[i];
            }
            SegmentsItemsControl.ItemsSource = items;
        }

        // 添加辅助方法
        private double ReadWeight(TextBox box, double defaultValue)
        {
            if (double.TryParse(box.Text, out double val))
                return val;
            return defaultValue;
        }

        /// <summary>
        /// 模糊匹配站点名称
        /// </summary>
        /// <param name="input">用户输入</param>
        /// <param name="stationList">站点列表</param>
        /// <returns>最佳匹配的站点名</returns>
        private string FindBestMatch(string input, List<string> stationList)
        {
            if (string.IsNullOrEmpty(input)) return null;
            // 1. 精确匹配
            var exact = stationList.FirstOrDefault(s => s.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            // 2. 前缀匹配
            var prefix = stationList.FirstOrDefault(s => s.StartsWith(input));
            if (prefix != null) return prefix;
            // 3. 包含匹配
            var contains = stationList.FirstOrDefault(s => s.Contains(input));
            return contains;
        }
        #endregion

        #region 数据展示构建
        /// <summary>
        /// 构建分段显示项集合
        /// </summary>
        private List<SegmentDisplayItem> BuildSegmentDisplayItems(List<SimRouteSegment> segments)
        {
            var items = new List<SegmentDisplayItem>();
            foreach (var seg in segments)
            {
                var color = GetLineColor(seg.LineId);
                string title = seg.LineName;
                string subtitle = $"开往 {seg.Direction} 方向，{seg.StationCount} 站";
                try
                {
                    items.Add(new SegmentDisplayItem
                    {
                        Title = title,
                        Subtitle = subtitle,
                        Color = new SolidColorBrush(color),
                        Stations = new List<string>()
                    });
                }
                catch (Exception)
                {
                    items.Add(new SegmentDisplayItem
                    {
                        Title = title,
                        Subtitle = subtitle,
                        Color = new SolidColorBrush(color),
                        Stations = new List<string>()
                    });
                }
            }
            return items;
        }

        /// <summary>
        /// 提取每段包含的站点列表
        /// </summary>
        private List<List<string>> ExtractSegmentStations(List<string> path, List<SimRouteSegment> segments)
        {
            var result = new List<List<string>>();
            int pathIndex = 0;
            foreach (var seg in segments)
            {
                var stations = new List<string>();
                while (pathIndex < path.Count)
                {
                    var node = path[pathIndex];
                    var station = SimGraphKeyUtils.ParseNode(node)[1];
                    stations.Add(station);
                    pathIndex++;
                    if (station == seg.EndStation)
                        break;
                }
                result.Add(stations);
            }
            return result;
        }

        /// <summary>
        /// 获取线路颜色（用于UI显示）
        /// </summary>
        private Color GetLineColor(string lineId)
        {
            if (_cache.LineColorMap.TryGetValue(lineId, out var color))
            {
                // 其次使用线路主色
                if (!string.IsNullOrEmpty(color))
                {
                    try { return (Color)ColorConverter.ConvertFromString(color); }
                    catch { }
                }
            }
            return Colors.Gray; // 默认灰色
        }

        /// <summary>
        /// 清空摘要信息
        /// </summary>
        private void ClearSummary()
        {
            StartText.Text = "";
            EndText.Text = "";
            DistanceText.Text = "";
            TimeText.Text = "";
            FareText.Text = "";
        }
        #endregion

        /// <summary>
        /// 分段显示项数据模型
        /// </summary>
        public class SegmentDisplayItem
        {
            public string Title { get; set; }
            public string Subtitle { get; set; }
            public Brush Color { get; set; }
            public List<string> Stations { get; set; }
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
}