using MetroEditor.Windows.Utils;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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

            public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
            public double Lon { get => _lon; set { _lon = value; OnPropertyChanged(); } }
            public double Lat { get => _lat; set { _lat = value; OnPropertyChanged(); } }
            public string City { get => _city; set { _city = value; OnPropertyChanged(); } }
            public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
            public string CalibrationStatus { get => _calibrationStatus; set { _calibrationStatus = value; OnPropertyChanged(); } }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = "") =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private ObservableCollection<StationItem> _stations = new(); // 站点集合（绑定到DataGrid）
        private GeocodeService _geocodeService; // 地理编码服务（高德API）
        private const string Indent = "  "; // 代码缩进

        public ManagerStationWindow()
        {
            InitializeComponent();
            StationDataGrid.ItemsSource = _stations;
            LoadApiKey(); // 加载加密的API Key
        }

        /// <summary>
        /// 从外部注入站点数据（用于编辑模式）
        /// </summary>
        public void SetStationData(List<StationItem> stationItems) =>
            _stations = new ObservableCollection<StationItem>(stationItems);

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
                Clipboard.SetText(code);
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
        /// 高德地图批量校准按钮点击事件
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

            var updateDict = new Dictionary<string, (double Lon, double Lat)>();
            int success = 0;

            // 逐个校准（避免API限流）
            for (int i = 0; i < selected.Count; i++)
            {
                var item = selected[i];
                try
                {
                    // 调用高德API（自动添加"地铁站"后缀提高准确率）
                    var result = await _geocodeService.GetLocationAsync(item.Name + "地铁站", item.City, true);
                    item.Lon = result.Lng;
                    item.Lat = result.Lat;
                    item.CalibrationStatus = "成功";
                    updateDict[item.Name] = (result.Lng, result.Lat);
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
            MessageBox.Show($"修正完成，成功 {success} 个站点", "完成", MessageBoxButton.OK, MessageBoxImage.Information);

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