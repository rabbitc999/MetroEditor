using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.Windows.Controls
{
    public partial class StationListPanel : UserControl
    {
        // ========== 依赖属性 ==========
        public static readonly DependencyProperty ParsedDataProperty =
            DependencyProperty.Register(nameof(ParsedData), typeof(ParsedData), typeof(StationListPanel),
                new PropertyMetadata(null, OnParsedDataChanged));

        public ParsedData ParsedData
        {
            get => (ParsedData)GetValue(ParsedDataProperty);
            set => SetValue(ParsedDataProperty, value);
        }

        // ========== 事件 ==========
        public event EventHandler<StationViewModel>? StationSelected;
        public event EventHandler<StationViewModel>? StationDoubleClicked;
        public event EventHandler<StationViewModel>? EditStationRequested;
        public event EventHandler? AddStationRequested;

        // ========== 字段 ==========
        private ICollectionView? _view;
        private List<StationViewModel> _stations = new();

        public StationListPanel()
        {
            InitializeComponent();
        }

        // ========== 数据更新 ==========
        private static void OnParsedDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (StationListPanel)d;
            control.UpdateData(e.NewValue as ParsedData);
        }

        private void UpdateData(ParsedData? data)
        {
            if (data == null)
            {
                _stations.Clear();
                dataGrid.ItemsSource = null;
                detailText.Text = "请编译代码获取站台数据";
                return;
            }

            // 构建 ViewModel 列表（迁移自 MainWindow.BuildStationViewModels）
            _stations = BuildStationViewModels(data);

            // 设置 ICollectionView 以便筛选
            _view = CollectionViewSource.GetDefaultView(_stations);
            dataGrid.ItemsSource = _view;
            ApplyFilter();

            // 默认选中第一个
            if (_stations.Any())
                dataGrid.SelectedIndex = 0;
            else
                detailText.Text = "暂无站台数据";
        }

        // ========== ViewModel 构建（从 MainWindow 移植） ==========
        private List<StationViewModel> BuildStationViewModels(ParsedData data)
        {
            var list = new List<StationViewModel>();
            var lineNameMap = new Dictionary<string, string>();
            foreach (var kv in data.Lines)
            {
                string lid = kv.Key;
                var lineInfo = kv.Value;
                string city = lineInfo.City ?? data.MainCity;
                string displayName = (city == data.MainCity) ? lineInfo.Name : $"{city}{lineInfo.Name}";
                lineNameMap[lid] = displayName;
            }

            foreach (var kv in data.Stations)
            {
                var st = kv.Value;
                var lineIds = data.Directions.Values
                    .Where(d => d.Stations.Contains(kv.Key))
                    .Select(d => d.LineId)
                    .Distinct()
                    .ToList();

                var lineDisplayNames = lineIds
                    .OrderBy(id => id)
                    .Select(id => lineNameMap.GetValueOrDefault(id, id))
                    .ToList();

                string city = data.StationCities.TryGetValue(kv.Key, out var c) ? c : data.MainCity;

                list.Add(new StationViewModel
                {
                    Name = kv.Key,
                    Lon = st.Lon,
                    Lat = st.Lat,
                    Lines = string.Join(", ", lineDisplayNames),
                    City = city
                });
            }
            return list;
        }

        // ========== 筛选逻辑 ==========
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
                if (item is StationViewModel st)
                {
                    return column switch
                    {
                        "Name" => st.Name.Contains(keyword),
                        "Lines" => st.Lines.Contains(keyword),
                        "City" => st.City.Contains(keyword),
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

        // ========== 选择与双击 ==========
        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dataGrid.SelectedItem is StationViewModel station)
            {
                StationSelected?.Invoke(this, station);
                UpdateDetail(station);
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dataGrid.SelectedItem is StationViewModel station)
                StationDoubleClicked?.Invoke(this, station);
        }

        // ========== 详情显示 ==========
        private void UpdateDetail(StationViewModel station)
        {
            if (ParsedData == null || !ParsedData.Stations.TryGetValue(station.Name, out var coord))
            {
                detailText.Text = "无法获取站台坐标";
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"站名: {station.Name}");
            sb.AppendLine($"城市: {station.City}");
            sb.AppendLine($"坐标: ({coord.Lon:F6}, {coord.Lat:F6})");

            // ---- 站点开通状态 ----
            var lineStatusIds = ParsedData.Directions.Values
                .Where(d => d.Stations.Contains(station.Name))
                .Select(d => d.LineId)
                .Distinct()
                .ToList();

            if (lineStatusIds.Any())
            {
                int total = lineStatusIds.Count;
                int pendingCount = 0;
                foreach (var lineId in lineStatusIds)
                {
                    // 查询 StationStatus，键为 (station.Name, lineId)
                    if (ParsedData.StationStatus.TryGetValue((station.Name, lineId), out var status))
                    {
                        if (status == "pending" || status == "PENDING" || status == "future" || status == "FUTURE") // 根据实际情况扩展
                            pendingCount++;
                    }
                    // 如果字典中没有该键，默认认为不是 pending（即正常开通）
                }

                string statusText;
                if (pendingCount == 0)
                    statusText = "全部开通";
                else if (pendingCount == total)
                    statusText = "全部线路暂缓开通";
                else
                    statusText = "部分线路暂缓开通";

                sb.AppendLine($"状态: {statusText}");
            }
            else
            {
                sb.AppendLine($"状态: 无线路");
            }

            // ---- 获取该站途经的线路 ID 列表 ----
            var lineIds = ParsedData.Directions.Values
                .Where(d => d.Stations.Contains(station.Name))
                .Select(d => d.LineId)
                .Distinct()
                .ToList();

            // ---- 构建线路名称映射 ----
            var lineNameMap = new Dictionary<string, string>();
            foreach (var kv in ParsedData.Lines)
            {
                string city = kv.Value.City ?? ParsedData.MainCity;
                string displayName = (city == ParsedData.MainCity) ? kv.Value.Name : $"{city}{kv.Value.Name}";
                lineNameMap[kv.Key] = displayName;
            }

            var lineDisplayNames = lineIds
                .OrderBy(id => id)
                .Select(id => lineNameMap.GetValueOrDefault(id, id))
                .ToList();

            // ---- 途经线路 ----
            if (lineDisplayNames.Any())
                sb.AppendLine($"途经线路: {string.Join(", ", lineDisplayNames)}");
            else
                sb.AppendLine($"途经线路: 无");

            // ---- 换乘信息 ----
            sb.AppendLine("换乘信息:");
            if (lineIds.Count < 2)
            {
                sb.AppendLine("  仅一条线路，无换乘");
            }
            else
            {
                double defaultTimeMin = ParsedData.DefaultInStationTime / 60.0; // 默认站内换乘时间（分钟）
                var explicitTransfers = ParsedData.Transfers?.Where(t => t.Station == station.Name).ToList();

                // 遍历所有线路对
                for (int i = 0; i < lineIds.Count; i++)
                {
                    for (int j = i + 1; j < lineIds.Count; j++)
                    {
                        string l1 = lineIds[i];
                        string l2 = lineIds[j];
                        string name1 = lineNameMap.GetValueOrDefault(l1, l1);
                        string name2 = lineNameMap.GetValueOrDefault(l2, l2);

                        // 查找换乘定义
                        var match = explicitTransfers?.FirstOrDefault(t =>
                            (t.L1 == l1 && t.L2 == l2) || (t.L1 == l2 && t.L2 == l1));

                        string status = match?.Status ?? "operating";
                        string statusDisplay = status switch
                        {
                            "operating" => "站内换乘",
                            "out_station" => "出站换乘",
                            "pending" => "暂缓开通",
                            _ => status
                        };

                        double timeMin = match?.Time.HasValue == true
                            ? match.Time.Value / 60.0
                            : defaultTimeMin;

                        sb.AppendLine($"  {name1} <-> {name2}: {statusDisplay}, {timeMin:F1} 分钟");
                    }
                }
            }

            // ---- 站内设施信息 ----
            AppendFacilities(sb, station.Name);

            // ---- 其他名称 ----
            if (ParsedData.StationOtherNames.TryGetValue(station.Name, out var otherNames) && otherNames.Any())
            {
                sb.AppendLine($"其他名称: {string.Join(", ", otherNames)}");
            }

            // ---- 虚拟换乘信息 ----
            var virtualTransfersForStation = ParsedData.VirtualTransfers
                .Where(vt => vt.StationA == station.Name || vt.StationB == station.Name)
                .ToList();

            if (virtualTransfersForStation.Any())
            {
                sb.AppendLine("虚拟换乘:");
                foreach (var vt in virtualTransfersForStation)
                {
                    string otherStation = vt.StationA == station.Name ? vt.StationB : vt.StationA;
                    string via = vt.ViaDirections.Any() ? $" (经由 {string.Join(", ", vt.ViaDirections)})" : "";
                    string security = vt.SecurityCheck ? "，需安检" : "";
                    sb.AppendLine($"  {station.Name} <-> {otherStation}: {vt.TimeMinutes:F1} 分钟{security}{via}");
                }
            }
            else
            {
                sb.AppendLine("虚拟换乘: 无");
            }

            detailText.Text = sb.ToString();
        }

        private void AppendFacilities(StringBuilder sb, string stationName)
        {
            if (ParsedData == null || !ParsedData.Facilities.TryGetValue(stationName, out var facilities))
            {
                sb.AppendLine("设施信息: 暂无");
                return;
            }

            sb.AppendLine("设施信息:");

            // 普通卫生间
            if (facilities.Toilet != null)
            {
                var t = facilities.Toilet;
                string pos = t.Position == "paid_area" ? "付费区" : "非付费区";
                string gender = t.Gender switch
                {
                    "male" => "男",
                    "female" => "女",
                    "mixed" => "通用",
                    _ => t.Gender
                };
                sb.AppendLine($"  卫生间: {pos}, {gender}, {(t.Accessible ? "无障碍" : "普通")}");
            }

            // 无障碍卫生间
            if (facilities.AccessibleToilet != null)
            {
                var at = facilities.AccessibleToilet;
                string pos = at.Position == "paid_area" ? "付费区" : "非付费区";
                sb.AppendLine($"  无障碍卫生间: {pos}, {at.Gender}");
            }

            // 电梯
            if (facilities.Elevators != null && facilities.Elevators.Any())
            {
                int count = facilities.Elevators.Count;
                bool accessible = facilities.Elevators.Any(e => e.Accessible);
                sb.AppendLine($"  垂直电梯: {count} 部{(accessible ? " (含无障碍)" : "")}");
                // 可展开显示具体位置，但简单起见只显示数量
            }

            // 宽闸机
            if (facilities.WideGate != null)
            {
                sb.AppendLine($"  宽通道闸机: {facilities.WideGate.Quantity} 台");
            }

            // 自动扶梯
            if (facilities.Escalators != null && facilities.Escalators.Any())
            {
                int total = facilities.Escalators.Sum(e => e.Quantity);
                sb.AppendLine($"  自动扶梯: {total} 部");
            }

            // 盲道
            if (facilities.TactilePaving != null)
            {
                string status = facilities.TactilePaving.Status == "available" ? "已铺设" : "未铺设";
                sb.AppendLine($"  盲道: {status}");
            }

            // 如果没有任何设施，显示“无”
            if (facilities.Toilet == null && facilities.AccessibleToilet == null &&
                (facilities.Elevators == null || !facilities.Elevators.Any()) &&
                facilities.WideGate == null &&
                (facilities.Escalators == null || !facilities.Escalators.Any()) &&
                facilities.TactilePaving == null)
            {
                sb.AppendLine("  无记录设施");
            }
        }

        // ========== 右键菜单 ==========
        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (dataGrid.SelectedItem is StationViewModel station)
                EditStationRequested?.Invoke(this, station);
        }

        private void AddMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AddStationRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    // ========== ViewModel 类（可从主窗口迁移） ==========
    public class StationViewModel
    {
        public string Name { get; set; } = string.Empty;
        public double Lon { get; set; }
        public double Lat { get; set; }
        public string Lines { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }
}