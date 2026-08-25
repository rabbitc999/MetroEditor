using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 添加停站时间对话框：用于定义特定车站在特定线路上的停站时间
    /// </summary>
    public partial class AddDwellTimeDialog : Window
    {
        // 公共属性：返回用户输入的数据
        public string SelectedStation => StationCombo.Text.Trim();
        public string SelectedLine => LineCombo.SelectedValue?.ToString() ?? LineCombo.Text;
        public double TimeMin => double.TryParse(TimeBox.Text, out double v) ? v : 0.5;

        /// <summary>
        /// 构造函数：初始化对话框，设置车站和线路下拉框的数据源
        /// </summary>
        /// <param name="stations">可用的车站列表</param>
        /// <param name="lines">可用的线路ID列表</param>
        /// <param name="lineNameMap">线路ID到显示名称的映射</param>
        public AddDwellTimeDialog(List<string> stations, List<string> lines, Dictionary<string, string> lineNameMap)
        {
            InitializeComponent();
            // 设置车站下拉框的数据源
            StationCombo.ItemsSource = stations;

            // 构建线路下拉框的数据源，显示名称，值为ID
            var lineItems = lines.Select(id => new { Display = lineNameMap.GetValueOrDefault(id, id), Id = id }).ToList();
            LineCombo.ItemsSource = lineItems;
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 确定按钮点击事件：验证输入并设置对话框结果
        /// </summary>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // 验证必填字段
            if (string.IsNullOrWhiteSpace(SelectedStation) ||
                string.IsNullOrWhiteSpace(SelectedLine))
            {
                MessageBox.Show("请完整填写所有字段", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 取消按钮点击事件：关闭对话框，返回false
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}