using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.Windows.Controls
{
    public partial class LineListPanel : UserControl
    {
        public static readonly DependencyProperty ParsedDataProperty =
            DependencyProperty.Register(nameof(ParsedData), typeof(ParsedData), typeof(LineListPanel),
                new PropertyMetadata(null, OnParsedDataChanged));

        public ParsedData ParsedData
        {
            get => (ParsedData)GetValue(ParsedDataProperty);
            set => SetValue(ParsedDataProperty, value);
        }

        public event EventHandler<LineViewModel>? LineSelected;
        public event EventHandler<LineViewModel>? LineDoubleClicked;
        public event EventHandler<LineViewModel>? EditLineRequested;

        private ICollectionView? _view;
        private List<LineViewModel> _lines = new();

        public LineListPanel() => InitializeComponent();

        private static void OnParsedDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (LineListPanel)d;
            control.UpdateData(e.NewValue as ParsedData);
        }

        private void UpdateData(ParsedData? data)
        {
            if (data == null)
            {
                _lines.Clear();
                dataGrid.ItemsSource = null;
                detailText.Text = "请编译代码获取线路数据";
                return;
            }

            _lines = BuildLineViewModels(data);
            _view = CollectionViewSource.GetDefaultView(_lines);
            dataGrid.ItemsSource = _view;
            ApplyFilter();

            if (_lines.Any())
                dataGrid.SelectedIndex = 0;
            else
                detailText.Text = "暂无线路数据";
        }

        private List<LineViewModel> BuildLineViewModels(ParsedData data)
        {
            var list = new List<LineViewModel>();
            foreach (var kv in data.Lines)
            {
                var line = kv.Value;
                list.Add(new LineViewModel
                {
                    LineId = kv.Key,
                    Name = line.Name,
                    Color = line.Color,
                    City = line.City,
                    Formation = line.Formation ?? string.Empty,
                    Maxspeed = line.Maxspeed,
                    OpenYear = line.OpenYear,
                    PowerKw = line.PowerKw,
                    Brake = line.Brake,
                    Opened = !line.Disabled
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
                if (item is LineViewModel l)
                {
                    return column switch
                    {
                        "LineId" => l.LineId.Contains(keyword),
                        "Name" => l.Name.Contains(keyword),
                        "City" => l.City.Contains(keyword),
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
            if (dataGrid.SelectedItem is LineViewModel line)
            {
                LineSelected?.Invoke(this, line);
                UpdateDetail(line);
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dataGrid.SelectedItem is LineViewModel line)
                LineDoubleClicked?.Invoke(this, line);
        }

        private void UpdateDetail(LineViewModel line)
        {
            if (ParsedData == null || !ParsedData.Lines.TryGetValue(line.LineId, out var lineInfo))
            {
                detailText.Text = "无法获取线路详情";
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"线路 ID: {line.LineId}");
            sb.AppendLine($"名称: {line.Name}");
            sb.AppendLine($"城市: {line.City}");
            sb.AppendLine($"颜色: {line.Color}");
            sb.AppendLine($"编组: {line.Formation ?? "未指定"}");
            sb.AppendLine($"最高速度: {line.Maxspeed?.ToString() + " km/h" ?? "未指定"}");
            sb.AppendLine($"开通年份: {line.OpenYear}");
            sb.AppendLine($"功率: {line.PowerKw?.ToString("F2") + " kW²" ?? "未指定"}");
            sb.AppendLine($"刹车: {line.Brake?.ToString("F2") + " m/s²" ?? "未指定"}");
            sb.AppendLine($"状态: {(line.Opened ? "运营中" : "未开通")}");

            var dirs = ParsedData.Directions.Where(kv => kv.Value.LineId == line.LineId).ToList();
            sb.AppendLine($"方向数: {dirs.Count}");
            foreach (var kv in dirs)
            {
                var dir = kv.Value;
                string attrs = dir.Attrs.Count > 0 ? string.Join(", ", dir.Attrs) : "无";
                string start = dir.Stations.FirstOrDefault() ?? "";
                string end = dir.Stations.LastOrDefault() ?? "";
                sb.AppendLine($"  {kv.Key}: {start} -> {end} [{attrs}] ({dir.Stations.Count}站)");
            }

            // ---- 关联方向列表（详细） ----
            var dirsWithKey = ParsedData.Directions.Values.Where(d => d.LineId == line.LineId).ToList();
            if (dirsWithKey.Any())
            {
                sb.AppendLine("关联方向:");
                foreach (var d in dirsWithKey)
                {
                    string attrs = d.Attrs != null && d.Attrs.Any() ? string.Join(", ", d.Attrs) : "无";
                    string start = d.Stations.FirstOrDefault() ?? "";
                    string end = d.Stations.LastOrDefault() ?? "";
                    sb.AppendLine($"  {d.LineId}: {start} → {end} [{attrs}] ({d.Stations.Count}站)");
                }
            }

            // ---- 运营时间 ----
            if (ParsedData.OpenTimes != null && ParsedData.OpenTimes.Any(o => o.LineId == line.LineId))
            {
                var openRules = ParsedData.OpenTimes.Where(o => o.LineId == line.LineId).ToList();
                sb.AppendLine("运营时间:");
                foreach (var rule in openRules)
                {
                    string days = GetDayDisplay(rule.Days);
                    sb.AppendLine($"  {rule.StartTime:hh\\:mm} - {rule.EndTime:hh\\:mm} ({days})");
                }
            }

            // ---- 发车间隔 ----
            if (ParsedData.HeadwayRules != null && ParsedData.HeadwayRules.Any(h => h.LineId == line.LineId))
            {
                var headways = ParsedData.HeadwayRules.Where(h => h.LineId == line.LineId).ToList();
                sb.AppendLine("发车间隔:");
                foreach (var rule in headways)
                {
                    string days = GetDayDisplay(rule.Days);
                    sb.AppendLine($"  {rule.StartTime:hh\\:mm} - {rule.EndTime:hh\\:mm}: {rule.IntervalSeconds}s ({days})");
                }
            }

            // ---- 拥挤度（汇总该线路所有方向） ----
            if (ParsedData.CrowdRules != null && ParsedData.CrowdRules.Any())
            {
                var dirIds = dirsWithKey.Select(d => d.LineId).Distinct(); // 实际使用方向ID，需从Directions中取Key
                                                                    // 更好的方式：直接从ParsedData.Directions取所有方向Key
                var allDirKeys = ParsedData.Directions.Keys;
                var crowdRulesForLine = ParsedData.CrowdRules
                    .Where(c => c.DirectionKey.Split("|")[0] == line.LineId)
                    .ToList();

                if (crowdRulesForLine.Any())
                {
                    sb.AppendLine("拥挤度规则:");
                    foreach (var rule in crowdRulesForLine)
                    {
                        string dir = rule.DirectionKey.Split('|')[1] + " → " + rule.DirectionKey.Split('|')[2];
                        string days = GetDayDisplay(rule.Days);
                        sb.AppendLine($"  {dir}: {rule.StartTime:hh\\:mm} - {rule.EndTime:hh\\:mm} 系数 {rule.Factor:F2} ({days})");
                    }
                }
            }

            detailText.Text = sb.ToString();
        }

        private static string GetDayDisplay(List<DayOfWeek> days)
        {
            if (days == null || !days.Any())
                return "每日";

            var set = new HashSet<DayOfWeek>(days);

            // 1. 全天（包含全部7天）
            if (set.Count == 7)
                return "全天";

            // 2. 工作日（仅包含周一至周五，不包含周六日）
            bool hasWeekday = set.Any(d => d >= DayOfWeek.Monday && d <= DayOfWeek.Friday);
            bool hasWeekend = set.Any(d => d == DayOfWeek.Saturday || d == DayOfWeek.Sunday);
            if (hasWeekday && !hasWeekend && set.Count == 5)
                return "工作日";
            if (!hasWeekday && hasWeekend && set.Count == 2)
                return "休息日";

            // 3. 其他情况：逐个显示中文
            var names = new Dictionary<DayOfWeek, string>
            {
                { DayOfWeek.Monday, "星期一" },
                { DayOfWeek.Tuesday, "星期二" },
                { DayOfWeek.Wednesday, "星期三" },
                { DayOfWeek.Thursday, "星期四" },
                { DayOfWeek.Friday, "星期五" },
                { DayOfWeek.Saturday, "星期六" },
                { DayOfWeek.Sunday, "星期日" }
            };

            // 按星期顺序显示
            var ordered = days.OrderBy(d => d);
            return string.Join("、", ordered.Select(d => names[d]));
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedItem is LineViewModel line)
                EditLineRequested?.Invoke(this, line);
        }
    }

    public class LineViewModel
    {
        public string LineId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Formation { get; set; } = string.Empty;
        public double? Maxspeed { get; set; }
        public int OpenYear { get; set; }
        public double? PowerKw { get; set; }
        public double? Brake { get; set; }
        public bool Opened { get; set; } = true;
    }
}