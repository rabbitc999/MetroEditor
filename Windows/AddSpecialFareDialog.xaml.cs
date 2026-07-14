using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 添加特殊票价对话框：用于定义特殊线路的票价规则（统一票价或区间票价）
    /// </summary>
    public partial class AddSpecialFareDialog : Window
    {
        // 公共属性：用于返回用户输入的数据
        public string LineId => LineCombo.SelectedValue?.ToString();
        public string Type => (TypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() == "统一票价(ALL)" ? "all" : "segment";
        public double Price => double.TryParse(PriceBox.Text, out double v) ? v : 2;
        public string StartStation => StartBox.Text.Trim();
        public string EndStation => EndBox.Text.Trim();

        /// <summary>
        /// 构造函数：初始化对话框，设置线路下拉框和票价类型切换逻辑
        /// </summary>
        /// <param name="lineIds">可用的线路ID列表</param>
        /// <param name="lineNameMap">线路ID到显示名称的映射</param>
        public AddSpecialFareDialog(List<string> lineIds, Dictionary<string, string> lineNameMap)
        {
            InitializeComponent();
            // 构建线路下拉框的数据源，显示名称，值为ID
            var lineItems = lineIds.Select(id => new { Display = lineNameMap.GetValueOrDefault(id, id), Id = id }).ToList();
            LineCombo.ItemsSource = lineItems;

            // 根据票价类型切换区间输入框的启用状态
            TypeCombo.SelectionChanged += (s, e) =>
            {
                bool isSegment = Type == "segment";
                StartBox.IsEnabled = isSegment;
                EndBox.IsEnabled = isSegment;
            };
        }

        /// <summary>
        /// 确定按钮点击事件：验证输入并设置对话框结果
        /// </summary>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // 验证线路选择
            if (string.IsNullOrEmpty(LineId))
            {
                MessageBox.Show("请选择线路", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // 区间票价需要填写起点和终点
            if (Type == "segment" && (string.IsNullOrEmpty(StartStation) || string.IsNullOrEmpty(EndStation)))
            {
                MessageBox.Show("区间票价需要填写起点和终点", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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