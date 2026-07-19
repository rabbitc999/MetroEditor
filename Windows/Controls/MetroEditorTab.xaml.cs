using ICSharpCode.AvalonEdit.Folding;
using MetroEditor.MetroLang.Compiler;
using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.Windows.Controls;
using MetroEditor.Windows.Controls.Style;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MetroEditor.Windows.Controls
{
    /// <summary>
    /// 多文档编辑标签页，包含独立的编辑器、编译状态和错误列表
    /// </summary>
    public partial class MetroEditorTab : UserControl, INotifyPropertyChanged
    {
        #region 依赖属性（便于绑定）

        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(MetroEditorTab),
                new PropertyMetadata(string.Empty, (d, e) =>
                {
                    var tab = (MetroEditorTab)d;
                    tab.UpdateTabHeader();
                }));

        public static readonly DependencyProperty IsDirtyProperty =
            DependencyProperty.Register(nameof(IsDirty), typeof(bool), typeof(MetroEditorTab),
                new PropertyMetadata(false, (d, e) =>
                {
                    var tab = (MetroEditorTab)d;
                    tab.UpdateTabHeader();
                }));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(MetroEditorTab),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public string FilePath
        {
            get => (string)GetValue(FilePathProperty);
            set => SetValue(FilePathProperty, value);
        }

        public bool IsDirty
        {
            get => (bool)GetValue(IsDirtyProperty);
            set => SetValue(IsDirtyProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TabHeaderProperty =
            DependencyProperty.Register(nameof(TabHeader), typeof(string), typeof(MetroEditorTab),
                new PropertyMetadata("未命名"));

        public string TabHeader
        {
            get => (string)GetValue(TabHeaderProperty);
            private set => SetValue(TabHeaderProperty, value);
        }

        private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tab = (MetroEditorTab)d;
            tab.RaisePropertyChanged(nameof(FileName));
            tab.RaisePropertyChanged(nameof(TabHeader));
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 外部修改文本时同步到编辑器（例如加载文件）
            var tab = (MetroEditorTab)d;
            if (!tab._suppressTextSync)
                tab.editorControl.Text = e.NewValue as string ?? string.Empty;
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 显示在标签页头部的文件名（不含路径）
        /// </summary>
        public string FileName => string.IsNullOrEmpty(FilePath) ? "未命名" : Path.GetFileName(FilePath);

        /// <summary>
        /// 当前文件的目录（用于 include 等相对路径解析）
        /// </summary>
        public string BaseDirectory => string.IsNullOrEmpty(FilePath) ? Environment.CurrentDirectory : Path.GetDirectoryName(FilePath);

        /// <summary>
        /// 最后一次编译成功的 ParsedData
        /// </summary>
        public ParsedData? ParsedData { get; private set; }

        /// <summary>
        /// 最后一次编译成功的 JsonRoot
        /// </summary>
        public JsonRoot? Root { get; private set; }

        /// <summary>
        /// 当前编译错误/信息列表
        /// </summary>
        public ObservableCollection<CompileMessage> Errors { get; } = new();

        /// <summary>
        /// 编译状态文本（用于状态栏）
        /// </summary>
        public string StatusText { get; private set; } = "就绪";

        /// <summary>
        /// 暴露内部编辑器供外部高级操作
        /// </summary>
        public MetroEditorControl Editor => editorControl;

        #endregion

        #region 事件

        /// <summary>
        /// 编译完成时触发（无论成功或失败）
        /// </summary>
        public event EventHandler<CompilationEventArgs>? CompilationCompleted;

        /// <summary>
        /// 请求另存为（由宿主处理）
        /// </summary>
        public event EventHandler? RequestSaveAs;

        #endregion

        #region 字段

        private DispatcherTimer _debounceTimer;
        private readonly object _compileLock = new();
        private bool _isCompiling;
        private bool _suppressTextSync;  // 防止循环更新

        #endregion

        #region 构造函数与初始化

        public MetroEditorTab()
        {
            InitializeComponent();
            Dispatcher.BeginInvoke(new Action(() => UpdateTabHeader()), DispatcherPriority.Background);
            ConfigureEditor();
            InitializeDebounceTimer();
            Loaded += OnLoaded;
        }

        public void UpdateTabHeader()
        {
            string fileName = string.IsNullOrEmpty(FilePath) ? "未命名" : Path.GetFileName(FilePath);
            TabHeader = fileName + (IsDirty ? "*" : "");
        }

        private void ConfigureEditor()
        {
            // 同步编辑器的 Text 变化到本控件的 Text 属性，并触发脏标记
            editorControl.TextChanged += (s, e) =>
            {
                if (!_suppressTextSync)
                {
                    SetValue(TextProperty, editorControl.Text);
                    if (!IsDirty)
                    {
                        IsDirty = true;
                        RaisePropertyChanged(nameof(TabHeader));
                    }
                }
                // 触发防抖编译
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };

            // 编辑器内容加载后，同步一次（确保初始状态）
            editorControl.Loaded += (s, e) =>
            {
                if (!string.IsNullOrEmpty(editorControl.Text) && editorControl.Text != Text)
                {
                    _suppressTextSync = true;
                    SetValue(TextProperty, editorControl.Text);
                    _suppressTextSync = false;
                }
            };

            // 光标位置变化等可在此扩展
        }

        private void InitializeDebounceTimer()
        {
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) // 防抖间隔
            };
            _debounceTimer.Tick += (_, _) =>
            {
                _debounceTimer.Stop();
                DynamicCompile();
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 加载完成后自动触发一次编译
            DynamicCompile();
        }

        #endregion

        #region 动态编译（核心）

        /// <summary>
        /// 异步执行编译，防抖触发
        /// </summary>
        private async void DynamicCompile()
        {
            if (_isCompiling) return;
            lock (_compileLock)
            {
                if (_isCompiling) return;
                _isCompiling = true;
            }

            string text = Text;
            string baseDir = BaseDirectory;
            var dispatcher = Dispatcher;

            try
            {
                var result = await Task.Run(() => CompileInBackground(text, baseDir));
                dispatcher.Invoke(() => ApplyCompileResult(result));
            }
            finally
            {
                lock (_compileLock) _isCompiling = false;
            }
        }

        /// <summary>
        /// 后台编译逻辑（纯CPU，无UI依赖）
        /// </summary>
        private (List<CompileMessage> Messages, string StatusText, ParsedData? Data, JsonRoot? Root) CompileInBackground(string text, string baseDir)
        {
            var messages = new List<CompileMessage>();
            ParsedData? data = null;
            JsonRoot? root = null;
            string status = "就绪";

            if (string.IsNullOrWhiteSpace(text))
            {
                messages.Add(new CompileMessage { ColorBrush = Brushes.Gray, Text = "请输入 MetroLang 代码" });
                status = "等待输入";
                return (messages, status, data, root);
            }

            var compiler = new MetroCompiler();
            try
            {
                compiler.Parse(text, baseDir);
                data = compiler.GetParsedData();
                root = compiler.ExportJson();
                string successMsg = $"编译成功 | 线路: {data.Lines.Count}，站台: {data.Stations.Count}，方向: {data.Directions.Count}";
                messages.Add(new CompileMessage { ColorBrush = Brushes.Green, Text = successMsg });
                status = successMsg;

                // 警告信息
                foreach (var w in compiler.Warnings)
                    messages.Add(new CompileMessage { ColorBrush = Brushes.Orange, Text = w });
            }
            catch (Exception ex)
            {
                var errorLines = ex.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in errorLines)
                    messages.Add(new CompileMessage { ColorBrush = Brushes.Red, Text = line.Trim() });
                int errorCount = errorLines.Length;
                status = $"编译失败，{errorCount} 个错误";
            }

            return (messages, status, data, root);
        }

        /// <summary>
        /// 应用编译结果到UI（UI线程）
        /// </summary>
        private void ApplyCompileResult((List<CompileMessage> Messages, string StatusText, ParsedData? Data, JsonRoot? Root) result)
        {
            // 更新错误列表
            Errors.Clear();
            foreach (var msg in result.Messages)
                Errors.Add(msg);
            errorListBox.ItemsSource = Errors; // 可用于显示

            // 更新状态
            StatusText = result.StatusText;
            ParsedData = result.Data;
            Root = result.Root;

            // 触发事件，通知宿主（如主窗口更新地图、状态栏等）
            CompilationCompleted?.Invoke(this, new CompilationEventArgs(result.Messages, result.StatusText, result.Data, result.Root));

            // 刷新脏标记（编译本身不改变脏状态）
        }

        #endregion

        #region 文件操作

        /// <summary>
        /// 加载文件到编辑器
        /// </summary>
        public void LoadFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("文件不存在", path);

            _suppressTextSync = true;
            try
            {
                string content = File.ReadAllText(path, new UTF8Encoding(false));
                editorControl.Text = content;
                SetValue(TextProperty, content);
                FilePath = path;
                IsDirty = false;
                RaisePropertyChanged(nameof(TabHeader));
            }
            finally
            {
                _suppressTextSync = false;
            }
            // 加载后触发编译
            DynamicCompile();
        }

        /// <summary>
        /// 保存到当前文件路径
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(FilePath))
                throw new InvalidOperationException("未指定文件路径，请使用 SaveAs 或先设置 FilePath");
            SaveFile(FilePath);
        }

        /// <summary>
        /// 保存到指定路径
        /// </summary>
        public void SaveFile(string path)
        {
            File.WriteAllText(path, Text, Encoding.UTF8);
            FilePath = path;
            IsDirty = false;
            RaisePropertyChanged(nameof(TabHeader));
        }

        /// <summary>
        /// 另存为（触发 RequestSaveAs 事件由宿主处理）
        /// </summary>
        public void SaveAs()
        {
            RequestSaveAs?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 检查是否已保存，若未保存则弹出提示
        /// </summary>
        /// <returns>true 表示可以继续（已保存或取消），false 表示取消操作</returns>
        public bool CheckSave()
        {
            if (!IsDirty) return true;
            var result = MessageBox.Show($"文件 “{FileName}” 已修改，是否保存？", "保存提示",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Save();
                    return true;
                }
                catch
                {
                    // 保存失败，询问另存为
                    var saveAs = MessageBox.Show("保存失败，是否另存为？", "错误", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (saveAs == MessageBoxResult.Yes)
                    {
                        SaveAs();
                        return false; // 另存为由宿主异步处理，此处返回 false 表示中断当前操作
                    }
                    return false;
                }
            }
            return result != MessageBoxResult.Cancel;
        }

        /// <summary>
        /// 关闭当前文档（需先 CheckSave）
        /// </summary>
        public void CloseDocument()
        {
            // 由宿主移除 Tab
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;
        private void RaisePropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        #endregion
    }

    #region 辅助类型

    /// <summary>
    /// 编译消息（与主窗口兼容）
    /// </summary>
    public class CompileMessage
    {
        public string Text { get; set; } = string.Empty;
        public Brush ColorBrush { get; set; } = Brushes.Black;
    }

    /// <summary>
    /// 编译完成事件参数
    /// </summary>
    public class CompilationEventArgs : EventArgs
    {
        public IReadOnlyList<CompileMessage> Messages { get; }
        public string StatusText { get; }
        public ParsedData? Data { get; }
        public JsonRoot? Root { get; }

        public CompilationEventArgs(IReadOnlyList<CompileMessage> messages, string statusText, ParsedData? data, JsonRoot? root)
        {
            Messages = messages;
            StatusText = statusText;
            Data = data;
            Root = root;
        }
    }

    #endregion
}