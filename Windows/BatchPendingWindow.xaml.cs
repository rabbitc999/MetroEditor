using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 批量设置站点暂缓开通窗口
    /// 功能：快速生成 STATION_STATUS ... PENDING 代码，用于标记新线或新站尚未开通的状态
    /// 应用场景：
    /// - 新线路建设期间，部分车站暂未投入运营
    /// - 既有线路延长段，新增车站尚未开通
    /// - 临时封闭施工的车站
    /// </summary>
    public partial class BatchPendingWindow : Window
    {
        private readonly ParsedData _data;                      // 解析后的路网数据
        private readonly Dictionary<string, string> _lineNameMap; // 线路ID到显示名称的映射
        private readonly List<string> _lineIds;                 // 所有线路ID列表
        private readonly ObservableCollection<StationCheckItem> _stations = new(); // 站点勾选列表（绑定到UI）
        private string _currentLineId; // 当前选中的线路ID

        public BatchPendingWindow(ParsedData data)
        {
            InitializeComponent();
            _data = data;

            // 获取所有线路ID并按字母排序
            _lineIds = data.Lines.Keys.OrderBy(x => x).ToList();

            // 构建线路显示名称映射（跨城市线路添加城市前缀）
            _lineNameMap = data.Lines.ToDictionary(
                kv => kv.Key,
                kv =>
                {
                    string city = kv.Value.City ?? data.MainCity;
                    // 非主城市线路添加城市前缀以便区分
                    return (city == data.MainCity) ? kv.Value.Name : $"{city}{kv.Value.Name}";
                }
            );

            // 填充线路下拉框
            var lineItems = _lineIds.Select(id => new { Display = _lineNameMap.GetValueOrDefault(id, id), Id = id }).ToList();
            LineCombo.ItemsSource = lineItems;
            if (lineItems.Any())
            {
                LineCombo.SelectedIndex = 0;
                _currentLineId = lineItems.First().Id;
            }

            // 初始化站点列表（按当前线路筛选）
            LoadStations(_currentLineId, SearchBox.Text);

            // 事件绑定
            LineCombo.SelectionChanged += LineCombo_SelectionChanged; // 线路切换时刷新站点
            SearchBox.TextChanged += SearchBox_TextChanged;           // 搜索框实时过滤
        }

        /// <summary>
        /// 线路选择变更事件处理
        /// </summary>
        private void LineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentLineId = LineCombo.SelectedValue?.ToString();
            if (!string.IsNullOrEmpty(_currentLineId))
                LoadStations(_currentLineId, SearchBox.Text);
        }

        /// <summary>
        /// 搜索框文本变更事件处理（实时过滤）
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentLineId))
                LoadStations(_currentLineId, SearchBox.Text);
        }

        /// <summary>
        /// 加载指定线路的站点列表
        /// </summary>
        /// <param name="lineId">线路ID</param>
        /// <param name="filter">搜索过滤词</param>
        private void LoadStations(string lineId, string filter)
        {
            _stations.Clear();
            if (string.IsNullOrEmpty(lineId))
            {
                StationListBox.ItemsSource = _stations;
                return;
            }

            // 从Directions中提取该线路途经的所有站点（去重并排序）
            var lineStations = _data.Directions.Values
                .Where(d => d.LineId == lineId)  // 筛选指定线路
                .SelectMany(d => d.Stations)     // 展开所有方向的站点
                .Distinct()                      // 去重
                .OrderBy(x => x)                 // 按名称排序
                .ToList();

            // 应用搜索过滤（不区分大小写）
            var filtered = string.IsNullOrEmpty(filter)
                ? lineStations
                : lineStations.Where(s => s.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            // 构建勾选列表项
            foreach (var name in filtered)
            {
                _stations.Add(new StationCheckItem { Name = name, IsSelected = false });
            }
            StationListBox.ItemsSource = _stations;
        }

        /// <summary>
        /// 生成代码按钮点击事件
        /// </summary>
        private void GenerateCode_Click(object sender, RoutedEventArgs e)
        {
            var selectedLineId = LineCombo.SelectedValue?.ToString();
            if (string.IsNullOrEmpty(selectedLineId))
            {
                MessageBox.Show("请选择线路", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 获取用户勾选的站点
            var selectedStations = _stations.Where(s => s.IsSelected).Select(s => s.Name).ToList();
            if (selectedStations.Count == 0)
            {
                MessageBox.Show("请至少选择一个站点", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 生成 MetroLang 代码
            var sb = new StringBuilder();
            foreach (var st in selectedStations)
                sb.AppendLine($"STATION_STATUS '{st}' {selectedLineId} PENDING");

            PreviewTextBox.Text = sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 复制预览代码到剪贴板
        /// </summary>
        private void CopyPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PreviewTextBox.Text))
            {
                MessageBox.Show("没有可复制的内容", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                MainWindow.InsertText(PreviewTextBox.Text);
                MessageBox.Show("代码已复制到主界面", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ======================== 内部数据模型 ========================
        /// <summary>
        /// 站点勾选项数据模型（实现属性变更通知）
        /// </summary>
        public class StationCheckItem : INotifyPropertyChanged
        {
            private string _name;
            private bool _isSelected;

            public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
            public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = "") =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}