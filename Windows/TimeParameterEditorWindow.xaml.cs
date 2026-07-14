using MetroEditor.MetroLang.DataModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 时间参数编辑器窗口
    /// 功能：可视化配置影响路径计算的时间类参数
    /// 核心配置项：
    /// 1. 全局默认时间（进站、停站、换乘等）
    /// 2. 个性化换乘规则（特定车站的特定线路间换乘）
    /// 3. 个性化停站时间（特定车站在特定线路上的停靠时长）
    /// </summary>
    /// <remarks>
    /// 业务背景：
    /// - 换乘时间受通道长度、客流密度影响，不能一概而论
    /// - 停站时间受站台规模、客流量影响（如换乘站通常停站更长）
    /// - 这些参数直接影响路径规划的结果（换乘少的路径可能因换乘时间长而不优）
    /// 
    /// 技术特点：
    /// - 双向绑定：UI修改实时同步到ViewModel
    /// - 代码生成：实时生成符合MetroLang语法的配置代码
    /// - 单位转换：UI显示分钟，内部存储秒（符合MetroLang规范）
    /// </remarks>
    public partial class TimeParameterEditorWindow : Window
    {
        private readonly ParsedData _data;                                  // 路网数据（只读）
        private readonly ObservableCollection<TransferRuleViewModel> _transfers = new(); // 换乘规则集合
        private readonly ObservableCollection<DwellTimeViewModel> _dwellTimes = new();   // 停站时间集合
        private readonly Dictionary<string, string> _lineNameMap;           // 线路ID->显示名称映射
        private readonly List<string> _stationNames;                       // 所有站点名称
        private readonly List<string> _lineIds;                            // 所有线路ID
        private readonly ObservableCollection<string> _lineDisplayNames = new(); // 线路显示名称集合

        /// <summary>
        /// 暴露给XAML绑定的线路显示名称集合
        /// </summary>
        public ObservableCollection<string> LineNames => _lineDisplayNames;

        /// <summary>
        /// 构造函数：初始化时间参数编辑器
        /// </summary>
        /// <param name="data">解析后的路网数据</param>
        public TimeParameterEditorWindow(ParsedData data)
        {
            InitializeComponent();
            _data = data;

            // 获取所有站点并按名称排序
            _stationNames = data.Stations.Keys.OrderBy(x => x).ToList();
            // 获取所有线路并按ID排序
            _lineIds = data.Lines.Keys.OrderBy(x => x).ToList();

            // ---------- 构建线路ID到显示名称的映射 ----------
            // 跨城市线路添加城市前缀，便于区分
            _lineNameMap = data.Lines.ToDictionary(
                kv => kv.Key,
                kv =>
                {
                    string city = kv.Value.City ?? data.MainCity;
                    return (city == data.MainCity) ? kv.Value.Name : $"{city}{kv.Value.Name}";
                }
            );
            // 填充线路显示名称集合（用于下拉框）
            foreach (var name in _lineNameMap.Values.OrderBy(x => x))
                _lineDisplayNames.Add(name);

            // ---------- 加载全局默认时间参数 ----------
            // 注意：UI显示分钟，内部存储秒，这里做转换
            InStationTimeBox.Text = data.DefaultInStationTime.ToString("F2");
            OutStationTimeBox.Text = data.DefaultOutStationTime.ToString("F2");
            DwellTimeBox.Text = data.DefaultDwellTime.ToString("F2");
            EntryTimeBox.Text = data.DefaultEntryTime.ToString("F2");

            // ---------- 加载已有的换乘规则 ----------
            foreach (var tr in data.Transfers)
            {
                var vm = new TransferRuleViewModel
                {
                    Station = tr.Station,
                    L1 = tr.L1,
                    L2 = tr.L2,
                    Status = tr.Status,
                    // 内部存储秒，UI显示分钟
                    TimeMin = tr.Time.HasValue ? tr.Time.Value / 60.0 : data.DefaultInStationTime
                };
                vm.SetLineNameMap(_lineNameMap); // 注入映射，用于显示友好名称
                _transfers.Add(vm);
            }
            TransferGrid.ItemsSource = _transfers;

            // ---------- 加载已有的停站时间规则 ----------
            foreach (var st in data.DwellTimes)
            {
                var vm = new DwellTimeViewModel
                {
                    Station = st.Key.Station,
                    Line = st.Key.LineId,
                    TimeMin = st.Value // 已经是分钟
                };
                vm.SetLineNameMap(_lineNameMap);
                _dwellTimes.Add(vm);
            }
            DwellTimeGrid.ItemsSource = _dwellTimes;

            // 设置DataContext为自身，使LineNames绑定生效
            DataContext = this;

            // 初始化代码预览
            RefreshPreview();
        }

        #region 换乘规则操作
        /// <summary>
        /// 添加换乘规则按钮点击事件
        /// </summary>
        private void AddTransfer_Click(object sender, RoutedEventArgs e)
        {
            // 弹出换乘规则编辑对话框
            var dialog = new AddTransferDialog(_stationNames, _lineIds, _lineNameMap);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                var vm = new TransferRuleViewModel
                {
                    Station = dialog.SelectedStation,
                    L1 = dialog.SelectedLine1,
                    L2 = dialog.SelectedLine2,
                    Status = dialog.SelectedStatus,
                    TimeMin = dialog.TimeMin
                };
                vm.SetLineNameMap(_lineNameMap);
                _transfers.Add(vm);
                RefreshPreview(); // 更新代码预览
            }
        }

        /// <summary>
        /// 删除选中的换乘规则
        /// </summary>
        private void DeleteTransfer_Click(object sender, RoutedEventArgs e)
        {
            var selected = TransferGrid.SelectedItems.Cast<TransferRuleViewModel>().ToList();
            foreach (var item in selected)
                _transfers.Remove(item);
            RefreshPreview();
        }
        #endregion

        #region 停站时间操作
        /// <summary>
        /// 添加停站时间规则按钮点击事件
        /// </summary>
        private void AddDwellTime_Click(object sender, RoutedEventArgs e)
        {
            // 弹出停站时间编辑对话框
            var dialog = new AddDwellTimeDialog(_stationNames, _lineIds, _lineNameMap);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                var vm = new DwellTimeViewModel
                {
                    Station = dialog.SelectedStation,
                    Line = dialog.SelectedLine,
                    TimeMin = dialog.TimeMin
                };
                vm.SetLineNameMap(_lineNameMap);
                _dwellTimes.Add(vm);
                RefreshPreview();
            }
        }

        /// <summary>
        /// 删除选中的停站时间规则
        /// </summary>
        private void DeleteDwellTime_Click(object sender, RoutedEventArgs e)
        {
            var selected = DwellTimeGrid.SelectedItems.Cast<DwellTimeViewModel>().ToList();
            foreach (var item in selected)
                _dwellTimes.Remove(item);
            RefreshPreview();
        }
        #endregion

        #region 代码预览与生成
        /// <summary>
        /// 手动刷新代码预览按钮点击事件
        /// </summary>
        private void RefreshPreview_Click(object sender, RoutedEventArgs e) => RefreshPreview();

        /// <summary>
        /// 生成符合MetroLang语法的配置代码
        /// </summary>
        /// <remarks>
        /// 生成逻辑：
        /// 1. OPERATION_DEFINE：定义全局默认时间参数
        /// 2. TRANSFER_DEFINE：定义特定换乘规则（覆盖全局默认）
        /// 3. DWELL_TIME：定义特定停站时间（覆盖全局默认）
        /// 
        /// 单位转换：UI中的分钟 -> 代码中的秒
        /// </remarks>
        private void RefreshPreview()
        {
            var sb = new StringBuilder();

            // ---------- 1. 全局默认时间参数 ----------
            if (double.TryParse(InStationTimeBox.Text, out double inT) &&
                double.TryParse(OutStationTimeBox.Text, out double outT) &&
                double.TryParse(DwellTimeBox.Text, out double dwellT) &&
                double.TryParse(EntryTimeBox.Text, out double entryT))
            {
                sb.AppendLine("OPERATION_DEFINE");
                sb.AppendLine($"    TRANSFER_TIME {(int)(inT * 60)}");       // 分钟转秒
                sb.AppendLine($"    OUT_STATION_TRANSFER_TIME {(int)(outT * 60)}");
                sb.AppendLine($"    DWELL_TIME {(int)(dwellT * 60)}");
                sb.AppendLine($"    ENTRY_TIME {(int)(entryT * 60)}");
                sb.AppendLine("END_DEFINE");
                sb.AppendLine();
            }

            // ---------- 2. 换乘规则 ----------
            foreach (var tr in _transfers)
            {
                sb.AppendLine($"TRANSFER_DEFINE '{tr.Station}' {tr.L1} {tr.L2}");
                string status = tr.Status.ToLowerInvariant(); // 确保小写
                sb.AppendLine($"    STATUS {status}");
                sb.AppendLine($"    TIME {(int)(tr.TimeMin * 60)}");   // 分钟转秒
                sb.AppendLine("END_DEFINE");
            }
            if (_transfers.Any()) sb.AppendLine();

            // ---------- 3. 停站时间规则 ----------
            foreach (var st in _dwellTimes)
                sb.AppendLine($"DWELL_TIME '{st.Station}' {st.Line} {st.TimeMin:F2}");

            PreviewTextBox.Text = sb.ToString().TrimEnd();
        }
        #endregion

        #region 辅助功能
        /// <summary>
        /// 复制生成的代码到剪贴板
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
                Clipboard.SetText(PreviewTextBox.Text);
                MessageBox.Show("代码已复制到剪贴板", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        #endregion

        #region ViewModel定义（用于数据绑定）
        /// <summary>
        /// 换乘规则视图模型
        /// 职责：桥接UI显示和内部数据存储
        /// </summary>
        public class TransferRuleViewModel : INotifyPropertyChanged
        {
            // 状态枚举的显示映射
            private static readonly Dictionary<string, string> StatusToDisplay = new()
            {
                ["operating"] = "站内换乘",
                ["out_station"] = "出站换乘",
                ["pending"] = "暂缓开通"
            };
            private static readonly Dictionary<string, string> DisplayToStatus =
                StatusToDisplay.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            private string _station;
            private string _l1;
            private string _l2;
            private string _status;
            private double _timeMin;
            private Dictionary<string, string> _lineNameMap;

            /// <summary>
            /// 注入线路名称映射，用于显示友好名称
            /// </summary>
            public void SetLineNameMap(Dictionary<string, string> map) => _lineNameMap = map;

            // ---------- 原始属性（用于代码生成）----------
            public string Station { get => _station; set { _station = value; OnPropertyChanged(); } }
            public string L1 { get => _l1; set { _l1 = value; OnPropertyChanged(); } }
            public string L2 { get => _l2; set { _l2 = value; OnPropertyChanged(); } }
            public string Status
            {
                get => _status;
                set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); }
            }
            public double TimeMin { get => _timeMin; set { _timeMin = value; OnPropertyChanged(); } }

            // ---------- 显示属性（用于UI绑定）----------
            /// <summary>
            /// 状态显示名称（如"站内换乘"）
            /// </summary>
            public string StatusDisplay
            {
                get => StatusToDisplay.TryGetValue(_status, out var display) ? display : _status;
                set
                {
                    if (DisplayToStatus.TryGetValue(value, out var status))
                        Status = status;
                }
            }

            /// <summary>
            /// 线路1的显示名称
            /// </summary>
            public string DisplayLine1 => _lineNameMap != null && _lineNameMap.TryGetValue(L1, out var name) ? name : L1;

            /// <summary>
            /// 线路2的显示名称
            /// </summary>
            public string DisplayLine2 => _lineNameMap != null && _lineNameMap.TryGetValue(L2, out var name) ? name : L2;

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = "") =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 停站时间视图模型
        /// </summary>
        public class DwellTimeViewModel : INotifyPropertyChanged
        {
            private string _station;
            private string _line;
            private double _timeMin;
            private Dictionary<string, string> _lineNameMap;
            private Dictionary<string, string> _displayToLineMap = new(); // 显示名称->ID的逆向映射

            public void SetLineNameMap(Dictionary<string, string> map)
            {
                _lineNameMap = map;
                // 构建逆向映射，用于从显示名称查找ID
                _displayToLineMap = map.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            }

            public string Station { get => _station; set { _station = value; OnPropertyChanged(); } }

            /// <summary>
            /// 线路ID（内部存储）
            /// </summary>
            public string Line
            {
                get => _line;
                set
                {
                    if (_line != value)
                    {
                        _line = value;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(DisplayLine)); // 通知显示属性更新
                    }
                }
            }

            /// <summary>
            /// 线路显示名称（用于UI绑定）
            /// </summary>
            public string DisplayLine
            {
                get => _lineNameMap != null && _lineNameMap.TryGetValue(Line, out var name) ? name : Line;
                set
                {
                    if (string.IsNullOrEmpty(value)) return;
                    string newLine = value;
                    // 尝试从显示名称查找ID
                    if (_displayToLineMap.TryGetValue(value, out var id))
                        newLine = id;
                    else
                    {
                        // 手动查找（兜底逻辑）
                        var pair = _lineNameMap?.FirstOrDefault(kvp => kvp.Value == value);
                        if (pair.HasValue && !pair.Value.Equals(default(KeyValuePair<string, string>)))
                            newLine = pair.Value.Key;
                    }
                    if (Line != newLine)
                        Line = newLine;
                }
            }

            public double TimeMin { get => _timeMin; set { _timeMin = value; OnPropertyChanged(); } }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = "") =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}