using MetroEditor.MetroLang.DataModel;
using System;
using System.Linq;
using System.Text;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 程序信息编辑窗口（PROGRAM_DEFINE）
    /// 功能：图形化编辑程序元数据——主城市、其他城市、版本、作者、日期、语言顺序
    /// 支持从当前编译数据（ParsedData）导入，生成 PROGRAM_DEFINE 代码块插入编辑器
    /// </summary>
    public partial class ProgramInfoWindow : Window
    {
        private const string Indent = "  ";

        public ProgramInfoWindow(ParsedData? data = null, string defaultCity = "广州")
        {
            InitializeComponent();
            InitializeDefaults(defaultCity);
            if (data != null)
                LoadFromData(data);
            UpdatePreview();
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 设置默认值（无导入数据时的初始值）
        /// </summary>
        private void InitializeDefaults(string defaultCity)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            KeyCityTextBox.Text = defaultCity;
            VersionTextBox.Text = "V1.0";
            AuthorTextBox.Text = "匿名";
            CreateDateTextBox.Text = today;
            UpdateDateTextBox.Text = today;
            LanguageOrderTextBox.Text = "zh-Hans";
        }

        /// <summary>
        /// 从编译结果导入现有程序信息
        /// </summary>
        private void LoadFromData(ParsedData data)
        {
            if (!string.IsNullOrEmpty(data.MainCity))
                KeyCityTextBox.Text = data.MainCity;
            if (data.OtherCities != null && data.OtherCities.Count > 0)
                OtherCitiesTextBox.Text = string.Join(", ", data.OtherCities);
            if (!string.IsNullOrEmpty(data.Version))
                VersionTextBox.Text = data.Version;
            if (!string.IsNullOrEmpty(data.Author))
                AuthorTextBox.Text = data.Author;
            if (!string.IsNullOrEmpty(data.CreateDate))
                CreateDateTextBox.Text = data.CreateDate;
            if (!string.IsNullOrEmpty(data.UpdateDate))
                UpdateDateTextBox.Text = data.UpdateDate;
            if (data.LanguageOrder != null && data.LanguageOrder.Count > 0)
                LanguageOrderTextBox.Text = string.Join(", ", data.LanguageOrder);
        }

        /// <summary>
        /// 按分隔符解析列表输入（逗号、空格、换行）
        /// </summary>
        private static List<string> SplitList(string input) =>
            (input ?? "").Split(new[] { ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim())
                         .Where(s => s.Length > 0)
                         .Distinct()
                         .ToList();

        /// <summary>
        /// 生成 PROGRAM_DEFINE 代码块
        /// </summary>
        private string GenerateCode()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 程序定义（主城市/版本/日期）");
            sb.AppendLine("PROGRAM_DEFINE");

            string keyCity = KeyCityTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(keyCity))
                sb.AppendLine($"{Indent}KEY_CITY '{keyCity}'");

            var otherCities = SplitList(OtherCitiesTextBox.Text);
            if (otherCities.Count > 0)
                sb.AppendLine($"{Indent}OTHER_CITY [{string.Join(", ", otherCities.Select(c => $"'{c}'"))}]");

            if (!string.IsNullOrWhiteSpace(VersionTextBox.Text))
                sb.AppendLine($"{Indent}VERSION '{VersionTextBox.Text.Trim()}'");
            if (!string.IsNullOrWhiteSpace(CreateDateTextBox.Text))
                sb.AppendLine($"{Indent}CREATE_DATE '{CreateDateTextBox.Text.Trim()}'");
            if (!string.IsNullOrWhiteSpace(UpdateDateTextBox.Text))
                sb.AppendLine($"{Indent}UPDATE_DATE '{UpdateDateTextBox.Text.Trim()}'");
            if (!string.IsNullOrWhiteSpace(AuthorTextBox.Text))
                sb.AppendLine($"{Indent}AUTHOR '{AuthorTextBox.Text.Trim()}'");

            var langs = SplitList(LanguageOrderTextBox.Text);
            if (langs.Count > 0)
                sb.AppendLine($"{Indent}LANGUAGE_ORDER [{string.Join(", ", langs.Select(l => $"'{l}'"))}]");

            sb.AppendLine("END_DEFINE");
            return sb.ToString();
        }

        /// <summary>
        /// 更新代码预览
        /// </summary>
        private void UpdatePreview() => PreviewTextBox.Text = GenerateCode();

        /// <summary>
        /// 字段变化时实时刷新预览
        /// </summary>
        private void Field_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdatePreview();

        /// <summary>
        /// 插入到编辑器
        /// </summary>
        private void InsertCode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(KeyCityTextBox.Text))
            {
                MessageBox.Show("KEY_CITY（主城市）为必填项", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                MainWindow.InsertText(GenerateCode());
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"插入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
