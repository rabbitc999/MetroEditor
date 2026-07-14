using System.Windows;
using System.Windows.Input;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 通用简单输入对话框
    /// 功能：获取用户单行文本输入
    /// 应用场景：
    /// - 批量添加站点时输入多行文本
    /// - 重命名操作时输入新名称
    /// - 任何需要简单文本输入的场景
    /// </summary>
    /// <remarks>
    /// 设计特点：
    /// - 支持 Enter 键快速确认
    /// - 自动聚焦到输入框
    /// - 支持 Owner 设置，确保模态行为正确
    /// </remarks>
    public partial class InputDialog : Window
    {
        /// <summary>
        /// 获取用户输入的文本
        /// </summary>
        public string InputText => InputTextBox.Text;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">提示消息</param>
        /// <param name="title">窗口标题</param>
        /// <param name="defaultText">默认文本</param>
        /// <param name="owner">父窗口（可选）</param>
        public InputDialog(string message, string title, string defaultText = "", Window? owner = null)
        {
            InitializeComponent();
            Title = title;
            MessageTextBlock.Text = message;
            InputTextBox.Text = defaultText;

            // 设置父窗口，确保对话框居中显示
            if (owner != null)
            {
                Owner = owner;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            // 键盘支持：Enter 键触发确定
            InputTextBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                    Ok_Click(this, e);
            };

            // 加载完成后自动聚焦到输入框
            Loaded += (_, _) => InputTextBox.Focus();
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}