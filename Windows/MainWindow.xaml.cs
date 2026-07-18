using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using MetroEditor.MetroLang.Compiler;
using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.Decompiler;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.MetroLang.Tool;
using MetroEditor.Windows.Controls.Style;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Path = System.IO.Path;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 主窗口 — 整合编辑、编译、预览与数据管理
    /// </summary>
    public partial class MainWindow : Window
    {
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
            ConfigureEditor();
            ConfigureCommands();
            InitializeDebounceTimer();
            SetupEventHandlers();
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
                    Dispatcher.BeginInvoke(new Action(() => LoadFileFromArgument(_startupFile)), DispatcherPriority.Background);
                }
            };
            DataContext = this;

            _foldingManager = FoldingManager.Install(textEditor.TextArea);
            textEditor.TextArea.Document.TextChanged += (s, e) =>
            {
                Dispatcher.BeginInvoke(UpdateFoldings, DispatcherPriority.Background);
            };

            mapControl.MapReady += (s, e) =>
            {
                // 地图已准备就绪，可以执行一些操作
                Debug.WriteLine("地图已加载完成");
            };
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

            // 原有字体设置
            if (key.GetValue("FontSize") is int size && size > 0)
                textEditor.FontSize = size;

            if (key.GetValue("FontFamily") is string familyName)
            {
                try { textEditor.FontFamily = new FontFamily(familyName); }
                catch { /* 忽略无效字体 */ }
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

        private void UpdateFoldings()
        {
            var foldings = MetroFoldingGenerator.CreateFolds(textEditor.Document);
            _foldingManager.UpdateFoldings(foldings, int.MaxValue);
        }

        // 可选：Ctrl+M 切换折叠
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var folds = _foldingManager.GetFoldingsAt(textEditor.TextArea.Caret.Offset);
                if (folds.Any())
                {
                    var section = folds.First();
                    section.IsFolded = !section.IsFolded;
                }
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void LoadFileFromArgument(string filePath)
        {
            if (!CheckSave()) return;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".metro")
            {
                try
                {
                    textEditor.Text = File.ReadAllText(filePath, new UTF8Encoding(false));
                    _currentFilePath = filePath;
                    _isDirty = false;
                    UpdateTitle();
                    statusInfo.Text = $"已打开 {Path.GetFileName(filePath)}";
                    AddRecentFile(filePath);   // 记录
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (ext == ".json" || ext == ".bmetro")
            {
                LoadCompiledFile(filePath);
                AddRecentFile(filePath);   // 编译文件同样记录
            }
            else
            {
                MessageBox.Show($"不支持的文件格式: {ext}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SyntaxReferenceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem is SyntaxReferenceItem item)
            {
                if (!string.IsNullOrEmpty(item.InsertTemplate))
                {
                    textEditor.Focus();
                    int offset = textEditor.TextArea.Caret.Offset;
                    textEditor.Document.Insert(offset, item.InsertTemplate);
                    textEditor.TextArea.Caret.Offset = offset + item.InsertTemplate.Length;
                }
            }
        }

        private void ConfigureEditor()
        {
            // 注册自定义高亮
            HighlightingManager.Instance.RegisterHighlighting("Metro", new[] { ".metro" }, new MetroHighlighting());
            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Metro");

            // 文本修改标记
            textEditor.TextChanged += (_, _) => { _isDirty = true; UpdateTitle(); };

            // 光标位置更新
            textEditor.TextArea.Caret.PositionChanged += (_, _) =>
            {
                var caret = textEditor.TextArea.Caret;
                cursorStatus.Text = $"行: {caret.Line}, 列: {caret.Column}";
            };

            keywordItemsControl.ItemsSource = Keywords;
        }

        private void ConfigureCommands()
        {
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, SaveFile_Click));
        }

        private void InitializeDebounceTimer()
        {
            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceIntervalMs) };
            _debounceTimer.Tick += (_, _) =>
            {
                _debounceTimer.Stop();
                DynamicCompile();
            };
        }

        private void SetupEventHandlers()
        {
            textEditor.TextChanged += TextEditor_TextChanged;
        }

        private void Window_Closed(object sender, EventArgs e) => Application.Current.Shutdown();
        #endregion

        #region 关键字插入
        private void KeywordButton_Click(object sender, EventArgs e)
        {
            if (sender is not Button { Tag: string keyword }) return;
            textEditor.Focus();
            int offset = textEditor.TextArea.Caret.Offset;
            textEditor.Document.Insert(offset, keyword);
            textEditor.TextArea.Caret.Offset = offset + keyword.Length;
        }
        #endregion

        #region 错误列表双击定位
        /// <summary>
        /// 错误列表双击定位：跳转到编译错误的对应行
        /// 设计要点：
        /// 1. 正则提取：从错误消息中解析行号，兼容不同格式的错误提示
        /// 2. 边界校验：行号超出文档范围时自动忽略，避免异常
        /// </summary>
        private void ErrorListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (errorListBox.SelectedItem is not CompileMessage msg) return;
            var match = Regex.Match(msg.Text, @"第(\d+)行");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int line))
            {
                var document = textEditor.Document;
                if (line >= 1 && line <= document.LineCount)
                {
                    int offset = document.GetLineByNumber(line).Offset;
                    textEditor.Select(offset, 0);
                    textEditor.ScrollTo(line, 0);
                }
            }
        }
        #endregion

        #region 表格双击插入
        /// <summary>
        /// 表格双击插入：将选中的线路/站点/方向ID插入编辑器
        /// 设计要点：
        /// 1. 多网格适配：通过sender判断当前选中的DataGrid，统一处理逻辑
        /// 2. 光标定位：插入后自动移动光标到插入内容末尾，提升编辑效率
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid grid || grid.SelectedItem == null) return;

            string insertText = null;

            if (grid == directionGrid && grid.SelectedItem is DirectionViewModel dir)
            {
                insertText = dir.DirId;
            }
            else if (grid == stationGrid && grid.SelectedItem is StationViewModel sta)
            {
                string name = sta.Name;
                // 若显示名为 "站名 (城市)" 格式，则转换为 "城市::站名"
                if (name.EndsWith(")") && name.Contains("("))
                {
                    int openParen = name.LastIndexOf('(');
                    int closeParen = name.LastIndexOf(')');
                    if (openParen > 0 && closeParen > openParen)
                    {
                        string stationName = name.Substring(0, openParen).Trim();
                        string city = name.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                        if (!string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(stationName))
                        {
                            insertText = $"'{city}::{stationName}'";
                        }
                    }
                }
                // 若不满足转换条件，则原样插入站名
                if (string.IsNullOrEmpty(insertText))
                {
                    insertText = name;
                }
            }
            else if (grid == lineGrid && grid.SelectedItem is LineViewModel line)
            {
                insertText = line.LineId;
            }

            if (string.IsNullOrEmpty(insertText))
                return;

            textEditor.Focus();
            int offset = textEditor.TextArea.Caret.Offset;
            textEditor.Document.Insert(offset, insertText);
            textEditor.TextArea.Caret.Offset = offset + insertText.Length;
        }
        #endregion

        #region 详情面板构建
        private void DirectionGrid_SelectionChanged(object sender, SelectionChangedEventArgs? e)
        {
            if (directionGrid.SelectedItem is DirectionViewModel dirVM &&
                _currentParsedData?.Directions.TryGetValue(dirVM.DirId, out var dirInfo) == true)
            {
                directionDetailText.Text = BuildDirectionDetail(dirVM, dirInfo);
            }
            else
                directionDetailText.Text = "请选择一个方向查看详情";
        }

        private void StationGrid_SelectionChanged(object sender, SelectionChangedEventArgs? e)
        {
            if (stationGrid.SelectedItem is StationViewModel staVM &&
                _currentParsedData?.Stations.TryGetValue(staVM.Name, out var coord) == true)
            {
                stationDetailText.Text = BuildStationDetail(staVM, coord);
            }
            else
                stationDetailText.Text = "请选择一个站台查看详情";
        }

        private void LineGrid_SelectionChanged(object sender, SelectionChangedEventArgs? e)
        {
            if (lineGrid.SelectedItem is LineViewModel lineVM &&
                _currentParsedData?.Lines.TryGetValue(lineVM.LineId, out var lineInfo) == true)
            {
                lineDetailText.Text = BuildLineDetail(lineVM, lineInfo);
            }
            else
                lineDetailText.Text = "请选择一条线路查看详情";
        }

        private string BuildDirectionDetail(DirectionViewModel dirVM, DirectionInfo dirInfo)
        {
            var displayMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["BRANCH"] = "支线",
                ["FAST"] = "快车",
                ["FUTURE"] = "建设中/规划中"
            };

            var sb = new StringBuilder();
            sb.AppendLine($"方向 ID: {dirVM.DirId}");
            sb.AppendLine($"线路: {dirVM.DisplayLine}");
            var displayParts = dirVM.Attrs.Split(' ')
                .Where(attr => displayMap.ContainsKey(attr))
                .Select(attr => displayMap[attr])
                .ToList();
            string displayText = displayParts.Any() ? string.Join(" ", displayParts) : "常规";
            sb.AppendLine($"属性: {displayText}");
            if (dirInfo.Attrs.Contains("LOOP"))
            {
                string loopText = dirInfo.DirectionType?.ToUpperInvariant() == "INNER" ? "内环" : "外环";
                sb.AppendLine($"环线方向: {loopText ?? "未指定"}");
            }
            sb.AppendLine($"起点: {dirVM.Start}");
            sb.AppendLine($"终点: {dirVM.End}");
            sb.AppendLine($"站点列表 ({dirInfo.Stations.Count} 站):");

            var dists = _currentParsedData?.Distances?.GetValueOrDefault(dirVM.DirId) ?? new List<double>();
            bool isLoop = dirInfo.Attrs.Contains("LOOP");
            for (int i = 0; i < dirInfo.Stations.Count; i++)
            {
                string line = $"  {i + 1}. {dirInfo.Stations[i]}";
                if (i < dists.Count)
                {
                    double dist = dists[i];
                    if (dist < 1e30)
                    {
                        line += $" -> {dist:F2} km";
                    }
                    else
                    {
                        line += " [终点]";
                    }
                }
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        private string BuildStationDetail(StationViewModel staVM, (double Lon, double Lat) coord)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"站名: {staVM.Name}");
            sb.AppendLine($"城市: {staVM.City}");
            sb.AppendLine($"坐标: ({coord.Lon:F6}, {coord.Lat:F6})");

            // 转换线路ID为中文名称
            var lineIdList = staVM.Lines.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(s => s.Trim())
                                         .Where(s => !string.IsNullOrEmpty(s))
                                         .ToList();
            var tagList = staVM.Lines.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Select(s => {
                    foreach (var a in _lineNameMap)
                    {
                        if (a.Value == s || a.Value == _currentParsedData.MainCity + s) return a.Key;
                    }
                    return "";
                })
                .ToList();
            var lineNames = new List<string>();
            if (_currentParsedData != null)
            {
                foreach (var lid in lineIdList)
                {
                    if (_currentParsedData.Lines.TryGetValue(lid, out var lineInfo))
                    {
                        // 构建显示名称：城市名 + 线路名（如 "广州1号线"），如果城市与主城市相同则省略城市名
                        string city = lineInfo.City ?? _currentParsedData.MainCity;
                        string displayName = (city == _currentParsedData.MainCity) ? lineInfo.Name : $"{city}{lineInfo.Name}";
                        lineNames.Add(displayName);
                    }
                    else
                    {
                        lineNames.Add(lid); // 未找到则保留ID
                    }
                }
            }
            else
            {
                lineNames = lineIdList; // 无数据时保持原样
            }

            sb.AppendLine($"途经线路: {string.Join(", ", lineNames)}");

            sb.AppendLine("换乘信息:");
            if (lineIdList.Count < 2)
            {
                sb.AppendLine("  无 (仅一条线路)");
            }
            else
            {
                double defaultTimeMin = _currentParsedData?.DefaultInStationTime / 60.0 ?? 3.0;
                var explicitTransfers = _currentParsedData?.Transfers?.Where(t => t.Station == staVM.Name).ToList();

                for (int i = 0; i < lineIdList.Count; i++)
                {
                    for (int j = i + 1; j < lineIdList.Count; j++)
                    {
                        string l1 = tagList[i], l2 = tagList[j];
                        string ln1 = lineNames[i], ln2 = lineNames[j];
                        var match = explicitTransfers?.FirstOrDefault(t =>
                            (t.L1 == l1 && t.L2 == l2) || (t.L1 == l2 && t.L2 == l1));

                        string status = match?.Status ?? "operating";
                        if (status == "operating") status = "站内换乘";
                        if (status == "pending") status = "暂缓开通";
                        if (status == "out_station") status = "出站换乘";
                        string timeDisplay = match?.Time.HasValue == true
                            ? $", {match.Time.Value / 60:F1} 分钟"
                            : $", {defaultTimeMin:F1} 分钟";
                        sb.AppendLine($"  {ln1} <-> {ln2}: {status}{timeDisplay}");
                    }
                }
            }

            return sb.ToString();
        }

        private string BuildLineDetail(LineViewModel lineVM, LineInfo lineInfo)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"线路 ID: {lineVM.LineId}");
            sb.AppendLine($"名称: {lineVM.Name}");
            sb.AppendLine($"城市: {lineVM.City}");
            sb.AppendLine($"颜色: {lineVM.Color}");
            sb.AppendLine($"编组: {lineVM.Formation ?? "未指定"}");
            sb.AppendLine($"最高速度: {lineVM.Maxspeed?.ToString() + " km/h" ?? "未指定"}");
            sb.AppendLine($"开通年份: {lineVM.OpenYear}");
            sb.AppendLine($"运行功率: {lineVM.PowerKw?.ToString("F2") + " kW²" ?? "未指定"}");
            sb.AppendLine($"刹车加速度: {lineVM.Brake?.ToString("F2") + " m/s²" ?? "未指定"}");
            sb.AppendLine($"状态: {(lineVM.Opened ? "运营中" : "未开通")}");

            var dirs = _currentParsedData?.Directions
                .Where(kv => kv.Value.LineId == lineVM.LineId)
                .ToList();
            if (dirs?.Any() == true)
            {
                sb.AppendLine($"方向数: {dirs.Count}");
                foreach (var kv in dirs)
                {
                    var dir = kv.Value;
                    string attrs = dir.Attrs.Count > 0 ? string.Join(", ", dir.Attrs) : "无";
                    string start = dir.Stations.FirstOrDefault() ?? "";
                    string end = dir.Stations.LastOrDefault() ?? "";
                    sb.AppendLine($"  {kv.Key}: {start} -> {end} [{attrs}] ({dir.Stations.Count}站)");
                }
            }
            else
                sb.AppendLine("方向数: 无");

            return sb.ToString();
        }
        #endregion

        #region 文件操作
        private void NewFile_Click(object sender, EventArgs e)
        {
            if (!CheckSave()) return;
            textEditor.Text = string.Empty;
            _currentFilePath = string.Empty;
            _isDirty = false;
            UpdateTitle();
        }

        private void OpenFile_Click(object sender, EventArgs e)
        {
            if (!CheckSave()) return;
            var dlg = new OpenFileDialog { Filter = "Metro文件 (*.metro)|*.metro|所有文件 (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    textEditor.Text = File.ReadAllText(dlg.FileName, new UTF8Encoding(false));
                    _currentFilePath = dlg.FileName;
                    _isDirty = false;
                    UpdateTitle();
                    AddRecentFile(dlg.FileName);   // 记录
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveFile_Click(object sender, EventArgs? e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
                SaveAsFile_Click(sender, e);
            else
            {
                try
                {
                    File.WriteAllText(_currentFilePath, textEditor.Text, Encoding.UTF8);
                    _isDirty = false;
                    UpdateTitle();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAsFile_Click(object sender, EventArgs? e)
        {
            var dlg = new SaveFileDialog { Filter = "Metro文件 (*.metro)|*.metro|所有文件 (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                _currentFilePath = dlg.FileName;
                SaveFile_Click(sender, e);
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
            if (!_isDirty) return true;
            var result = MessageBox.Show("当前文件已修改，是否保存？", "保存提示",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.OK)
            {
                SaveFile_Click(this, null);
                return !_isDirty;
            }
            return result != MessageBoxResult.Cancel;
        }

        private void UpdateTitle()
        {
            string fileName = string.IsNullOrEmpty(_currentFilePath) ? "未命名" : Path.GetFileName(_currentFilePath);
            Title = $"MetroLang 线路编辑器 - {fileName}{(_isDirty ? "*" : "")}";
        }
        #endregion

        #region 视图菜单
        private void Wrap_Checked(object sender, EventArgs e) => textEditor.WordWrap = true;
        private void Wrap_Unchecked(object sender, EventArgs e) => textEditor.WordWrap = false;
        private void ZoomIn_Click(object sender, EventArgs e) => textEditor.FontSize = Math.Min(textEditor.FontSize * 1.2, FontSizeMax);
        private void ZoomOut_Click(object sender, EventArgs e) => textEditor.FontSize = Math.Max(textEditor.FontSize / 1.2, FontSizeMin);
        private void LineNumbers_Checked(object sender, EventArgs e) => textEditor.ShowLineNumbers = true;
        private void LineNumbers_Unchecked(object sender, EventArgs e) => textEditor.ShowLineNumbers = false;
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

        private static string DecompressJson(byte[] compressed)
        {
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private void Compile_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textEditor.Text))
            {
                MessageBox.Show("编辑器为空，无法编译", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new SaveFileDialog
            {
                Filter = "编译后地铁数据 (*.bmetro)|*.bmetro|JSON 文件 (*.json)|*.json",
                Title = "保存编译结果"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string filePath = dlg.FileName;
                    statusInfo.Text = "正在编译...";

                    var parser = new MetorCompiler();
                    parser.Parse(textEditor.Text, Path.GetDirectoryName(_currentFilePath));
                    var root = parser.ExportJson();

                    // 序列化为 JSON（紧凑，保留中文字符）
                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    string json = JsonSerializer.Serialize(root, jsonOptions);

                    string ext = Path.GetExtension(filePath).ToLowerInvariant();
                    if (ext == ".json")
                    {
                        File.WriteAllText(filePath, json, Encoding.UTF8);
                    }
                    else if (ext == ".bmetro")
                    {
                        byte[] compressed = CompressJson(json);
                        Console.WriteLine("前两字节: {0:X2} {1:X2}", compressed[0], compressed[1]);
                        File.WriteAllBytes(filePath, compressed);
                    }
                    else
                    {
                        // 未知扩展名，可提示或默认保存为 bmetro
                        byte[] compressed = CompressJson(json);
                        File.WriteAllBytes(filePath, compressed);
                    }

                    statusInfo.Text = $"编译完成，保存至 {Path.GetFileName(filePath)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"编译失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    statusInfo.Text = DefaultStatus;
                }
            }
        }

        private void Decompile_Click(object sender, EventArgs e)
        {
            if (!CheckSave()) return;

            // 1. 修改过滤器，同时支持 .json 和 .bmetro
            var dlg = new OpenFileDialog
            {
                Filter = "编译后地铁数据 (*.bmetro)|*.bmetro|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "选择要反编译的数据文件"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    statusInfo.Text = "正在反编译...";
                    string filePath = dlg.FileName;
                    string ext = Path.GetExtension(filePath).ToLowerInvariant();

                    string jsonText;
                    if (ext == ".bmetro")
                    {
                        // 读取二进制压缩数据并解压
                        byte[] compressed = File.ReadAllBytes(filePath);
                        jsonText = DecompressJson(compressed);   // 与 CompressJson 对称
                    }
                    else // .json 或其它（默认按 JSON 处理）
                    {
                        jsonText = File.ReadAllText(filePath, new UTF8Encoding(false));
                    }

                    var root = JsonSerializer.Deserialize<JsonRoot>(jsonText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    var decompiler = new MetroDecompiler(root ?? new JsonRoot());
                    textEditor.Text = decompiler.Decompile();

                    _currentFilePath = string.Empty;
                    _isDirty = true;
                    UpdateTitle();
                    statusInfo.Text = $"反编译完成，已加载 {Path.GetFileName(filePath)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"反编译失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    statusInfo.Text = DefaultStatus;
                }
            }
        }
        #endregion

        #region 查找/替换
        private void Find_Click(object sender, EventArgs e)
        {
            var dialog = new FindReplaceDialog(textEditor);
            dialog.Show();
        }
        #endregion

        #region 编辑命令（撤销/重做/剪切/复制/粘贴/全选等）
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (textEditor.CanUndo) textEditor.Undo();
        }
        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (textEditor.CanRedo) textEditor.Redo();
        }
        private void Cut_Click(object sender, RoutedEventArgs e) => textEditor.Cut();
        private void Copy_Click(object sender, RoutedEventArgs e) => textEditor.Copy();
        private void Paste_Click(object sender, RoutedEventArgs e) => textEditor.Paste();
        private void SelectAll_Click(object sender, RoutedEventArgs e) => textEditor.SelectAll();
        private void ToggleWordWrap_Click(object sender, RoutedEventArgs e) => textEditor.WordWrap = !textEditor.WordWrap;
        private void ToggleLineNumbers_Click(object sender, RoutedEventArgs e)
        {
            textEditor.ShowLineNumbers = !textEditor.ShowLineNumbers;
            mnuLineNumbers.IsChecked = textEditor.ShowLineNumbers;
        }
        #endregion

        #region 站点/线路/方向等的管理操作（添加、编辑）
        private void StationGrid_Edit_Click(object sender, RoutedEventArgs e)
        {
            if (stationGrid.SelectedItem is not StationViewModel station)
            {
                MessageBox.Show("请先选择要编辑的站台", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string defaultCity = _currentParsedData?.MainCity ?? DefaultCity;
            var item = new ManagerStationWindow.StationItem
            {
                Name = station.Name,
                Lon = station.Lon,
                Lat = station.Lat,
                City = station.City
            };
            var dialog = new EditStationWindow(item, defaultCity);
            dialog.Show();
        }

        private void StationGrid_Add_Click(object sender, RoutedEventArgs e)
        {
            string defaultCity = _currentParsedData?.MainCity ?? DefaultCity;
            var dialog = new EditStationWindow(defaultCity);
            dialog.Show();
        }

        private void OpenStationManager_Click(object sender, RoutedEventArgs e)
        {
            OpenStationManager();   // 调用已有的方法
        }

        // 以下方法供右键菜单命令使用，通过 CommandParameter 传递对象
        private void CanEditItem(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = e.Parameter != null || GetCurrentDataGrid()?.SelectedItem != null;
        }

        private void EditItem_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var item = e.Parameter ?? GetCurrentDataGrid()?.SelectedItem;
            if (item == null) return;

            switch (item)
            {
                case StationViewModel st: EditStation(st); break;
                case DirectionViewModel dir: EditDirection(dir); break;
                case LineViewModel line: EditLine(line); break;
            }
        }

        private void AddItem_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var grid = GetCurrentDataGrid();
            if (grid == null) return;
            if (grid == stationGrid) OpenStationManager();
        }

        private DataGrid? GetCurrentDataGrid()
        {
            if (FocusManager.GetFocusedElement(this) is FrameworkElement child)
                return FindParent<DataGrid>(child);
            return null;
        }

        private static T? FindParent<T>(FrameworkElement child) where T : FrameworkElement
        {
            var parent = VisualTreeHelper.GetParent(child) as FrameworkElement;
            while (parent != null)
            {
                if (parent is T result) return result;
                parent = VisualTreeHelper.GetParent(parent) as FrameworkElement;
            }
            return null;
        }

        private void EditStation(StationViewModel station)
        {
            if (_currentParsedData == null) return;
            if (!_currentParsedData.Stations.TryGetValue(station.Name, out var coord)) return;

            var editWin = new ManagerStationWindow();
            editWin.LoadStation(station.Name, coord.Lon, coord.Lat);
            editWin.Show();
            RefreshGrids();
        }

        private void EditDirection(DirectionViewModel direction)
        {
            if (_currentParsedData == null) return;
            if (!_currentParsedData.Directions.TryGetValue(direction.DirId, out var dirInfo)) return;

            var stationNames = _currentParsedData.Stations.Keys.OrderBy(x => x).ToList();
            var existingIds = new HashSet<string>(_currentParsedData.Lines.Keys);
            var dialog = new EditDirectionWindow(
                stationNames, existingIds,
                dirInfo.LineId, direction.DirId,
                dirInfo.Stations, dirInfo.Attrs.ToList(), dirInfo.DirectionType);
            dialog.Show();
            RefreshGrids();
        }

        private void EditLine(LineViewModel line)
        {
            if (_currentParsedData == null) return;
            if (!_currentParsedData.Lines.TryGetValue(line.LineId, out var lineInfo)) return;

            var existingIds = new HashSet<string>(_currentParsedData.Lines.Keys);
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
                _currentParsedData.LineLinks.GetValueOrDefault(line.LineId) ?? "");
            dialog.Show();
            RefreshGrids();
        }

        private void RefreshGrids()
        {
            if (_currentParsedData != null)
                UpdateUI(_currentParsedData);
        }

        private void OpenStationManager()
        {
            var dialog = new ManagerStationWindow { DefaultCity = _currentParsedData?.MainCity ?? DefaultCity };
            if (_currentParsedData != null)
            {
                foreach (var item in _currentParsedData.Stations)
                {
                    dialog.AddStation(new ManagerStationWindow.StationItem
                    {
                        City = _currentParsedData.StationCities.GetValueOrDefault(item.Key, _currentParsedData?.MainCity ?? DefaultCity),
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
            if (_currentParsedData == null)
            {
                MessageBox.Show("请先编译代码以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var win = new TimeParameterEditorWindow(_currentParsedData);
            win.Show();
        }

        private void FareSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_currentParsedData == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new FareEditorWindow(_currentParsedData);
            dialog.Show();
        }

        private void BatchPending_Click(object sender, RoutedEventArgs e)
        {
            if (_currentParsedData == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new BatchPendingWindow(_currentParsedData);
            dialog.Show();
        }

        private void EditTransfers_Click(object sender, RoutedEventArgs e)
        {
            if (_currentParsedData == null) return;
            var dialog = new TimeParameterEditorWindow(_currentParsedData);
            dialog.Show();
        }

        private void EditDwellTimes_Click(object sender, RoutedEventArgs e)
        {
            if (_currentParsedData == null) return;
            var dialog = new TimeParameterEditorWindow(_currentParsedData);
            dialog.Show();
        }

        private void EditFare_Click(object sender, RoutedEventArgs e)
        {
            if (_currentParsedData == null) return;
            var dialog = new FareEditorWindow(_currentParsedData);
            dialog.Show();
        }

        private void EditSpecialFare_Click(object sender, RoutedEventArgs e)
        {
            if (_currentParsedData == null) return;
            var dialog = new FareEditorWindow(_currentParsedData);
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
            if (_currentParsedData == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new CityInfoWindow(_currentParsedData);
            dialog.Show();
        }

        private void CommuteAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (_currentParsedData == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new CommuteAnalysisWindow(_currentParsedData);
            dialog.Show();
        }

        private void LineDetail_Click(object sender, RoutedEventArgs e)
        {
            if (_currentParsedData == null)
            {
                MessageBox.Show("请先编译文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var lineIds = _currentParsedData.Lines.Keys.ToList();
            var selector = new LineSelectorWindow(lineIds, _lineNameMap);
            selector.Owner = this;
            if (selector.ShowDialog() == true)
            {
                string lineId = selector.SelectedLineId;
                if (!string.IsNullOrEmpty(lineId) && _currentParsedData.Lines.ContainsKey(lineId))
                {
                    var detailWindow = new LineDetailWindow(_currentParsedData, lineId);
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
            if (_currentParsedData == null)
            {
                MessageBox.Show("请先编译文件以获取数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var window = new RouteDetailWindow(_currentParsedData);
            window.Show();
        }

        private void MigrateCurrentFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(textEditor.Text))
            {
                MessageBox.Show("编辑器为空，没有可迁移的内容", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 调用迁移助手，若失败会抛出异常
                string newCode = MigrationHelper.Migrate(textEditor.Text, _currentFilePath, out var _, true);

                // 替换编辑器内容
                textEditor.Text = newCode;
                _isDirty = true;
                UpdateTitle();

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
            var dlg = new SaveFileDialog { Filter = "Metro文件 (*.metro)|*.metro|所有文件 (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                _currentFilePath = dlg.FileName;
                SaveFile_Click(sender, e);
            }
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
                else // .json
                {
                    jsonText = File.ReadAllText(filePath, new UTF8Encoding(false));
                }

                var root = JsonSerializer.Deserialize<JsonRoot>(jsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (root == null)
                    throw new Exception("无法解析数据文件");

                var decompiler = new MetroDecompiler(root);
                textEditor.Text = decompiler.Decompile();

                _currentFilePath = string.Empty; // 不关联源文件
                _isDirty = true;
                UpdateTitle();
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
            if (_currentParsedData == null || _currentParsedData.Stations.Count == 0 || _currentRoot == null)
            {
                MessageBox.Show("请先正确编辑文件以获取站点数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var editor = new MapEditorWindow();
            editor.LoadData(_currentRoot);
            editor.Show();
        }
        #endregion
    }
}