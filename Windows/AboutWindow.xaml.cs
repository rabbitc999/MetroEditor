using System.Windows;
using System.Windows.Navigation;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 关于窗口 - 显示应用程序信息和版权声明
    /// 功能：
    /// 1. 展示软件版本、作者、许可证等基本信息
    /// 2. 提供外部链接跳转（如项目主页、文档、开源协议）
    /// 3. 简单的模态对话框交互
    /// </summary>
    /// <remarks>
    /// 窗口特性：
    /// - 模态对话框（ShowDialog）
    /// - 固定大小，不可调整
    /// - 居中显示
    /// - 支持超链接导航
    /// </remarks>
    public partial class AboutWindow : Window
    {
        /// <summary>
        /// 构造函数：初始化关于窗口
        /// </summary>
        public AboutWindow()
        {
            // 加载XAML定义的界面组件
            InitializeComponent();
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 确定按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件源（确定按钮）</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 功能：关闭窗口，返回DialogResult=true
        /// 使用场景：用户阅读完信息后点击"确定"
        /// </remarks>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // 关闭窗口，返回成功结果
            Close();
        }

        /// <summary>
        /// 超链接导航请求事件处理
        /// </summary>
        /// <param name="sender">事件源（Hyperlink控件）</param>
        /// <param name="e">导航事件参数，包含目标URI</param>
        /// <remarks>
        /// 功能：在默认浏览器中打开超链接
        /// 安全特性：
        /// 1. 使用UseShellExecute=true调用系统默认浏览器
        /// 2. 异常捕获，避免崩溃
        /// 3. 用户友好的错误提示
        /// 
        /// XAML绑定：在XAML中通过Hyperlink.RequestNavigate绑定此方法
        /// 示例：<Hyperlink NavigateUri="https://github.com/..." RequestNavigate="Hyperlink_RequestNavigate">
        /// </remarks>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                // 启动默认浏览器打开链接
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
                    {
                        UseShellExecute = true  // 使用系统shell执行，确保调用默认浏览器
                    });

                // 标记事件已处理，阻止进一步冒泡
                e.Handled = true;
            }
            catch (System.Exception ex)
            {
                // 异常处理：显示友好的错误信息
                MessageBox.Show($"无法打开链接: {ex.Message}",
                               "错误",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }
    }
}