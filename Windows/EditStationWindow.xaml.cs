using MetroEditor.Windows.Utils;
using Microsoft.Win32;
using System;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 站点编辑器窗口
    /// 功能：新建或修改站点（NODE）定义
    /// 核心特性：
    /// - 支持经纬度手动输入
    /// - 集成高德地图API进行坐标校准
    /// - 自动生成 NODE ... COORD 代码
    /// - 支持跨城市站点定义
    /// </summary>
    public partial class EditStationWindow : Window
    {
        private GeocodeService _geocodeService; // 地理编码服务（高德API）
        private const string Indent = "  ";     // 代码缩进
        private string _defaultCity;            // 默认城市（主城市）
        private ManagerStationWindow.StationItem _station = null; // 编辑模式下的原站点数据

        // ===== 构造函数重载 =====
        /// <summary>
        /// 编辑模式构造函数
        /// </summary>
        public EditStationWindow(ManagerStationWindow.StationItem station, string defaultCity)
            : this(defaultCity)
        {
            _station = station;
            NameTextBox.Text = station.Name;
            LonTextBox.Text = station.Lon.ToString("F6");
            LatTextBox.Text = station.Lat.ToString("F6");
            CityTextBox.Text = station.City;
            Title = "编辑站点";
            UpdatePreview();
            LoadApiKey();
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 新建模式构造函数
        /// </summary>
        public EditStationWindow(string defaultCity)
        {
            InitializeComponent();
            _defaultCity = defaultCity;
            CityTextBox.Text = defaultCity;
            Title = "编辑站点";

            // 事件绑定：任何输入变化都更新预览
            NameTextBox.TextChanged += (s, e) => UpdatePreview();
            LonTextBox.TextChanged += (s, e) => UpdatePreview();
            LatTextBox.TextChanged += (s, e) => UpdatePreview();
            CityTextBox.TextChanged += (s, e) => UpdatePreview();

            UpdatePreview();
            LoadApiKey();
        }

        /// <summary>
        /// 更新代码预览区域
        /// </summary>
        private void UpdatePreview()
        {
            string name = NameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                PreviewTextBox.Text = "";
                return;
            }

            // 验证坐标格式
            if (!double.TryParse(LonTextBox.Text, out double lon) || !double.TryParse(LatTextBox.Text, out double lat))
            {
                PreviewTextBox.Text = "坐标无效";
                return;
            }

            string city = CityTextBox.Text.Trim();
            // 生成 NODE 代码行
            string line = $"{Indent}NODE '{name}' COORD {lon:F6}, {lat:F6}";
            // 非默认城市需添加 CITY 后缀
            if (!string.IsNullOrEmpty(city) && city != _defaultCity)
                line += $" CITY '{city}'";
            PreviewTextBox.Text = line;
        }

        /// <summary>
        /// 复制生成的代码到剪贴板
        /// </summary>
        private void CopyCode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PreviewTextBox.Text))
            {
                MessageBox.Show("请先完善站点信息", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                Clipboard.SetText(PreviewTextBox.Text);
                MessageBox.Show("节点代码已复制到剪贴板", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从注册表加载高德API Key（支持加密存储）
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
        /// 使用高德地图API校准坐标
        /// </summary>
        private async void AmapCalibrate_Click(object sender, RoutedEventArgs e)
        {
            if (_geocodeService == null)
            {
                MessageBox.Show("未配置高德 API Key，请先在主程序设置中配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string name = NameTextBox.Text.Trim();
            string city = CityTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入站名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 调用地理编码API（自动添加"地铁站"后缀提高准确率）
                var result = await _geocodeService.GetLocationAsync(name + "地铁站", city, true);
                LonTextBox.Text = result.Lng.ToString("F6");
                LatTextBox.Text = result.Lat.ToString("F6");
                // 若城市为空，则使用API返回的格式化地址
                if (string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(result.FormattedAddress))
                    CityTextBox.Text = result.FormattedAddress;
                MessageBox.Show($"校准成功：{result.FormattedAddress}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"校准失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 确认保存按钮点击事件
        /// </summary>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("站名不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 验证经度
            if (!double.TryParse(LonTextBox.Text, out double lon) || lon < -180 || lon > 180)
            {
                MessageBox.Show("经度格式错误或超出范围 (-180~180)", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 验证纬度
            if (!double.TryParse(LatTextBox.Text, out double lat) || lat < -90 || lat > 90)
            {
                MessageBox.Show("纬度格式错误或超出范围 (-90~90)", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 更新或创建站点对象
            if (_station == null)
                _station = new ManagerStationWindow.StationItem();
            _station.Name = name;
            _station.Lon = lon;
            _station.Lat = lat;
            _station.City = CityTextBox.Text.Trim();

            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>
        /// 获取编辑后的站点对象（供父窗口调用）
        /// </summary>
        public ManagerStationWindow.StationItem GetStation() => _station;
    }
}