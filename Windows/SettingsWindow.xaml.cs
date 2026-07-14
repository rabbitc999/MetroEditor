using MetroEditor.Windows.Utils;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 设置窗口
    /// 功能：配置应用程序的全局参数
    /// 核心配置项：
    /// 1. 编辑器外观（字体、字号）
    /// 2. 路径搜索权重（影响路径规划结果）
    /// 3. 高德地图API Key（用于坐标校准）
    /// </summary>
    /// <remarks>
    /// 设计特点：
    /// - 持久化存储：使用Windows注册表保存配置
    /// - 安全存储：API Key使用DPAPI加密
    /// - 实时预览：滑块移动时实时更新数值显示
    /// - 默认值：首次运行时使用合理的默认值
    /// 
    /// 权重配置说明：
    /// 权重值越大，算法越倾向于优化该指标。
    /// 例如：TransferWeight=10表示非常讨厌换乘，宁愿绕远路也要少换乘。
    /// </remarks>
    public partial class SettingsWindow : Window
    {
        // 路径搜索权重属性（供外部访问）
        public double LengthWeight { get; private set; } = 0.02;
        public double TransferWeight { get; private set; } = 0.5;
        public double MoneyWeight { get; private set; } = 1.0;
        public double TimeWeight { get; private set; } = 0.01;
        public double StationWeight { get; private set; } = 0.1;
        public double SpecialWeight { get; private set; } = 1.0;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();          // 加载已有设置
            LoadFonts();            // 加载系统字体
            RegisterSliderEvents(); // 注册滑块事件
            UpdateAllDisplays();    // 初始化显示
        }

        #region UI初始化
        /// <summary>
        /// 注册所有滑块的值变化事件
        /// </summary>
        private void RegisterSliderEvents()
        {
            // 字体大小
            FontSizeSlider.ValueChanged += (s, e) => FontSizeDisplay.Text = ((int)e.NewValue).ToString();

            // 路径权重
            LengthWeightSlider.ValueChanged += (s, e) => LengthWeightDisplay.Text = e.NewValue.ToString("F2");
            TransferWeightSlider.ValueChanged += (s, e) => TransferWeightDisplay.Text = e.NewValue.ToString("F1");
            MoneyWeightSlider.ValueChanged += (s, e) => MoneyWeightDisplay.Text = e.NewValue.ToString("F1");
            TimeWeightSlider.ValueChanged += (s, e) => TimeWeightDisplay.Text = e.NewValue.ToString("F3");
            StationWeightSlider.ValueChanged += (s, e) => StationWeightDisplay.Text = e.NewValue.ToString("F2");
            SpecialWeightSlider.ValueChanged += (s, e) => SpecialWeightDisplay.Text = e.NewValue.ToString("F1");
        }

        /// <summary>
        /// 更新所有显示标签的文本内容
        /// </summary>
        private void UpdateAllDisplays()
        {
            FontSizeDisplay.Text = ((int)FontSizeSlider.Value).ToString();
            LengthWeightDisplay.Text = LengthWeightSlider.Value.ToString("F2");
            TransferWeightDisplay.Text = TransferWeightSlider.Value.ToString("F1");
            MoneyWeightDisplay.Text = MoneyWeightSlider.Value.ToString("F1");
            TimeWeightDisplay.Text = TimeWeightSlider.Value.ToString("F3");
            StationWeightDisplay.Text = StationWeightSlider.Value.ToString("F2");
            SpecialWeightDisplay.Text = SpecialWeightSlider.Value.ToString("F1");
        }

        /// <summary>
        /// 加载系统字体列表到下拉框
        /// </summary>
        private void LoadFonts()
        {
            var fonts = new ObservableCollection<string>();
            foreach (var family in Fonts.SystemFontFamilies)
                fonts.Add(family.Source);
            FontFamilyComboBox.ItemsSource = fonts;
        }
        #endregion

        #region 设置加载与保存
        /// <summary>
        /// 从注册表加载设置
        /// </summary>
        private void LoadSettings()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\MetroEditor");
            if (key == null) return;

            // 编辑器设置
            if (key.GetValue("FontSize") is int size && size > 0)
                FontSizeSlider.Value = size;
            if (key.GetValue("FontFamily") is string font)
                FontFamilyComboBox.Text = font;

            // API Key（解密）
            if (key.GetValue("ApiKey") is string apiKey)
            {
                try
                {
                    string decrypted = SecureStorageHelper.Decrypt(apiKey);
                    ApiKeyPasswordBox.Password = decrypted;
                }
                catch
                {
                    // 解密失败可能是旧明文格式
                    ApiKeyPasswordBox.Password = apiKey;
                }
            }

            // 路径搜索权重
            if (key.GetValue("LengthWeight") is double lw) LengthWeightSlider.Value = lw;
            if (key.GetValue("TransferWeight") is double tw) TransferWeightSlider.Value = tw;
            if (key.GetValue("MoneyWeight") is double mw) MoneyWeightSlider.Value = mw;
            if (key.GetValue("TimeWeight") is double timeW) TimeWeightSlider.Value = timeW;
            if (key.GetValue("StationWeight") is double sw) StationWeightSlider.Value = sw;
            if (key.GetValue("SpecialWeight") is double spw) SpecialWeightSlider.Value = spw;
        }

        /// <summary>
        /// 保存设置按钮点击事件
        /// </summary>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\MetroEditor");

                // 编辑器设置
                key.SetValue("FontSize", (int)FontSizeSlider.Value);
                key.SetValue("FontFamily", FontFamilyComboBox.Text);

                // API Key（加密存储）
                string encrypted = SecureStorageHelper.Encrypt(ApiKeyPasswordBox.Password);
                key.SetValue("ApiKey", encrypted);

                // 路径搜索权重
                key.SetValue("LengthWeight", LengthWeightSlider.Value);
                key.SetValue("TransferWeight", TransferWeightSlider.Value);
                key.SetValue("MoneyWeight", MoneyWeightSlider.Value);
                key.SetValue("TimeWeight", TimeWeightSlider.Value);
                key.SetValue("StationWeight", StationWeightSlider.Value);
                key.SetValue("SpecialWeight", SpecialWeightSlider.Value);

                // 更新公开属性，供主窗口读取
                LengthWeight = LengthWeightSlider.Value;
                TransferWeight = TransferWeightSlider.Value;
                MoneyWeight = MoneyWeightSlider.Value;
                TimeWeight = TimeWeightSlider.Value;
                StationWeight = StationWeightSlider.Value;
                SpecialWeight = SpecialWeightSlider.Value;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        #endregion
    }
}