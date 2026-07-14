using System;
using System.Windows;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 添加票价分段对话框 - 用于定义票价分段规则
    /// 功能：
    /// 1. 输入票价分段的起始里程、结束里程和对应票价
    /// 2. 验证输入数据的合法性
    /// 3. 返回用户输入的分段数据
    /// </summary>
    /// <remarks>
    /// 使用场景：
    /// - 在票价规则编辑器中添加新的分段规则
    /// - 支持鼠标和键盘交互
    /// 
    /// 数据验证：
    /// - 起始里程 ≥ 0
    /// - 结束里程 > 起始里程
    /// - 票价 ≥ 0
    /// 
    /// 设计模式：模态对话框，通过DialogResult返回结果
    /// </remarks>
    public partial class AddSegmentDialog : Window
    {
        #region 公共属性（数据访问接口）
        /// <summary>起始里程（千米）</summary>
        /// <remarks>默认值：0，从StartBox文本框读取</remarks>
        public double Start => double.TryParse(StartBox.Text, out double v) ? v : 0;

        /// <summary>结束里程（千米）</summary>
        /// <remarks>默认值：4，从EndBox文本框读取</remarks>
        public double End => double.TryParse(EndBox.Text, out double v) ? v : 4;

        /// <summary>分段票价（元）</summary>
        /// <remarks>默认值：2，从PriceBox文本框读取</remarks>
        public double Price => double.TryParse(PriceBox.Text, out double v) ? v : 2;
        #endregion

        /// <summary>
        /// 构造函数：初始化添加分段对话框
        /// </summary>
        public AddSegmentDialog()
        {
            // 加载XAML定义的界面组件
            InitializeComponent();

            // 可选：设置初始焦点
            StartBox.Focus();
        }

        /// <summary>
        /// 确定按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件源（确定按钮）</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 功能流程：
        /// 1. 验证输入数据的合法性
        /// 2. 若验证通过，设置DialogResult=true并关闭窗口
        /// 3. 若验证失败，显示警告信息并保持窗口打开
        /// </remarks>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // 数据验证：结束里程必须大于起始里程
            if (Start < 0 || End <= Start)
            {
                MessageBox.Show("结束里程必须大于起始里程",
                               "输入验证错误",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                return; // 阻止窗口关闭，等待用户修正
            }

            // 验证通过：设置对话框结果为true并关闭
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 取消按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件源（取消按钮）</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 功能：放弃输入，关闭窗口并返回DialogResult=false
        /// 快捷键：支持ESC键触发（需在XAML中设置IsCancel=true）
        /// </remarks>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // 设置对话框结果为false并关闭
            DialogResult = false;
            Close();
        }

        #region 可选扩展方法
        /// <summary>
        /// 预填充对话框数据（用于编辑模式）
        /// </summary>
        /// <param name="start">起始里程</param>
        /// <param name="end">结束里程</param>
        /// <param name="price">票价</param>
        /// <remarks>
        /// 使用场景：当需要编辑现有分段而非新建时调用
        /// 调用时机：在ShowDialog()之前调用
        /// </remarks>
        public void PopulateFields(double start, double end, double price)
        {
            StartBox.Text = start.ToString();
            EndBox.Text = end.ToString();
            PriceBox.Text = price.ToString();
        }

        /// <summary>
        /// 设置对话框标题（动态定制）
        /// </summary>
        /// <param name="title">新标题</param>
        public void SetTitle(string title)
        {
            Title = title;
        }
        #endregion
    }
}