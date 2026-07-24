using MetroEditor.MetroLang.Compiler;
using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.Decompiler;
using MetroEditor.MetroLang.Exporter;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.MetroLang.Tool;
using MetroEditor.Simulator.Algorithms;
using MetroEditor.Simulator.Data;
using MetroEditor.Windows.Controls;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static MetroEditor.MetroLang.SyntaxUsage;
using Application = System.Windows.Application;
using Path = System.IO.Path;
using Window = System.Windows.Window;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 主窗口 — 整合编辑、编译、预览与数据管理
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 常量与静态资源
        private const string DefaultStatus = "就绪";
        private const string DefaultCity = "北京";
        private const double FontSizeMin = 8;
        private const double FontSizeMax = 30;
        private const string RegistryKeyPath = @"Software\MetroEditor";
        private const string RecentFilesValueName = "RecentFiles";
        private const int MaxRecentFiles = 10;
        private List<string> _recentFiles = new List<string>();
        #endregion

        #region 字段
        private static double _defaultFontSize = 16;
        private static string _defaultFontFamily = "Consolas";
        private JsonRoot _currentRoot;

        private Dictionary<string, string> _lineNameMap;
        private string _startupFile;
        #endregion

        #region 构造函数与初始化
        /// <summary>
        /// 窗口构造函数：完成所有基础设施的初始化
        /// 设计要点：
        /// 1. 延迟加载启动文件：通过Dispatcher延迟执行，确保UI完全初始化后再加载内容
        /// 2. 折叠管理器单例：全局唯一FoldingManager，避免重复创建性能开销
        /// 3. 数据上下文绑定：将静态语法参考、ViewModel集合绑定到自身，简化XAML访问
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            ConfigureCommands();
            LoadSettings();

            // 处理命令行参数
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                string filePath = args[1];
                if (File.Exists(filePath))
                {
                    _startupFile = filePath;
                }
            }

            // 加载启动文件（延迟）
            Loaded += (_, _) =>
            {
                if (!string.IsNullOrEmpty(_startupFile))
                {
                    LoadFileFromArgument(_startupFile);
                }
            };

            // 订阅文档工作区事件
            documentWorkspace.CurrentTabChanged += OnCurrentTabChanged;
            documentWorkspace.TabClosing += OnTabClosing;

            stationListPanel.StationDoubleClicked += (s, vm) => InsertText(vm.Name);
            stationListPanel.EditStationRequested += StationListPanel_EditStationRequested;
            stationListPanel.AddStationRequested += (s, e) => OpenStationManager();

            directionListPanel.DirectionDoubleClicked += (s, vm) => InsertText(vm.DirId);
            directionListPanel.EditDirectionRequested += DirectionListPanel_EditDirectionRequested;

            lineListPanel.LineDoubleClicked += (s, vm) => InsertText(vm.LineId);
            lineListPanel.EditLineRequested += LineListPanel_EditLineRequested;

            fareParametersPanel.EditFareRequested += (s, e) => EditFare_Click(null, null);
            fareParametersPanel.EditSpecialFareRequested += (s, e) => EditSpecialFare_Click(null, null);

            timeParametersPanel.EditTransfersRequested += (s, e) => EditTransfers_Click(null, null);
            timeParametersPanel.EditDwellTimesRequested += (s, e) => EditDwellTimes_Click(null, null);

            mapControl.MapReady += (s, e) =>
            {
                // 地图已准备就绪，可以执行一些操作
                Debug.WriteLine("地图已加载完成");
            };
        }

        private void StationListPanel_EditStationRequested(object sender, StationViewModel station)
        {
            if (station == null) return;
            var data = documentWorkspace.CurrentTab?.ParsedData;
            if (data == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!data.Stations.TryGetValue(station.Name, out var coord))
            {
                MessageBox.Show($"未找到站台 {station.Name} 的坐标数据", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 打开编辑窗口（复用原有的 ManagerStationWindow）
            var editWin = new ManagerStationWindow();
            editWin.LoadStation(station.Name, coord.Lon, coord.Lat);
            editWin.Show();
            // 编辑完成后刷新数据（可在窗口关闭时触发，简单起见直接刷新）
            RefreshGrids();
        }

        private void DirectionListPanel_EditDirectionRequested(object sender, DirectionViewModel direction)
        {
            if (direction == null) return;
            var data = documentWorkspace.CurrentTab?.ParsedData;
            if (data == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!data.Directions.TryGetValue(direction.DirId, out var dirInfo))
            {
                MessageBox.Show($"未找到方向 {direction.DirId} 的数据", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var stationNames = data.Stations.Keys.OrderBy(x => x).ToList();
            var existingIds = new HashSet<string>(data.Lines.Keys);

            var dialog = new EditDirectionWindow(
                stationNames,
                existingIds,
                dirInfo.LineId,
                direction.DirId,
                dirInfo.Stations,
                dirInfo.Attrs.ToList(),
                dirInfo.DirectionType
            );
            dialog.Show();
            RefreshGrids();
        }

        private void LineListPanel_EditLineRequested(object sender, LineViewModel line)
        {
            if (line == null) return;
            var data = documentWorkspace.CurrentTab?.ParsedData;
            if (data == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!data.Lines.TryGetValue(line.LineId, out var lineInfo))
            {
                MessageBox.Show($"未找到线路 {line.LineId} 的数据", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var existingIds = new HashSet<string>(data.Lines.Keys);
            var dialog = new EditLineWindow(
                existingIds,
                line.LineId,
                lineInfo.Name,
                lineInfo.Color,
                lineInfo.City,
                lineInfo.Formation ?? "",
                lineInfo.Maxspeed?.ToString() ?? "",
                lineInfo.OpenYear.ToString(),
                lineInfo.PowerKw?.ToString() ?? "",
                lineInfo.Brake?.ToString() ?? "",
                lineInfo.Disabled,
                data.LineLinks.GetValueOrDefault(line.LineId) ?? ""
            );
            dialog.Show();
            RefreshGrids();
        }

        private void OnCurrentTabChanged(object? sender, MetroEditorTab? tab)
        {
            if (tab != null)
            {
                // 更新标题、UI、订阅光标事件等（原 DocumentTabControl_SelectionChanged 中的逻辑）
                UpdateUIFromTab(tab);
                UpdateTitle(tab);
                // 订阅光标变化
                if (tab.Editor is MetroEditorControl newEditor)
                    newEditor.CaretPositionChanged += Editor_CaretPositionChanged;
                // 注意：取消旧订阅需在切换时处理，可在 OnCurrentTabChanged 中维护旧 tab 的引用
            }
        }

        private void OnTabClosing(object? sender, TabClosingEventArgs e)
        {
            // 让宿主决定是否关闭（调用 CheckSave）
            if (!e.Tab.CheckSave())
                e.Cancel = true;
        }

        /// <summary>
        /// 从注册表加载用户配置：字体、最近文件列表等
        /// 设计要点：
        /// 1. 容错处理：无效字体、已删除的最近文件自动过滤
        /// 2. 最近文件去重：新打开的文件置顶，超过10个自动截断
        /// </summary>
        private void LoadSettings()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            if (key == null) return;

            if (key.GetValue("FontSize") is int size && size > 0)
                _defaultFontSize = size;
            if (key.GetValue("FontFamily") is string familyName)
                _defaultFontFamily = familyName;

            // 应用到当前 Tab（如果有）
            if (documentWorkspace.CurrentTab?.Editor is MetroEditorControl editor)
            {
                editor.FontSize = _defaultFontSize;
                try { editor.FontFamily = new FontFamily(_defaultFontFamily); } catch { }
            }


            // 读取最近文件列表（存储为分号分隔的字符串）
            string recentData = key.GetValue(RecentFilesValueName) as string;
            if (!string.IsNullOrEmpty(recentData))
            {
                _recentFiles = recentData.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Where(File.Exists)      // 自动过滤掉已被删除的文件
                                         .ToList();
                if (_recentFiles.Count > MaxRecentFiles)
                    _recentFiles = _recentFiles.Take(MaxRecentFiles).ToList();
                UpdateRecentFilesMenu();
            }
            else
            {
                _recentFiles = new List<string>();
                UpdateRecentFilesMenu();
            }
        }

        private void AddRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            // 移除旧记录（如果存在）
            _recentFiles.Remove(filePath);
            // 插入到最前面
            _recentFiles.Insert(0, filePath);
            // 限制数量
            if (_recentFiles.Count > MaxRecentFiles)
                _recentFiles.RemoveAt(_recentFiles.Count - 1);

            // 写入注册表
            SaveRecentFilesToRegistry();
            // 更新菜单
            UpdateRecentFilesMenu();
        }

        private void SaveRecentFilesToRegistry()
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
            if (key == null) return;

            string data = string.Join(";", _recentFiles);
            key.SetValue(RecentFilesValueName, data);
        }

        private void UpdateRecentFilesMenu()
        {
            mnuRecentFiles.Items.Clear();

            if (_recentFiles.Count == 0)
            {
                mnuRecentFiles.Items.Add(new MenuItem { Header = "(空)", IsEnabled = false });
                return;
            }

            foreach (string path in _recentFiles)
            {
                var item = new MenuItem
                {
                    Header = Path.GetFileName(path),   // 只显示文件名
                    Tag = path,
                    ToolTip = path                     // 完整路径作为提示
                };
                item.Click += RecentFileMenuItem_Click;
                mnuRecentFiles.Items.Add(item);
            }

            // 添加分隔线和“清除列表”命令
            mnuRecentFiles.Items.Add(new Separator());
            var clearItem = new MenuItem { Header = "清除列表" };
            clearItem.Click += (s, e) => ClearRecentFiles();
            mnuRecentFiles.Items.Add(clearItem);
        }

        private void RecentFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            string path = menuItem?.Tag as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                // 文件已被删除，从列表中移除并刷新
                _recentFiles.Remove(path);
                SaveRecentFilesToRegistry();
                UpdateRecentFilesMenu();
                MessageBox.Show("文件不存在，已从最近列表中移除。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 使用现有的加载逻辑（例如 LoadFileFromArgument 或 OpenFile 的类似代码）
            LoadFileFromArgument(path);
        }

        private void ClearRecentFiles()
        {
            _recentFiles.Clear();
            SaveRecentFilesToRegistry();
            UpdateRecentFilesMenu();
        }

        private void LoadFileFromArgument(string filePath)
        {
            var tab = new MetroEditorTab();
            tab.RequestSaveAs += (s, ev) => SaveAsForTab(tab);
            tab.CompilationCompleted += OnTabCompilationCompleted;
            tab.LoadFile(filePath);
            documentWorkspace.AddTab(tab);
            AddRecentFile(filePath);
        }

        private void KeywordPanel_KeywordClicked(object sender, string keyword)
        {
            InsertText(keyword);
        }

        private void SyntaxRefPanel_ItemDoubleClicked(object sender, SyntaxReferenceItem item)
        {
            if (!string.IsNullOrEmpty(item.InsertTemplate))
                InsertText(item.InsertTemplate);
        }

        private void InsertText(string text)
        {
            var editor = documentWorkspace?.CurrentTab?.Editor;
            if (editor == null) return;

            var textEditor = editor.Editor;
            textEditor.Focus();
            int offset = textEditor.TextArea.Caret.Offset;
            textEditor.Document.Insert(offset, text);
            textEditor.TextArea.Caret.Offset = offset + text.Length;
        }

        private void ConfigureCommands()
        {
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, SaveFile_Click));
        }

        private void Window_Closed(object sender, EventArgs e) => Application.Current.Shutdown();
        #endregion

        #region 文件操作
        private void NewFile_Click(object sender, EventArgs e)
        {
            var tab = new MetroEditorTab();
            tab.RequestSaveAs += (s, ev) => SaveAsForTab(tab);
            tab.CompilationCompleted += OnTabCompilationCompleted;
            documentWorkspace.AddTab(tab);   // 替换原来的 _tabs.Add 和 SelectedItem
        }

        private void OpenFile_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Metro文件 (*.metro;*.m)|*.metro;*.m|所有文件 (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                var tab = new MetroEditorTab();
                tab.RequestSaveAs += (s, ev) => SaveAsForTab(tab);
                tab.CompilationCompleted += OnTabCompilationCompleted;
                tab.LoadFile(dlg.FileName);
                documentWorkspace.AddTab(tab);
                AddRecentFile(dlg.FileName);
            }
        }

        private void SaveFile_Click(object sender, EventArgs e)
        {
            if (documentWorkspace.CurrentTab != null)
                documentWorkspace.CurrentTab?.Save();
        }

        private void OnTabCompilationCompleted(object? sender, CompilationEventArgs e)
        {
            var tab = sender as MetroEditorTab;
            if (tab == documentWorkspace.CurrentTab)
            {
                // 更新主窗口的错误列表、地图等
                documentWorkspace.errorListBox.ItemsSource = e.Messages;
                statusInfo.Text = e.StatusText;
                if (e.Root != null)
                {
                    _currentRoot = e.Root;
                    mapControl.LoadData(e.Root);
                    UpdateUI(e.Data);
                }
            }
        }

        private void UpdateTitle(MetroEditorTab tab)
        {
            Title = $"地铁线路编辑器 - {tab.TabHeader}";
        }

        private void UpdateUIFromTab(MetroEditorTab tab)
        {
            // 更新DataGrid、地图等
            if (tab.Root != null)
            {
                _currentRoot = tab.Root;
                mapControl.LoadData(tab.Root);
                UpdateUI(tab.ParsedData);
            }
            documentWorkspace.errorListBox.ItemsSource = tab.Errors;
            statusInfo.Text = tab.StatusText;
        }

        private void SaveAsForTab(MetroEditorTab tab)
        {
            var dlg = new SaveFileDialog { Filter = "Metro文件 (*.metro;*.m)|*.metro;*.m|所有文件 (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                tab.SaveFile(dlg.FileName);
            }
        }

        private void Exit_Click(object sender, EventArgs e)
        {
            if (CheckSave()) Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = !CheckSave();
            base.OnClosing(e);
        }
        /// <summary>
        /// 未保存文件检查：统一的退出/打开文件前置校验
        /// 设计要点：
        /// 1. Modal对话框：使用Yes/No/Cancel标准交互，符合Windows桌面规范
        /// 2. 状态同步：保存后重置_dirty标志，避免重复提示
        /// </summary>
        private bool CheckSave()
        {
            foreach (var tab in documentWorkspace.Tabs.ToList())
            {
                if (!tab.CheckSave())
                    return false;
            }
            return true;
        }

        private void Editor_CaretPositionChanged(object? sender, EventArgs e)
        {
            if (documentWorkspace.CurrentTab?.Editor is MetroEditorControl editor)
            {
                var (line, col) = editor.GetCaretPosition();
                cursorStatus.Text = $"行: {line}, 列: {col}";
            }
        }
        #endregion

        #region 视图菜单

        private void ZoomIn_Click(object sender, EventArgs e)
        {
            if (documentWorkspace.CurrentTab?.Editor is MetroEditorControl editor)
                editor.FontSize = Math.Min(editor.FontSize * 1.2, FontSizeMax);
        }
        private void ZoomOut_Click(object sender, EventArgs e)
        {
            if (documentWorkspace.CurrentTab?.Editor is MetroEditorControl editor)
                editor.FontSize = Math.Max(editor.FontSize / 1.2, FontSizeMin);
        }
        private void ShowPreview_Checked(object sender, EventArgs e) => previewPanel.Visibility = Visibility.Visible;
        private void ShowPreview_Unchecked(object sender, EventArgs e) => previewPanel.Visibility = Visibility.Collapsed;
        private void About_Click(object sender, EventArgs e)
        {
            var about = new AboutWindow();
            about.Show();
        }
        #endregion

        #region 编译/反编译/元数据
        /// <summary>
        /// GZip压缩JSON：生成.bmetro格式文件
        /// 设计要点：
        /// 1. 对称设计：与DecompressJson一一对应，保证编解码一致性
        /// 2. 中文保留：使用UnsafeRelaxedJsonEscaping避免中文被转义
        /// </summary>
        public static byte[] CompressJson(string json)
        {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(json);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
            {
                gzip.Write(utf8Bytes, 0, utf8Bytes.Length);
            }
            return output.ToArray(); // 包含 GZip 标准头（0x1F 0x8B）
        }

        public static string DecompressJson(byte[] compressed)
        {
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private void Compile_Click(object sender, EventArgs e)
        {
            var tab = documentWorkspace.CurrentTab;
            if (tab == null) return;

            string sourceText = tab.Text;
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                MessageBox.Show("编辑器为空，无法编译", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                statusInfo.Text = "正在编译...";

                // ---------- 编译 ----------
                var compiler = new MetroCompiler();
                compiler.Parse(sourceText, tab.BaseDirectory);
                var root = compiler.ExportJson();

                // ---------- 保存对话框：支持四种格式 ----------
                var saveDlg = new SaveFileDialog
                {
                    Filter =
                        "BMETRO压缩JSON文件 (*.bmetro)|*.bmetro|" +
                        "JSON文件 (*.json)|*.json|" +
                        "MBIN嵌入式二进制 (*.mbin)|*.mbin",
                    FileName = $"route_data_{root.KeyCity}",
                    Title = "编译完成，选择导出格式",
                    DefaultExt = ".mbin"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    string ext = Path.GetExtension(saveDlg.FileName).ToLowerInvariant();
                    string fullPath = saveDlg.FileName;

                    switch (ext)
                    {
                        case ".mbin":
                            // -------- 导出 MBIN 二进制 --------
                            byte[] mbinBytes = MBINExporter.Export(root);
                            File.WriteAllBytes(fullPath, mbinBytes);
                            statusInfo.Text = $"MBIN 导出完成: {Path.GetFileName(fullPath)}";
                            break;

                        case ".bmetro":
                            // -------- 导出压缩 JSON (.bmetro) --------
                            var jsonOptions = new JsonSerializerOptions
                            {
                                WriteIndented = false,
                                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            };
                            string json = JsonSerializer.Serialize(root, jsonOptions);
                            byte[] compressed = CompressJson(json);
                            File.WriteAllBytes(fullPath, compressed);
                            statusInfo.Text = $"编译完成，保存至 {Path.GetFileName(fullPath)}";
                            break;

                        case ".json":
                            // -------- 导出明文 JSON (.json) --------
                            var jsonOptions2 = new JsonSerializerOptions
                            {
                                WriteIndented = false,  // 明文 JSON 方便阅读
                                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            };
                            string jsonText = JsonSerializer.Serialize(root, jsonOptions2);
                            File.WriteAllText(fullPath, jsonText, Encoding.UTF8);
                            statusInfo.Text = $"JSON 导出完成: {Path.GetFileName(fullPath)}";
                            break;

                        default:
                            MessageBox.Show($"不支持的文件扩展名: {ext}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                            statusInfo.Text = "导出取消";
                            return;
                    }

                    // 成功提示
                    MessageBox.Show($"导出成功！\n文件已保存至：{fullPath}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    statusInfo.Text = "导出已取消";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"编译或导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                statusInfo.Text = "编译失败";
            }
        }

        private void Decompile_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "编译后地铁数据 (*.bmetro)|*.bmetro|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "选择要反编译的数据文件"
            };
            if (dlg.ShowDialog() == true)
            {
                LoadCompiledFile(dlg.FileName);
            }
        }
        #endregion

        #region 查找/替换
        private void Find_Click(object sender, EventArgs e)
        {
            if (documentWorkspace.CurrentTab?.Editor is MetroEditorControl editor)
            {
                var dialog = new FindReplaceDialog(editor.Editor);
                dialog.Show();
            }
        }
        #endregion

        #region 编辑命令（撤销/重做/剪切/复制/粘贴/全选等）
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.Editor is MetroEditorControl editor && editor.Editor.CanUndo)
                editor.Editor.Undo();
        }
        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.Editor is MetroEditorControl editor && editor.Editor.CanRedo)
                editor.Editor.Redo();
        }
        private void Cut_Click(object sender, RoutedEventArgs e) => documentWorkspace.CurrentTab?.Editor?.Editor?.Cut();
        private void Copy_Click(object sender, RoutedEventArgs e) => documentWorkspace.CurrentTab?.Editor?.Editor?.Copy();
        private void Paste_Click(object sender, RoutedEventArgs e) => documentWorkspace.CurrentTab?.Editor?.Editor?.Paste();
        private void SelectAll_Click(object sender, RoutedEventArgs e) => documentWorkspace.CurrentTab?.Editor?.Editor?.SelectAll();
        private void ToggleWordWrap_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.Editor is MetroEditorControl editor)
                editor.WordWrap = !editor.WordWrap;
        }
        private void ToggleLineNumbers_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.Editor is MetroEditorControl editor)
            {
                editor.ShowLineNumbers = !editor.ShowLineNumbers;
                mnuLineNumbers.IsChecked = editor.ShowLineNumbers;
            }
        }
        #endregion

        #region 站点/线路/方向等的管理操作（添加、编辑）

        private void OpenStationManager_Click(object sender, RoutedEventArgs e)
        {
            OpenStationManager();   // 调用已有的方法
        }

        private void RefreshGrids()
        {
            if (documentWorkspace.CurrentTab?.ParsedData != null)
                UpdateUI(documentWorkspace.CurrentTab?.ParsedData);
        }

        private void OpenStationManager()
        {
            var dialog = new ManagerStationWindow { DefaultCity = documentWorkspace.CurrentTab?.ParsedData?.MainCity ?? DefaultCity };
            if (documentWorkspace.CurrentTab?.ParsedData != null)
            {
                foreach (var item in documentWorkspace.CurrentTab?.ParsedData.Stations)
                {
                    dialog.AddStation(new ManagerStationWindow.StationItem
                    {
                        City = documentWorkspace.CurrentTab?.ParsedData.StationCities.GetValueOrDefault(item.Key, documentWorkspace.CurrentTab?.ParsedData?.MainCity ?? DefaultCity),
                        Name = item.Key,
                        Lat = item.Value.Lat,
                        Lon = item.Value.Lon
                    });
                }
            }
            dialog.Show();
            RefreshGrids();
        }
        #endregion

        #region 其他事件
        private void Settings_Click(object sender, EventArgs e)
        {
            var dialog = new SettingsWindow { Owner = this };
            if (dialog.ShowDialog() == true)
                LoadSettings();
        }

        /// <summary>
        /// 打开时间参数编辑器：依赖已编译的路网数据
        /// 设计要点：
        /// 1. 边界校验：未编译时弹出提示，避免空引用异常
        /// 2. 数据共享：直接传递ParsedData引用，避免重复解析
        /// </summary>
        private void TimeParameterEditor_Click(object sender, RoutedEventArgs e)
        {
            var data = documentWorkspace.CurrentTab?.ParsedData;
            if (data == null)
            {
                MessageBox.Show("请先编译代码以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var win = new TimeParameterEditorWindow(data);
            win.Show();
        }

        private void FareSettings_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.ParsedData == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new FareEditorWindow(documentWorkspace.CurrentTab?.ParsedData);
            dialog.Show();
        }

        private void BatchPending_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.ParsedData == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new BatchPendingWindow(documentWorkspace.CurrentTab?.ParsedData);
            dialog.Show();
        }

        private void EditTransfers_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.ParsedData == null) return;
            var dialog = new TimeParameterEditorWindow(documentWorkspace.CurrentTab?.ParsedData);
            dialog.Show();
        }

        private void EditDwellTimes_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.ParsedData == null) return;
            var dialog = new TimeParameterEditorWindow(documentWorkspace.CurrentTab?.ParsedData);
            dialog.Show();
        }

        private void EditFare_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.ParsedData == null) return;
            var dialog = new FareEditorWindow(documentWorkspace.CurrentTab?.ParsedData);
            dialog.Show();
        }

        private void EditSpecialFare_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.ParsedData == null) return;
            var dialog = new FareEditorWindow(documentWorkspace.CurrentTab?.ParsedData);
            dialog.Show();
        }

        private void BatchCompile_Click(object sender, RoutedEventArgs e)
        {
            var window = new BatchCompileWindow();
            window.Show();
        }

        private void BatchDecompile_Click(object sender, RoutedEventArgs e)
        {
            var window = new BatchDecompileWindow();
            window.Show();
        }

        private void CityInfo_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.ParsedData == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new CityInfoWindow(documentWorkspace.CurrentTab?.ParsedData);
            dialog.Show();
        }

        private void CommuteAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.ParsedData == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new CommuteAnalysisWindow(documentWorkspace.CurrentTab?.Root);
            dialog.Show();
        }

        private void LineDetail_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab?.ParsedData == null)
            {
                MessageBox.Show("请先编译文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var lineIds = documentWorkspace.CurrentTab?.ParsedData.Lines.Keys.ToList();
            var selector = new LineSelectorWindow(lineIds, _lineNameMap);
            selector.Owner = this;
            if (selector.ShowDialog() == true)
            {
                string lineId = selector.SelectedLineId;
                if (!string.IsNullOrEmpty(lineId) && documentWorkspace.CurrentTab.ParsedData.Lines.ContainsKey(lineId))
                {
                    var detailWindow = new LineDetailWindow(documentWorkspace.CurrentTab?.ParsedData, lineId);
                    detailWindow.Show();
                }
                else
                {
                    MessageBox.Show("未找到该线路", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void OpenRouteDetailWindow_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab == null || documentWorkspace.CurrentTab?.ParsedData == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var window = new RouteDetailWindow(documentWorkspace.CurrentTab?.Root);
            window.Show();
        }

        private void MigrateCurrentFile_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab == null) return;
            if (string.IsNullOrEmpty(documentWorkspace.CurrentTab?.Text))
            {
                MessageBox.Show("编辑器为空，没有可迁移的内容", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string newCode = MigrationHelper.Migrate(documentWorkspace.CurrentTab?.Text, documentWorkspace.CurrentTab?.FilePath, out var _, true);
                documentWorkspace.CurrentTab.Text = newCode; // 触发 Tab 内部脏标记
                statusInfo.Text = "迁移成功，编辑器已更新为新语法";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"迁移失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                statusInfo.Text = "迁移失败";
            }
        }

        private void MigrateDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择包含 .metro 文件的目录"
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string dir = dialog.SelectedPath;
                // 询问是否备份
                var result = MessageBox.Show("迁移将覆盖原文件并备份为 .bak，是否继续？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                try
                {
                    int count = MigrationHelper.MigrateDirectory(dir, backupExtension: ".bak");
                    MessageBox.Show($"迁移完成，成功处理 {count} 个文件", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"迁移目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            if (documentWorkspace.CurrentTab == null)
            {
                MessageBox.Show("没有打开的文档", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SaveAsForTab(documentWorkspace.CurrentTab);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (CheckSave()) Close();
        }

        private void OsmImport_Click(object sender, RoutedEventArgs e)
        {
            var window = new OsmImportWindow();
            window.Show();
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string ext = Path.GetExtension(files[0]).ToLowerInvariant();
                    if (ext == ".json" || ext == ".bmetro")
                    {
                        e.Effects = DragDropEffects.Copy;
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                    }
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    LoadFileFromArgument(filePath);
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// 加载编译后的数据文件（.json/.bmetro）
        /// 设计要点：
        /// 1. 反向链路：将运行时数据反编译为源码，打通“编辑-编译-再编辑”闭环
        /// 2. 格式自适应：根据扩展名自动选择解压或直读逻辑
        /// </summary>
        private void LoadCompiledFile(string filePath)
        {
            try
            {
                statusInfo.Text = $"正在加载 {Path.GetFileName(filePath)}...";
                string ext = Path.GetExtension(filePath).ToLowerInvariant();

                string jsonText;
                if (ext == ".bmetro")
                {
                    byte[] compressed = File.ReadAllBytes(filePath);
                    jsonText = DecompressJson(compressed);
                }
                else
                {
                    jsonText = File.ReadAllText(filePath, new UTF8Encoding(false));
                }

                var root = JsonSerializer.Deserialize<JsonRoot>(jsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (root == null) throw new Exception("无法解析数据文件");

                var decompiler = new MetroDecompiler(root);
                string code = decompiler.Decompile();

                // 创建新 Tab 并加载
                var tab = new MetroEditorTab();
                tab.RequestSaveAs += (s, ev) => SaveAsForTab(tab);
                tab.CompilationCompleted += OnTabCompilationCompleted;
                tab.Text = code; // 直接设置文本（不关联文件）
                tab.IsDirty = true; // 标记为已修改
                documentWorkspace.AddTab(tab);

                statusInfo.Text = $"已加载 {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                statusInfo.Text = "加载失败";
            }
        }

        private void OpenIntervalTimeCalculatorWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = new IntervalTimeCalculatorWindow();
            window.Show();
        }

        private void MapEditor_Click(object sender, EventArgs e)
        {
            if (documentWorkspace.CurrentTab?.ParsedData == null || documentWorkspace.CurrentTab?.ParsedData.Stations.Count == 0 || _currentRoot == null)
            {
                MessageBox.Show("请先正确编辑文件以获取站点数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var editor = new MapEditorWindow();
            editor.LoadData(_currentRoot);
            editor.Show();
        }

        /// <summary>
        /// 导出当前站点列表为 CSV 文件（UTF-8 with BOM，便于 Excel 打开）
        /// </summary>
        private void ExportStationsCsv_Click(object sender, RoutedEventArgs e)
        {
            // 1. 检查数据是否存在
            if (documentWorkspace.CurrentTab?.ParsedData == null || documentWorkspace.CurrentTab?.ParsedData.Stations.Count == 0)
            {
                MessageBox.Show("请先编译代码以获取站点数据。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. 选择保存路径
            var dialog = new SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv",
                Title = "导出站点列表",
                FileName = $"站点列表_{documentWorkspace.CurrentTab?.ParsedData.MainCity}_{DateTime.Now:yyyyMMdd}.csv"
            };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                // 3. 构建 CSV 内容
                var sb = new StringBuilder();
                // 添加 BOM（UTF-8 with BOM）
                byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
                using var fs = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write);
                fs.Write(bom, 0, bom.Length);
                using var writer = new StreamWriter(fs, Encoding.UTF8);

                // 写入标题行
                writer.WriteLine("站名,经度,纬度,城市,途经线路");

                // 获取站点数据
                var stations = documentWorkspace.CurrentTab?.ParsedData.Stations;
                var stationCities = documentWorkspace.CurrentTab?.ParsedData.StationCities;
                var mainCity = documentWorkspace.CurrentTab?.ParsedData.MainCity;

                // 预构建线路名称映射
                var lineNameMap = new Dictionary<string, string>();
                foreach (var kv in documentWorkspace.CurrentTab?.ParsedData.Lines)
                {
                    string city = kv.Value.City ?? mainCity;
                    string displayName = (city == mainCity) ? kv.Value.Name : $"{city}{kv.Value.Name}";
                    lineNameMap[kv.Key] = displayName;
                }

                // 遍历站点
                foreach (var kv in stations)
                {
                    string stationName = kv.Key;
                    var coord = kv.Value;
                    double lon = coord.Lon;
                    double lat = coord.Lat;

                    // 获取城市
                    string city = stationCities.TryGetValue(stationName, out var c) ? c : mainCity;

                    // 获取该站经过的线路（通过方向列表收集）
                    var lineIds = documentWorkspace.CurrentTab?.ParsedData.Directions.Values
                        .Where(d => d.Stations.Contains(stationName))
                        .Select(d => d.LineId)
                        .Distinct()
                        .ToList();

                    // 转换为显示名称
                    var lineDisplayNames = lineIds
                        .OrderBy(id => id)
                        .Select(id => lineNameMap.TryGetValue(id, out var display) ? display : id)
                        .ToList();
                    string linesStr = string.Join("; ", lineDisplayNames); // 分号分隔

                    // 写入一行（注意：站名或线路名可能包含逗号，需加引号处理）
                    string escapedName = stationName.Contains(",") ? $"\"{stationName}\"" : stationName;
                    string escapedLines = linesStr.Contains(",") ? $"\"{linesStr}\"" : linesStr;
                    writer.WriteLine($"{escapedName},{lon:F6},{lat:F6},{city},{escapedLines}");
                }

                writer.Flush();
                fs.Close();

                MessageBox.Show($"站点数据已导出至：{dialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                statusInfo.Text = "导出成功";
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("access")) return;
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                statusInfo.Text = "导出失败";
            }
        }
        #endregion


        #region 编译与数据更新

        /// <summary>
        /// 增量更新UI：仅当数据实际变化时才刷新DataGrid
        /// 设计要点：
        /// 1. 深度Diff：比较列表的每个元素，而非仅引用比较
        /// 2. 避免闪烁：减少DataGrid的ItemsSource重置次数，保留滚动位置和选中态
        /// 3. 联动更新：数据变化后同步刷新详情面板和右侧Tab页
        /// </summary>
        private bool UpdateUI(ParsedData data)
        {
            if (data == null) return false;

            _lineNameMap = data.Lines.ToDictionary(
                kv => kv.Key,
                kv =>
                {
                    string city = kv.Value.City ?? data.MainCity;
                    return (city == data.MainCity) ? kv.Value.Name : $"{city}{kv.Value.Name}";
                }
            );

            stationListPanel.ParsedData = data;
            directionListPanel.ParsedData = data;
            lineListPanel.ParsedData = data;
            fareParametersPanel.ParsedData = data;
            timeParametersPanel.ParsedData = data;
            systemInfo.ParsedData = data;

            return true;
        }
        #endregion

        #region 其他事件
        private void GenerateLine_Click(object sender, EventArgs e)
        {
            var data = documentWorkspace.CurrentTab?.ParsedData;
            var existingIds = new HashSet<string>(data?.Lines.Keys ?? Enumerable.Empty<string>());
            var dialog = new EditLineWindow(existingIds) { };
            dialog.Show();
        }

        private void GenerateDirection_Click(object sender, EventArgs e)
        {
            var data = documentWorkspace.CurrentTab?.ParsedData;
            if (data == null)
            {
                MessageBox.Show("请先编译文件以获得站台列表", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var stationNames = data.Stations.Keys.OrderBy(x => x).ToList();
            var existingIds = new HashSet<string>(data.Lines.Keys);
            var dialog = new EditDirectionWindow(stationNames, existingIds);
            dialog.Show();
        }

        protected override void OnClosed(EventArgs e)
        {
            // 取消 DocumentWorkspace 的事件订阅（如果有）
            if (documentWorkspace != null)
            {
                documentWorkspace.CurrentTabChanged -= OnCurrentTabChanged;
                documentWorkspace.TabClosing -= OnTabClosing;
            }

            base.OnClosed(e);
        }
        #endregion
    }
}