using MetroEditor.Windows.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MessageBox = System.Windows.MessageBox;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 站点管理器窗口
    /// 功能：批量管理站点定义（NODE ... COORD）
    /// 核心特性：
    /// 1. 批量添加站点（支持空格/逗号/换行分隔）
    /// 2. 批量坐标校准（基于高德地图API）
    /// 3. 实时生成STATION_DEFINE代码
    /// 4. 计算站点群中心坐标
    /// </summary>
    /// <remarks>
    /// 应用场景：
    /// - 新城市路网初始化：快速录入大量站点
    /// - 数据纠错：批量修正坐标偏差
    /// - 数据维护：统一管理站点定义代码
    /// 
    /// 技术亮点：
    /// - 异步API调用：避免UI阻塞
    /// - 加密存储API Key：保护用户隐私
    /// - 双向数据绑定：UI操作实时反映到数据模型
    /// </remarks>
    public partial class ManagerStationWindow : Window
    {
        /// <summary>
        /// 默认城市（用于生成CITY后缀）
        /// </summary>
        public string DefaultCity { get; set; } = "广州";

        /// <summary>
        /// 站点数据模型
        /// 实现INotifyPropertyChanged接口，支持UI实时更新
        /// </summary>
        public class StationItem : INotifyPropertyChanged
        {
            private string _name;
            private double _lon;
            private double _lat;
            private string _city;
            private bool _isSelected;
            private string _calibrationStatus; // 校准状态提示
            private string _searchResult;      // 搜索预览结果（地址+坐标或失败原因）
            private List<string> _otherNames = new(); // 其他语言名称（OTHER_NAME）

            public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
            public double Lon { get => _lon; set { _lon = value; OnPropertyChanged(); } }
            public double Lat { get => _lat; set { _lat = value; OnPropertyChanged(); } }
            public string City { get => _city; set { _city = value; OnPropertyChanged(); } }
            public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
            public string CalibrationStatus { get => _calibrationStatus; set { _calibrationStatus = value; OnPropertyChanged(); } }
            public string SearchResult { get => _searchResult; set { _searchResult = value; OnPropertyChanged(); } }

            /// <summary>其他语言名称列表（对应 DSL 的 OTHER_NAME）</summary>
            public List<string> OtherNames
            {
                get => _otherNames;
                set { _otherNames = value ?? new List<string>(); OnPropertyChanged(); OnPropertyChanged(nameof(OtherNamesText)); }
            }

            /// <summary>其他语言名称的展示文本（逗号连接），用于 DataGrid 只读列</summary>
            public string OtherNamesText => string.Join(", ", _otherNames);

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = "") =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private ObservableCollection<StationItem> _stations = new(); // 站点集合（绑定到DataGrid）
        private ICollectionView _stationsView; // 站点过滤视图（站名筛选）
        private string _filterText = "";       // 当前筛选关键词
        private GeocodeService _geocodeService; // 地理编码服务（高德API）
        private const string Indent = "  "; // 代码缩进

        /// <summary>
        /// 搜索结果缓存（键：站名）。
        /// 先"搜索预览"再"高德修正"，修正时命中缓存即可避免重复调用高德API。
        /// </summary>
        private readonly Dictionary<string, (double Lng, double Lat, string Address)> _searchCache = new();

        public ManagerStationWindow()
        {
            InitializeComponent();
            _stationsView = CollectionViewSource.GetDefaultView(_stations);
            _stationsView.Filter = FilterStation;
            StationDataGrid.ItemsSource = _stationsView;
            LoadApiKey(); // 加载加密的API Key
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 从外部注入站点数据（用于编辑模式）
        /// </summary>
        public void SetStationData(List<StationItem> stationItems)
        {
            _stations = new ObservableCollection<StationItem>(stationItems);
            // 重新绑定过滤视图（新集合需要重新取视图）
            _stationsView = CollectionViewSource.GetDefaultView(_stations);
            _stationsView.Filter = FilterStation;
            StationDataGrid.ItemsSource = _stationsView;
        }

        /// <summary>
        /// 加载高德地图API Key（支持DPAPI加密）
        /// </summary>
        private void LoadApiKey()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\MetroEditor");
            string apiKey = key?.GetValue("ApiKey")?.ToString();
            if (!string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    // 尝试解密（支持DPAPI加密的密钥）
                    string decrypted = SecureStorageHelper.Decrypt(apiKey);
                    _geocodeService = new GeocodeService(decrypted);
                }
                catch
                {
                    // 解密失败可能是旧明文格式，直接尝试
                    _geocodeService = new GeocodeService(apiKey);
                }
            }
        }

        /// <summary>
        /// 添加单个站点（供外部调用）
        /// </summary>
        public void AddStation(StationItem station)
        {
            if (station == null) return;
            _stations.Add(station);
            UpdatePreview();
        }

        #region 批量操作
        /// <summary>
        /// 批量添加站点按钮点击事件
        /// </summary>
        private void BatchAdd_Click(object sender, RoutedEventArgs e)
        {
            string input = BatchInputTextBox.Text;
            if (string.IsNullOrWhiteSpace(input)) return;

            // 支持多种分隔符：空格、逗号、换行
            var names = input.Split(new[] { ' ', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(s => s.Trim())
                             .Where(s => !string.IsNullOrEmpty(s));

            foreach (var name in names)
            {
                // 去重检查
                if (_stations.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                _stations.Add(new StationItem { Name = name, Lon = 0, Lat = 0, City = DefaultCity });
            }
            BatchInputTextBox.Clear();
            UpdatePreview();
        }

        /// <summary>
        /// 删除选中站点按钮点击事件
        /// </summary>
        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = StationDataGrid.SelectedItems.Cast<StationItem>().ToList();
            foreach (var item in selected)
                _stations.Remove(item);
            UpdatePreview();
        }

        /// <summary>
        /// 编辑站点按钮点击事件：打开编辑窗口（支持其他语言名称），保存后刷新列表与预览
        /// </summary>
        private void EditSelected_Click(object sender, RoutedEventArgs e)
        {
            var item = StationDataGrid.SelectedItem as StationItem;
            if (item == null)
            {
                MessageBox.Show("请先在列表中选择一个站点", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new EditStationWindow(item, DefaultCity)
            {
                Owner = this
            };
            if (dlg.ShowDialog() == true)
            {
                var updated = dlg.GetStation();
                if (updated != null)
                {
                    item.Name = updated.Name;
                    item.Lon = updated.Lon;
                    item.Lat = updated.Lat;
                    item.City = updated.City;
                    item.OtherNames = updated.OtherNames;
                }
                UpdatePreview();
            }
        }

        /// <summary>
        /// 清空所有站点按钮点击事件
        /// </summary>
        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            _stations.Clear();
            UpdatePreview();
        }

        /// <summary>
        /// 计算站点群中心坐标按钮点击事件
        /// </summary>
        private void CalculateCenter_Click(object sender, RoutedEventArgs e)
        {
            if (_stations.Count == 0)
                CenterCoordDisplay.Text = "无站点";
            else
            {
                // 算术平均计算中心点
                double avgLon = _stations.Average(s => s.Lon);
                double avgLat = _stations.Average(s => s.Lat);
                CenterCoordDisplay.Text = $"{avgLon:F6}, {avgLat:F6}";
            }
        }

        /// <summary>
        /// 站名筛选谓词（忽略大小写、包含匹配）
        /// </summary>
        private bool FilterStation(object obj)
        {
            if (string.IsNullOrEmpty(_filterText)) return true;
            return obj is StationItem s && s.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 筛选输入变化时实时刷新列表，并更新匹配计数提示
        /// </summary>
        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filterText = FilterTextBox.Text.Trim();
            _stationsView?.Refresh();
            FilterCountText.Text = string.IsNullOrEmpty(_filterText)
                ? ""
                : $"匹配 {_stationsView.Cast<object>().Count()} / {_stations.Count}";
        }

        /// <summary>
        /// 清除筛选条件
        /// </summary>
        private void ClearFilter_Click(object sender, RoutedEventArgs e) => FilterTextBox.Clear();

        /// <summary>
        /// 批量勾选：按多行/分隔符输入站名，在全部站点中匹配并勾选
        /// </summary>
        private void QuickCheck_Click(object sender, RoutedEventArgs e)
        {
            string input = QuickCheckTextBox.Text;
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("请先输入要勾选的站名（空格、逗号或换行分隔）", "快速勾选", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 支持多种分隔符：空格、逗号、换行、Tab，并去重
            var names = input.Split(new[] { ' ', ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(s => s.Trim())
                             .Where(s => !string.IsNullOrEmpty(s))
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .ToList();

            int matched = 0;
            var notFound = new List<string>();
            foreach (var name in names)
            {
                var found = _stations.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    found.IsSelected = true;
                    matched++;
                }
                else
                {
                    notFound.Add(name);
                }
            }

            QuickCheckTextBox.Clear();
            string msg = $"已勾选 {matched} 个站点";
            if (notFound.Count > 0)
                msg += $"\n未找到：{string.Join("、", notFound)}";
            MessageBox.Show(msg, "快速勾选", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 取消全部站点的勾选状态
        /// </summary>
        private void UncheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var s in _stations) s.IsSelected = false;
        }
        #endregion

        #region 代码生成
        /// <summary>
        /// 生成STATION_DEFINE代码块
        /// </summary>
        /// <returns>符合MetroLang语法的站点定义代码</returns>
        private string GenerateCode()
        {
            if (_stations.Count == 0) return "";
            var sb = new StringBuilder();
            sb.AppendLine("STATION_DEFINE");
            foreach (var s in _stations)
            {
                string line = $"{Indent}NODE '{s.Name}' COORD {s.Lon:F6}, {s.Lat:F6}";
                // 非默认城市添加CITY后缀
                if (!string.IsNullOrEmpty(s.City) && s.City != DefaultCity)
                    line += $" CITY '{s.City}'";
                // 其他语言名称 → OTHER_NAME ['a', 'b']
                var otherNames = s.OtherNames?.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList() ?? new List<string>();
                if (otherNames.Count > 0)
                    line += $" OTHER_NAME [{string.Join(", ", otherNames.Select(n => $"'{n}'"))}]";
                sb.AppendLine(line);
            }
            sb.AppendLine("END_DEFINE");
            return sb.ToString();
        }

        /// <summary>
        /// 更新代码预览区域
        /// </summary>
        private void UpdatePreview() => PreviewTextBox.Text = GenerateCode();

        /// <summary>
        /// 复制生成的代码到剪贴板
        /// </summary>
        private void CopyCode_Click(object sender, RoutedEventArgs e)
        {
            string code = PreviewTextBox.Text;
            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("没有可复制的代码", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                MainWindow.InsertText(code);
                MessageBox.Show("代码已复制到主界面", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
                MessageBox.Show("已复制到剪贴板", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region 坐标校准
        /// <summary>
        /// 加载单个站点（用于编辑模式）
        /// </summary>
        public void LoadStation(string name, double lon, double lat)
        {
            _stations.Clear();
            _stations.Add(new StationItem { Name = name, Lon = lon, Lat = lat, City = DefaultCity });
            UpdatePreview();
            CalculateCenter_Click(null, null);
        }

        /// <summary>
        /// 格式化搜索结果用于"搜索结果"列展示
        /// </summary>
        private static string FormatSearchResult(string address, double lng, double lat) =>
            $"{address} ({lng:F6}, {lat:F6})";

        /// <summary>
        /// 搜索单个站点（站名 + "地铁站"后缀提高准确率），返回是否成功及结果
        /// </summary>
        private async Task<(bool Ok, double Lng, double Lat, string Message)> SearchStationAsync(StationItem item)
        {
            try
            {
                var result = await _geocodeService.GetLocationAsync(item.Name + "地铁站", item.City, true);
                return (true, result.Lng, result.Lat, result.FormattedAddress);
            }
            catch (Exception ex)
            {
                return (false, 0, 0, $"失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将搜索结果写入缓存与"搜索结果"列（失败时记录原因）
        /// </summary>
        private void RecordSearchResult(StationItem item, bool ok, double lng, double lat, string message)
        {
            if (ok)
            {
                _searchCache[item.Name] = (lng, lat, message);
                item.SearchResult = FormatSearchResult(message, lng, lat);
            }
            else
            {
                item.SearchResult = message;
            }
        }

        /// <summary>
        /// 搜索预览按钮点击事件
        /// 对勾选站点调用高德API搜索，将结果（地址+坐标）展示在"搜索结果"列并缓存，
        /// 不直接写回坐标，供用户确认后再点"高德修正"应用。已缓存站点跳过，减少搜索次数。
        /// </summary>
        private async void SearchPreview_Click(object sender, RoutedEventArgs e)
        {
            if (_geocodeService == null)
            {
                MessageBox.Show("未配置高德 API Key，请先在设置中配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = _stations.Where(s => s.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("请先勾选需要搜索的站点", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var todo = selected.Where(s => !_searchCache.ContainsKey(s.Name)).ToList();
            if (todo.Count == 0)
            {
                MessageBox.Show("所有勾选站点已有搜索结果缓存，无需重复搜索。可直接点击\"高德修正\"应用坐标。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 显示进度条（仅统计实际发起的网络请求）
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.Maximum = todo.Count;
            ProgressBar.Value = 0;

            int done = 0, fail = 0;
            foreach (var item in todo)
            {
                var r = await SearchStationAsync(item);
                RecordSearchResult(item, r.Ok, r.Lng, r.Lat, r.Message);
                if (!r.Ok) fail++;
                ProgressBar.Value = ++done;
            }

            ProgressBar.Visibility = Visibility.Collapsed;
            int cached = selected.Count - todo.Count;
            string cachedHint = cached > 0 ? $"，另有 {cached} 个命中缓存未重新搜索" : "";
            MessageBox.Show($"搜索完成：{todo.Count} 个站点，成功 {todo.Count - fail} 个，失败 {fail} 个{cachedHint}",
                "搜索预览", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 高德地图批量校准按钮点击事件
        /// 优先使用"搜索预览"的缓存结果，未命中缓存的站点才现场搜索（避免重复调用API）
        /// </summary>
        private async void AmapCorrect_Click(object sender, RoutedEventArgs e)
        {
            if (_geocodeService == null)
            {
                MessageBox.Show("未配置高德 API Key，请先在设置中配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = _stations.Where(s => s.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("请先勾选需要修正的站点", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 显示进度条
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.Maximum = selected.Count;
            ProgressBar.Value = 0;

            int success = 0, searched = 0;
            for (int i = 0; i < selected.Count; i++)
            {
                var item = selected[i];
                try
                {
                    // 优先使用缓存结果；未命中才调用高德API搜索
                    if (!_searchCache.TryGetValue(item.Name, out var cached))
                    {
                        var r = await SearchStationAsync(item);
                        if (!r.Ok)
                        {
                            item.CalibrationStatus = r.Message;
                            ProgressBar.Value = i + 1;
                            continue;
                        }
                        cached = (r.Lng, r.Lat, r.Message);
                        _searchCache[item.Name] = cached;
                        item.SearchResult = FormatSearchResult(r.Message, r.Lng, r.Lat);
                        searched++;
                    }
                    item.Lon = cached.Lng;
                    item.Lat = cached.Lat;
                    item.CalibrationStatus = "成功";
                    success++;
                }
                catch (Exception ex)
                {
                    item.CalibrationStatus = $"失败: {ex.Message}";
                }
                ProgressBar.Value = i + 1;
            }

            // 隐藏进度条，显示结果
            ProgressBar.Visibility = Visibility.Collapsed;
            string searchHint = searched > 0 ? $"，本次新搜索 {searched} 个，其余命中缓存" : "，全部命中缓存未重复搜索";
            MessageBox.Show($"修正完成，成功 {success} 个站点{searchHint}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);

            UpdatePreview();
            CalculateCenter_Click(null, null);
        }
        #endregion

        /// <summary>
        /// 关闭窗口
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>
        /// 单元格编辑结束时更新预览
        /// </summary>
        private void StationDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // 编辑完成后调用更新
            UpdatePreview();
        }
    }
}