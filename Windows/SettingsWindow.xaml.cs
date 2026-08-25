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
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();          // 加载已有设置
            LoadFonts();            // 加载系统字体
            Loaded += (o, e) => Topmost = false;
        }

        #region UI初始化

        /// <summary>
        /// 加载系统字体列表到下拉框
        /// </summary>
        private void LoadFonts()
        {
            var fonts = new ObservableCollection<string>();
            foreach (var family in Fonts.SystemFontFamilies)
                fonts.Add(family.Source);
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
        }

        /// <summary>
        /// 保存设置按钮点击事件
        /// </summary>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\MetroEditor");

                // API Key（加密存储）
                string encrypted = SecureStorageHelper.Encrypt(ApiKeyPasswordBox.Password);
                key.SetValue("ApiKey", encrypted);

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