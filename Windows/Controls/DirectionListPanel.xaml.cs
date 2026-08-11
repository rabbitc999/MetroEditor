using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.Windows.Controls
{
    public partial class DirectionListPanel : UserControl
    {
        public static readonly DependencyProperty ParsedDataProperty =
            DependencyProperty.Register(nameof(ParsedData), typeof(ParsedData), typeof(DirectionListPanel),
                new PropertyMetadata(null, OnParsedDataChanged));

        public ParsedData ParsedData
        {
            get => (ParsedData)GetValue(ParsedDataProperty);
            set => SetValue(ParsedDataProperty, value);
        }

        public event EventHandler<DirectionViewModel>? DirectionSelected;
        public event EventHandler<DirectionViewModel>? DirectionDoubleClicked;
        public event EventHandler<DirectionViewModel>? EditDirectionRequested;

        private ICollectionView? _view;
        private List<DirectionViewModel> _directions = new();

        public DirectionListPanel() => InitializeComponent();

        private static void OnParsedDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DirectionListPanel)d;
            control.UpdateData(e.NewValue as ParsedData);
        }

        private void UpdateData(ParsedData? data)
        {
            if (data == null)
            {
                _directions.Clear();
                dataGrid.ItemsSource = null;
                detailText.Text = "请编译代码获取方向数据";
                return;
            }

            _directions = BuildDirectionViewModels(data);
            _view = CollectionViewSource.GetDefaultView(_directions);
            dataGrid.ItemsSource = _view;
            ApplyFilter();

            if (_directions.Any())
                dataGrid.SelectedIndex = 0;
            else
                detailText.Text = "暂无方向数据";
        }

        private List<DirectionViewModel> BuildDirectionViewModels(ParsedData data)
        {
            // 属性显示映射（仅用于界面展示）
            var attrDisplayMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LOOP"] = "环线",
                ["FAST"] = "快车",
                ["INNER"] = "内环",
                ["OUTER"] = "外环"
            };

            var list = new List<DirectionViewModel>();
            foreach (var kv in data.Directions)
            {
                var dir = kv.Value;
                var lineInfo = data.Lines.TryGetValue(dir.LineId, out var li) ? li : null;
                string displayLine = lineInfo != null ? $"{lineInfo.City}{lineInfo.Name} ({dir.LineId})" : dir.LineId;

                var displayAttrs = new List<string>();
                if (dir.Attrs.Contains("LOOP"))
                {
                    if (!string.IsNullOrEmpty(dir.DirectionType))
                    {
                        string dirType = dir.DirectionType.ToLowerInvariant() == "inner" ? "内环" : "外环";
                        dirType = $"环线({dirType})";
                        displayAttrs.Add(dirType);
                    }
                    else
                        displayAttrs.Add("环线");
                }
                if (dir.Attrs.Contains("FAST"))
                    displayAttrs.Add("快车");
                if (dir.IsFuture)
                    displayAttrs.Add("规划建设线路");

                string attrsDisplay = string.Join(", ", displayAttrs);

                if (string.IsNullOrEmpty(attrsDisplay)) { attrsDisplay = "无"; }

                list.Add(new DirectionViewModel
                {
                    LineID = dir.LineId,
                    DirId = kv.Key,
                    DisplayLine = displayLine,
                    Start = dir.Stations.FirstOrDefault(),
                    End = dir.Stations.LastOrDefault(),
                    Attrs = attrsDisplay
                });
            }
            return list;
        }

        private void ApplyFilter()
        {
            if (_view == null) return;
            string keyword = FilterTextBox.Text.Trim();
            string column = (FilterColumn.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(column))
            {
                _view.Filter = null;
                return;
            }

            _view.Filter = item =>
            {
                if (item is DirectionViewModel d)
                {
                    return column switch
                    {
                        "DirId" => d.DirId.Contains(keyword),
                        "DisplayLine" => d.DisplayLine.Contains(keyword),
                        "Start" => d.Start.Contains(keyword),
                        "End" => d.End.Contains(keyword),
                        "Attrs" => d.Attrs.Contains(keyword),
                        _ => true
                    };
                }
                return true;
            };
            _view.Refresh();
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
        private void FilterColumn_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();
        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterTextBox.Text = string.Empty;
            ApplyFilter();
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGrid.SelectedItem is DirectionViewModel dir)
            {
                DirectionSelected?.Invoke(this, dir);
                UpdateDetail(dir);
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dataGrid.SelectedItem is DirectionViewModel dir)
                DirectionDoubleClicked?.Invoke(this, dir);
        }

        private void UpdateDetail(DirectionViewModel dir)
        {
            var sb = new System.Text.StringBuilder();

            if (ParsedData == null || !ParsedData.Directions.TryGetValue(dir.DirId, out var dirInfo))
            {
                detailText.Text = "无法获取方向详情";
                return;
            }

            if (ParsedData == null || !ParsedData.Distances.TryGetValue(dir.DirId, out var distanceInfo))
            {
                detailText.Text = "无法获取距离详情";
                return;
            }
            sb.AppendLine($"方向 ID: {dir.DirId}");
            sb.AppendLine($"线路: {dir.DisplayLine}");
            sb.AppendLine($"属性: {dir.Attrs}");
            sb.AppendLine($"起点: {dir.Start}");
            sb.AppendLine($"终点: {dir.End}");

            // ---- 停站时间 ----
            sb.AppendLine($"站点列表 ({dirInfo.Stations.Count} 站):");
            for (int i = 0; i < dirInfo.Stations.Count; i++)
            {
                var station = dirInfo.Stations[i];
                double dwellTime = ParsedData.DefaultDwellTime; // 默认值
                double distance = i >= distanceInfo.Count ? double.NaN : distanceInfo[i];
                distance = distance >= 1e49 || distance == double.NaN ? double.NaN : distance;
                if (ParsedData.DwellTimes.TryGetValue((station, dir.LineID), out double customTime))
                {
                    dwellTime = customTime;
                }
                sb.AppendLine($"  {(i + 1):D2}. {dirInfo.Stations[i]} [下一站距离 {distance:F2} km] [停站时间 {dwellTime:F1} s]");
            }

            // ---- 限速信息 ----
            if (ParsedData.SpeedLimits != null && ParsedData.SpeedLimits.Any(s => s.DirectionKey == dir.DirId))
            {
                var limits = ParsedData.SpeedLimits.Where(s => s.DirectionKey == dir.DirId).ToList();
                sb.AppendLine("限速信息:");
                foreach (var limit in limits)
                {
                    sb.AppendLine($"  {limit.FromStation} → {limit.ToStation}: {limit.SpeedKmh} km/h");
                }
            }

            // ---- 车辆段信息 ----
            if (ParsedData.Depots != null && ParsedData.Depots.Any(d => d.DirectionKey == dir.DirId))
            {
                var depots = ParsedData.Depots.Where(d => d.DirectionKey == dir.DirId).ToList();
                sb.AppendLine("车辆段:");
                foreach (var depot in depots)
                {
                    string timeStr = depot.DepartureTime.HasValue ? depot.DepartureTime.Value.ToString(@"hh\:mm") : "未指定";
                    sb.AppendLine($"  {depot.Station} (线路: {depot.LineId}), 出库时间: {timeStr}");
                }
            }

            detailText.Text = sb.ToString();
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedItem is DirectionViewModel dir)
                EditDirectionRequested?.Invoke(this, dir);
        }
    }

    public class DirectionViewModel
    {
        public string LineID { get; set; } = string.Empty;
        public string DirId { get; set; } = string.Empty;
        public string DisplayLine { get; set; } = string.Empty;
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public string Attrs { get; set; } = string.Empty;
    }
}