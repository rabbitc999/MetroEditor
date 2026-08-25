using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 线路选择器对话框
    /// 功能：从所有线路中选择一条线路
    /// 应用场景：
    /// - 在票价编辑器中指定特殊线路
    /// - 在路径规划中指定分析对象
    /// - 任何需要选择特定线路的场景
    /// </summary>
    /// <remarks>
    /// 设计特点：
    /// - 显示线路友好名称（如"北京地铁1号线"）而非内部ID
    /// - 按名称排序，便于查找
    /// - 支持键盘导航（上下键选择，回车确认）
    /// </remarks>
    public partial class LineSelectorWindow : Window
    {
        /// <summary>
        /// 获取用户选择的线路ID
        /// </summary>
        public string SelectedLineId { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="lineIds">所有线路ID列表</param>
        /// <param name="lineNameMap">线路ID到显示名称的映射</param>
        public LineSelectorWindow(List<string> lineIds, Dictionary<string, string> lineNameMap)
        {
            InitializeComponent();

            // 构建下拉框数据源：显示"名称 (ID)"格式
            var items = lineIds.Select(id => new
            {
                Id = id,
                Display = lineNameMap.TryGetValue(id, out var name) ? $"{name} ({id})" : id
            }).OrderBy(x => x.Display).ToList();

            LineComboBox.ItemsSource = items;
            if (items.Any())
                LineComboBox.SelectedIndex = 0;
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var selected = LineComboBox.SelectedItem;
            if (selected == null)
            {
                MessageBox.Show("请选择一条线路", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 使用反射获取匿名类型的 Id 属性
            var prop = selected.GetType().GetProperty("Id");
            if (prop != null)
            {
                SelectedLineId = prop.GetValue(selected) as string;
                DialogResult = true;
                Close();
            }
            else
            {
                // 降级处理：直接使用文本框内容（不太可能发生）
                SelectedLineId = LineComboBox.Text.Trim();
                DialogResult = true;
                Close();
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
    }
}