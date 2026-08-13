using MetroEditor.MetroLang.DataModel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 运营公司管理窗口（OPERATOR_DEFINE）
    /// 功能：列表 + 表单方式管理多家运营公司（名称/简称/英文名/国家/城市/成立年份/网站/热线/简介/LOGO/运营线路），
    /// 支持从当前编译数据（ParsedData）导入、增删改、生成 OPERATOR_DEFINE 代码块插入编辑器
    /// </summary>
    public partial class OperatorManagerWindow : Window
    {
        private const string Indent = "  ";

        /// <summary>
        /// 运营公司编辑项（表单与列表的数据载体）
        /// </summary>
        public class OperatorItem
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string ShortName { get; set; } = "";
            public string EnName { get; set; } = "";
            public string Country { get; set; } = "";
            public string City { get; set; } = "";
            public string FoundedText { get; set; } = "";
            public string Website { get; set; } = "";
            public string Hotline { get; set; } = "";
            public string Description { get; set; } = "";
            public string LogoPath { get; set; } = "";
            public string LinesText { get; set; } = "";

            public int LineCount => SplitLineIds(LinesText).Count;
        }

        private readonly List<OperatorItem> _operators = new();
        private bool _loading; // 表单加载标志

        public OperatorManagerWindow(ParsedData? data = null)
        {
            InitializeComponent();
            if (data != null)
                LoadFromData(data);
            else
                AddNewOperator();
            RefreshGrid();
            UpdatePreview();
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 从编译结果导入现有运营公司
        /// 注意：LOGO 以 Base64 存储无法还原为路径，导入时留空，可重新选择
        /// </summary>
        private void LoadFromData(ParsedData data)
        {
            foreach (var kv in data.OperatorCompanies)
            {
                var c = kv.Value;
                _operators.Add(new OperatorItem
                {
                    Id = kv.Key,
                    Name = c.Name ?? "",
                    ShortName = c.ShortName ?? "",
                    EnName = c.EnName ?? "",
                    Country = c.Country ?? "",
                    City = c.City ?? "",
                    FoundedText = c.Founded?.ToString(CultureInfo.InvariantCulture) ?? "",
                    Website = c.Website ?? "",
                    Hotline = c.Hotline ?? "",
                    Description = c.Description ?? "",
                    LogoPath = "",
                    LinesText = c.Lines != null ? string.Join(", ", c.Lines) : ""
                });
            }
        }

        /// <summary>
        /// 刷新列表
        /// </summary>
        private void RefreshGrid()
        {
            OperatorGrid.ItemsSource = null;
            OperatorGrid.ItemsSource = _operators;
        }

        /// <summary>
        /// 新增空白运营公司并选中
        /// </summary>
        private OperatorItem AddNewOperator()
        {
            var item = new OperatorItem();
            _operators.Add(item);
            return item;
        }

        #region 列表操作
        private void NewOperator_Click(object sender, RoutedEventArgs e)
        {
            var item = AddNewOperator();
            RefreshGrid();
            OperatorGrid.SelectedItem = item;
            OperatorGrid.ScrollIntoView(item);
            LoadForm(item);
            OpIdTextBox.Focus();
        }

        private void DeleteOperator_Click(object sender, RoutedEventArgs e)
        {
            if (OperatorGrid.SelectedItem is not OperatorItem item) return;
            _operators.Remove(item);
            if (_operators.Count == 0)
                AddNewOperator();
            RefreshGrid();
            OperatorGrid.SelectedIndex = Math.Min(0, _operators.Count - 1);
            LoadForm(OperatorGrid.SelectedItem as OperatorItem);
            UpdatePreview();
        }

        private void OperatorGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OperatorGrid.SelectedItem is OperatorItem item)
                LoadForm(item);
        }
        #endregion

        #region 表单同步
        /// <summary>
        /// 将选中项加载到表单（不触发写回）
        /// </summary>
        private void LoadForm(OperatorItem item)
        {
            if (item == null) return;
            _loading = true;
            try
            {
                OpIdTextBox.Text = item.Id;
                OpNameTextBox.Text = item.Name;
                OpShortNameTextBox.Text = item.ShortName;
                OpEnNameTextBox.Text = item.EnName;
                OpCountryTextBox.Text = item.Country;
                OpCityTextBox.Text = item.City;
                OpFoundedTextBox.Text = item.FoundedText;
                OpWebsiteTextBox.Text = item.Website;
                OpHotlineTextBox.Text = item.Hotline;
                OpDescriptionTextBox.Text = item.Description;
                OpLogoTextBox.Text = item.LogoPath;
                OpLinesTextBox.Text = item.LinesText;
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>
        /// 表单变更时写回选中项并刷新预览
        /// </summary>
        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || OperatorGrid.SelectedItem is not OperatorItem item) return;
            item.Id = OpIdTextBox.Text.Trim();
            item.Name = OpNameTextBox.Text;
            item.ShortName = OpShortNameTextBox.Text;
            item.EnName = OpEnNameTextBox.Text;
            item.Country = OpCountryTextBox.Text;
            item.City = OpCityTextBox.Text;
            item.FoundedText = OpFoundedTextBox.Text.Trim();
            item.Website = OpWebsiteTextBox.Text;
            item.Hotline = OpHotlineTextBox.Text;
            item.Description = OpDescriptionTextBox.Text;
            item.LogoPath = OpLogoTextBox.Text.Trim();
            item.LinesText = OpLinesTextBox.Text;
            UpdatePreview();
        }

        /// <summary>
        /// 选择 LOGO 图片文件
        /// </summary>
        private void SelectLogo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件 (*.*)|*.*",
                Title = "选择 LOGO 图片"
            };
            if (dlg.ShowDialog() == true)
                OpLogoTextBox.Text = dlg.FileName;
        }
        #endregion

        #region 代码生成
        /// <summary>
        /// 解析线路 ID 列表（逗号、空格、换行分隔，去空）
        /// </summary>
        public static List<string> SplitLineIds(string input) =>
            (input ?? "").Split(new[] { ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim())
                         .Where(s => s.Length > 0)
                         .Distinct()
                         .ToList();

        /// <summary>
        /// 生成单个运营公司的 OPERATOR_DEFINE 代码
        /// </summary>
        private static string GenerateOperatorCode(OperatorItem c)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"OPERATOR_DEFINE {c.Id}");

            if (!string.IsNullOrEmpty(c.Name))
                sb.AppendLine($"{Indent}NAME '{c.Name}'");
            if (!string.IsNullOrEmpty(c.ShortName))
                sb.AppendLine($"{Indent}SHORT_NAME '{c.ShortName}'");
            if (!string.IsNullOrEmpty(c.EnName))
                sb.AppendLine($"{Indent}EN_NAME '{c.EnName}'");
            if (!string.IsNullOrEmpty(c.Country))
                sb.AppendLine($"{Indent}COUNTRY '{c.Country}'");
            if (!string.IsNullOrEmpty(c.City))
                sb.AppendLine($"{Indent}CITY '{c.City}'");
            if (int.TryParse(c.FoundedText, out int founded))
                sb.AppendLine($"{Indent}FOUNDED {founded}");
            if (!string.IsNullOrEmpty(c.Website))
                sb.AppendLine($"{Indent}WEBSITE '{c.Website}'");
            if (!string.IsNullOrEmpty(c.Hotline))
                sb.AppendLine($"{Indent}HOTLINE '{c.Hotline}'");
            if (!string.IsNullOrEmpty(c.Description))
                sb.AppendLine($"{Indent}DESCRIPTION '{c.Description}'");
            if (!string.IsNullOrEmpty(c.LogoPath))
                sb.AppendLine($"{Indent}LOGO '{c.LogoPath}'");

            var lines = SplitLineIds(c.LinesText);
            if (lines.Count > 0)
                sb.AppendLine($"{Indent}LINES [{string.Join(", ", lines.Select(l => $"'{l}'"))}]");

            sb.AppendLine("END_DEFINE");
            return sb.ToString();
        }

        /// <summary>
        /// 生成全部运营公司的 OPERATOR_DEFINE 代码
        /// </summary>
        private string GenerateAllCode()
        {
            var sb = new StringBuilder();
            var valid = _operators.Where(c => !string.IsNullOrEmpty(c.Id)).ToList();
            for (int i = 0; i < valid.Count; i++)
            {
                sb.AppendLine($"# 运营公司：{valid[i].Id}");
                sb.AppendLine(GenerateOperatorCode(valid[i]));
                if (i < valid.Count - 1) sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// 更新预览
        /// </summary>
        private void UpdatePreview() => PreviewTextBox.Text = GenerateAllCode();
        #endregion

        #region 操作
        /// <summary>
        /// 插入到编辑器（生成全部运营公司的代码）
        /// </summary>
        private void InsertCode_Click(object sender, RoutedEventArgs e)
        {
            string code = GenerateAllCode();
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show("没有可插入的运营公司代码（请先填写公司 ID）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                MainWindow.InsertText(code);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"插入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        #endregion
    }
}
