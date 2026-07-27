using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 添加换乘关系对话框：用于定义两个线路在特定车站的换乘关系
    /// </summary>
    public partial class AddTransferDialog : Window
    {
        // 公共属性：返回用户输入的数据
        public string SelectedStation => StationCombo.Text.Trim();
        public string SelectedLine1 => (Line1Combo.SelectedItem as dynamic)?.Id?.ToString() ?? Line1Combo.Text;
        public string SelectedLine2 => (Line2Combo.SelectedItem as dynamic)?.Id?.ToString() ?? Line2Combo.Text;
        public string SelectedStatus => (StatusCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "operating";
        public double TimeMin => double.TryParse(TimeBox.Text, out double v) ? v : 3.0;

        private readonly Dictionary<string, string> _lineNameMap;

        /// <summary>
        /// 构造函数：初始化对话框，设置车站和线路下拉框的数据源
        /// </summary>
        /// <param name="stations">可用的车站列表</param>
        /// <param name="lines">可用的线路ID列表</param>
        /// <param name="lineNameMap">线路ID到显示名称的映射</param>
        public AddTransferDialog(List<string> stations, List<string> lines, Dictionary<string, string> lineNameMap)
        {
            InitializeComponent();
            _lineNameMap = lineNameMap;
            // 设置车站下拉框的数据源
            StationCombo.ItemsSource = stations;
            // 构建线路下拉框的数据源，显示名称，值为ID
            var lineItems = lines.Select(id => new { Display = _lineNameMap.GetValueOrDefault(id, id), Id = id }).ToList();
            // 设置两个线路下拉框的数据源
            Line1Combo.DisplayMemberPath = "Display";
            Line1Combo.SelectedValuePath = "Id";
            Line1Combo.ItemsSource = lineItems;
            Line2Combo.DisplayMemberPath = "Display";
            Line2Combo.SelectedValuePath = "Id";
            Line2Combo.ItemsSource = lineItems;
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 确定按钮点击事件：验证输入并设置对话框结果
        /// </summary>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // 验证必填字段
            if (string.IsNullOrWhiteSpace(SelectedStation) ||
                string.IsNullOrWhiteSpace(SelectedLine1) ||
                string.IsNullOrWhiteSpace(SelectedLine2))
            {
                MessageBox.Show("请完整填写所有字段", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // 验证两条线路不能相同
            if (SelectedLine1 == SelectedLine2)
            {
                MessageBox.Show("线路1和线路2不能相同", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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