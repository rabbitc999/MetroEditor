using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using MetroEditor.MetroLang;
using MetroEditor.MetroLang.Tool;
using MetroEditor.MetroLang.Decompiler;

namespace MetroEditor.Windows
{
    /// <summary>
    /// OSM数据导入工具窗口
    /// 功能：从OpenStreetMap导出的JSON数据中提取地铁线路和站点
    /// 核心流程：
    /// 1. 加载OSM JSON文件（通常包含relations, ways, nodes）
    /// 2. 加载配置规则（定义如何识别地铁线路）
    /// 3. 提取地铁关系（relations with route=subway）
    /// 4. 生成MetroLang格式的.metro代码
    /// </summary>
    /// <remarks>
    /// 应用场景：
    /// - 快速初始化新城市的路网数据
    /// - 从开源地图数据迁移到MetroEditor
    /// 
    /// 技术难点：
    /// - OSM数据结构的复杂性（Relations->Ways->Nodes的层级关系）
    /// - 线路识别规则的灵活性（不同城市的tag命名可能不同）
    /// - 数据清洗（去除公交线路、旅游线路等干扰项）
    /// 
    /// 配置文件示例：
    /// {
    ///   "line_tags": {
    ///     "route": "subway",
    ///     "railway": "subway"
    ///   },
    ///   "name_tags": ["name:zh", "name", "ref"]
    /// }
    /// </remarks>
    public partial class OsmImportWindow : Window
    {
        private string _generatedText; // 生成的代码缓存

        public OsmImportWindow()
        {
            InitializeComponent();
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 浏览OSM JSON文件按钮点击事件
        /// </summary>
        private void BrowseOsmFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON文件 (*.json)|*.json" };
            if (dlg.ShowDialog() == true)
                OsmFilePathTextBox.Text = dlg.FileName;
        }

        /// <summary>
        /// 浏览配置文件按钮点击事件
        /// </summary>
        private void BrowseConfigFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON文件 (*.json)|*.json" };
            if (dlg.ShowDialog() == true)
                ConfigFilePathTextBox.Text = dlg.FileName;
        }

        /// <summary>
        /// 提取按钮点击事件：执行 OSM 数据解析
        /// </summary>
        private void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            string osmPath = OsmFilePathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(osmPath) || !File.Exists(osmPath))
            {
                MessageBox.Show("请选择一个有效的OSM JSON文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string configPath = ConfigFilePathTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(configPath) && !File.Exists(configPath))
            {
                MessageBox.Show("配置文件不存在，将使用默认配置。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                configPath = null;
            }

            string keyCity = KeyCityTextBox.Text.Trim();
            if (string.IsNullOrEmpty(keyCity))
                keyCity = "广州";

            try
            {
                // ---------- 步骤1：加载配置 ----------
                StatusTextBlock.Text = "正在加载配置...";
                var config = ConfigLoader.LoadConfig(configPath);
                var metadata = new Dictionary<string, LineMetadata>(); // 元数据（暂未使用）

                // ---------- 步骤2：解析OSM JSON ----------
                StatusTextBlock.Text = "正在解析OSM数据...";
                string json = File.ReadAllText(osmPath);
                var osmRoot = JsonSerializer.Deserialize<OsmRoot>(json);
                if (osmRoot == null)
                {
                    MessageBox.Show("无法解析OSM JSON文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // ---------- 步骤3：提取地铁数据 → MetroJsonRoot ----------
                StatusTextBlock.Text = "正在提取地铁数据...";
                var extractor = new OsmMetroExtractor(config, metadata);
                var root = extractor.ExtractToRoot(osmRoot, keyCity);

                if (root.Lines.Count == 0)
                {
                    MessageBox.Show("未提取到任何地铁线路。请检查OSM数据是否包含地铁关系。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // ---------- 步骤4：使用 Decompiler 生成 .metro 代码 ----------
                StatusTextBlock.Text = "正在生成.metro代码...";
                var decompiler = new MetroDecompiler(root);
                _generatedText = decompiler.Decompile();

                PreviewTextBox.Text = _generatedText;
                StatusTextBlock.Text = $"提取成功。线路: {root.Lines.Count}, 站点: {root.Stations.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"提取失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "提取失败";
            }
        }

        /// <summary>
        /// 保存生成的代码到文件
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_generatedText))
            {
                MessageBox.Show("请先提取生成.metro代码。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new SaveFileDialog { Filter = "Metro文件 (*.metro)|*.metro" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, _generatedText, System.Text.Encoding.UTF8);
                    MessageBox.Show($"已保存至 {dlg.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}