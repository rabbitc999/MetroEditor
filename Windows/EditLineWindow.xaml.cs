using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;
using System.Security.Cryptography;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 线路定义编辑器窗口
    /// 功能：新建或修改 LINE_DEFINE 代码块
    /// 支持配置：
    /// - 线路名称、颜色、所属城市
    /// - 列车编组、最高时速、开通年份
    /// - 牵引功率、制动减速度
    /// - 禁用状态、外链信息
    /// </summary>
    public partial class EditLineWindow : Window
    {
        private readonly HashSet<string> _existingIds; // 现有线路ID集合（用于冲突检测）
        private readonly string? _editLineId;          // 编辑模式下的原线路ID
        private string _selectedColor = "#FF0000";    // 当前选中的颜色（默认红色）

        // ===== 构造函数重载 =====
        /// <summary>
        /// 新建模式构造函数
        /// </summary>
        public EditLineWindow(HashSet<string> existingLineIds)
            : this(existingLineIds, null, null, null, null, null, null, null, null, null, false, null)
        { }

        /// <summary>
        /// 完整构造函数（支持编辑模式）
        /// </summary>
        public EditLineWindow(
            HashSet<string> existingLineIds,
            string? lineId,
            string? name,
            string? color,
            string? city,
            string? formation,
            string? maxspeed,
            string? openYear,
            string? power,
            string? brake,
            bool disabled,
            string? link)
        {
            InitializeComponent();
            _existingIds = existingLineIds ?? new HashSet<string>();
            _editLineId = lineId;

            if (!string.IsNullOrEmpty(lineId))
            {
                // ===== 编辑模式：填充表单数据 =====
                LineIdTextBox.Text = lineId;
                NameTextBox.Text = name;
                CityTextBox.Text = city;
                FormationTextBox.Text = formation;
                MaxspeedTextBox.Text = maxspeed;
                OpenYearTextBox.Text = openYear;
                PowerKwTextBox.Text = power;
                BrakeTextBox.Text = brake;
                DisabledCheckBox.IsChecked = disabled;

                // 颜色处理
                if (!string.IsNullOrEmpty(color))
                {
                    _selectedColor = color;
                    ColorTextBox.Text = color;
                    UpdateColorPreview(color);
                }

                // 外链处理
                if (!string.IsNullOrEmpty(link))
                {
                    LinkCheckBox.IsChecked = true;
                    LinkTextBox.Text = link;
                }
                Title = "修改线路";
            }
            else
            {
                // ===== 新建模式：自动生成ID =====
                AutoGenerateId();
                Title = "新建线路";
            }

            // 事件绑定：颜色文本框变化时实时更新预览
            ColorTextBox.TextChanged += ColorTextBox_TextChanged;
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 自动生成线路ID（规则：L + 最小可用数字）
        /// </summary>
        private void AutoGenerateId()
        {
            int i = 1;
            while (true)
            {
                string id = "L" + i;
                // 跳过已存在的ID（编辑模式下允许与原ID相同）
                if (!_existingIds.Contains(id) || id == _editLineId)
                {
                    LineIdTextBox.Text = id;
                    break;
                }
                i++;
            }
        }

        /// <summary>
        /// 手动触发自动生成ID
        /// </summary>
        private void AutoGenerateId_Click(object sender, RoutedEventArgs e) => AutoGenerateId();

        // ==================== 颜色选择功能 ====================
        /// <summary>
        /// 打开系统颜色对话框选择颜色
        /// </summary>
        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ColorDialog();
            try
            {
                // 尝试解析当前颜色作为初始值
                dialog.Color = System.Drawing.ColorTranslator.FromHtml(_selectedColor);
            }
            catch
            {
                dialog.Color = System.Drawing.Color.Red; // 解析失败则用红色
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // 转换WinForms颜色为HTML格式
                var color = System.Drawing.Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
                _selectedColor = System.Drawing.ColorTranslator.ToHtml(color);
                ColorTextBox.Text = _selectedColor;
                UpdateColorPreview(_selectedColor);
            }
        }

        /// <summary>
        /// 颜色文本框内容变化事件（实时预览）
        /// </summary>
        private void ColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = ColorTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (!text.StartsWith("#")) text = "#" + text; // 补全#号
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(text);
                UpdateColorPreview(text);
                _selectedColor = text;
            }
            catch
            {
                // 无效颜色格式，忽略
            }
        }

        /// <summary>
        /// 更新颜色预览块的画刷
        /// </summary>
        private void UpdateColorPreview(string colorHex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                ColorPreview.Fill = new SolidColorBrush(color);
            }
            catch { /* 忽略无效颜色 */ }
        }

        // ==================== 代码生成 ====================
        /// <summary>
        /// 生成 LINE_DEFINE 代码块
        /// </summary>
        /// <returns>生成的代码字符串，验证失败返回null</returns>
        private string? GenerateCode()
        {
            string lid = LineIdTextBox.Text.Trim();
            string name = NameTextBox.Text.Trim();
            string color = ColorTextBox.Text.Trim();
            string city = CityTextBox.Text.Trim();
            string formation = FormationTextBox.Text.Trim();
            string maxspeed = MaxspeedTextBox.Text.Trim();
            string openYear = OpenYearTextBox.Text.Trim();
            string power = PowerKwTextBox.Text.Trim();
            string brake = BrakeTextBox.Text.Trim();
            bool disabled = DisabledCheckBox.IsChecked == true;
            bool hasLink = LinkCheckBox.IsChecked == true;
            string link = LinkTextBox.Text.Trim();

            // 基础验证
            if (string.IsNullOrEmpty(lid) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(color))
                return null;

            // ID冲突检测（编辑模式下允许与原ID相同）
            if (_editLineId != null && lid != _editLineId && _existingIds.Contains(lid))
                return null;

            var lines = new List<string>();
            lines.Add($"LINE_DEFINE {lid}");
            lines.Add($"    NAME '{name}'");
            lines.Add($"    COLOR '{color}'");

            // 可选参数（非空才添加）
            if (!string.IsNullOrEmpty(city)) lines.Add($"    CITY '{city}'");
            if (!string.IsNullOrEmpty(formation)) lines.Add($"    FORMATION '{formation}'");
            if (!string.IsNullOrEmpty(maxspeed)) lines.Add($"    MAXSPEED {maxspeed}");
            if (!string.IsNullOrEmpty(openYear)) lines.Add($"    OPEN_YEAR {openYear}");
            if (!string.IsNullOrEmpty(power)) lines.Add($"    POWER {power}");
            if (!string.IsNullOrEmpty(brake)) lines.Add($"    BRAKE {brake}");
            if (disabled) lines.Add($"    DISABLE");
            if (hasLink && !string.IsNullOrEmpty(link)) lines.Add($"    LINK '{link}'");

            lines.Add("END_DEFINE");
            return string.Join("\n", lines);
        }

        // ==================== 事件处理 ====================
        private void GeneratePreview_Click(object sender, RoutedEventArgs e)
        {
            string? code = GenerateCode();
            if (code == null)
            {
                MessageBox.Show("线路ID、名称和颜色不能为空，且ID不能与已存在线路冲突", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            PreviewTextBox.Text = code;
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PreviewTextBox.Text))
            {
                MessageBox.Show("没有代码可复制，请先生成预览", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                Clipboard.SetText(PreviewTextBox.Text);
                MessageBox.Show("代码已复制到主界面", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}