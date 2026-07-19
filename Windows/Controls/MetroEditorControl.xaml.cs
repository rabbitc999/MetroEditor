using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using MetroEditor.Windows.Controls.Style;
using System;
using System.ComponentModel;
using System.Formats.Tar;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace MetroEditor.Windows.Controls
{
    /// <summary>
    /// MetroEditorControl.xaml 的交互逻辑
    /// </summary>
    public partial class MetroEditorControl : UserControl
    {
        #region 依赖属性

        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(MetroEditorControl),
                new PropertyMetadata(string.Empty, OnFilePathChanged));

        public static readonly DependencyProperty IsDirtyProperty =
            DependencyProperty.Register(nameof(IsDirty), typeof(bool), typeof(MetroEditorControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(MetroEditorControl),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(MetroEditorControl),
                new PropertyMetadata(14.0, OnFontSizeChanged));

        public static readonly DependencyProperty ShowLineNumbersProperty =
            DependencyProperty.Register(nameof(ShowLineNumbers), typeof(bool), typeof(MetroEditorControl),
                new PropertyMetadata(true, OnShowLineNumbersChanged));

        public static readonly DependencyProperty WordWrapProperty =
            DependencyProperty.Register(nameof(WordWrap), typeof(bool), typeof(MetroEditorControl),
                new PropertyMetadata(false, OnWordWrapChanged));

        #endregion

        #region 属性

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

        public new double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public bool ShowLineNumbers
        {
            get => (bool)GetValue(ShowLineNumbersProperty);
            set => SetValue(ShowLineNumbersProperty, value);
        }

        public bool WordWrap
        {
            get => (bool)GetValue(WordWrapProperty);
            set => SetValue(WordWrapProperty, value);
        }

        // 暴露内部编辑器，便于外部高级操作（如查找替换）
        public TextEditor Editor => textEditor;

        #endregion

        #region 事件

        public event EventHandler TextChanged;
        public event EventHandler CaretPositionChanged;
        public event EventHandler IsDirtyChanged;

        #endregion

        #region 字段

        private FoldingManager _foldingManager;
        private bool _suppressTextSync; // 防止循环更新

        #endregion

        #region 构造函数与初始化

        public MetroEditorControl()
        {
            InitializeComponent();
            ConfigureEditor();
            Loaded += (s, e) => InitializeFolding();
        }

        private void ConfigureEditor()
        {
            // 注册自定义高亮（若尚未注册）
            if (HighlightingManager.Instance.GetDefinition("Metro") == null)
            {
                HighlightingManager.Instance.RegisterHighlighting("Metro", new[] { ".metro", ".m" }, new MetroHighlighting());
            }

            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Metro");
            textEditor.TextChanged += OnEditorTextChanged;
            textEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;

            // 双向同步 Text 属性与编辑器内容
            this.Loaded += (s, e) =>
            {
                if (!string.IsNullOrEmpty(Text) && textEditor.Text != Text)
                    textEditor.Text = Text;
            };
        }

        private void InitializeFolding()
        {
            if (_foldingManager == null)
            {
                _foldingManager = FoldingManager.Install(textEditor.TextArea);
                textEditor.TextArea.Document.TextChanged += (s, e) =>
                {
                    Dispatcher.BeginInvoke(UpdateFoldings, System.Windows.Threading.DispatcherPriority.Background);
                };
            }
        }

        private void UpdateFoldings()
        {
            if (_foldingManager == null) return;
            var foldings = MetroFoldingGenerator.CreateFolds(textEditor.Document);
            _foldingManager.UpdateFoldings(foldings, int.MaxValue);
        }

        #endregion

        #region 事件处理

        private void OnEditorTextChanged(object sender, EventArgs e)
        {
            if (!_suppressTextSync)
            {
                // 更新 Text 依赖属性
                SetValue(TextProperty, textEditor.Text);
                // 标记为脏
                if (!IsDirty)
                {
                    IsDirty = true;
                    IsDirtyChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            TextChanged?.Invoke(this, e);
        }

        private void OnCaretPositionChanged(object sender, EventArgs e)
        {
            CaretPositionChanged?.Invoke(this, e);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (MetroEditorControl)d;
            if (!ctrl._suppressTextSync)
            {
                string newText = e.NewValue as string ?? string.Empty;
                if (ctrl.textEditor.Text != newText)
                    ctrl.textEditor.Text = newText;
            }
        }

        private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 文件路径变化时，可进行标题更新等，由外部监听
        }

        private static void OnFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (MetroEditorControl)d;
            ctrl.textEditor.FontSize = (double)e.NewValue;
        }

        private static void OnShowLineNumbersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (MetroEditorControl)d;
            ctrl.textEditor.ShowLineNumbers = (bool)e.NewValue;
        }

        private static void OnWordWrapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (MetroEditorControl)d;
            ctrl.textEditor.WordWrap = (bool)e.NewValue;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 加载文件，自动检测编码（UTF8）
        /// </summary>
        public void LoadFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("文件不存在", path);

            _suppressTextSync = true;
            try
            {
                textEditor.Text = File.ReadAllText(path, new UTF8Encoding(false));
                FilePath = path;
                IsDirty = false;
                // 同步 Text 属性
                SetValue(TextProperty, textEditor.Text);
            }
            finally
            {
                _suppressTextSync = false;
            }
        }

        /// <summary>
        /// 保存当前内容到指定路径
        /// </summary>
        public void SaveFile(string path)
        {
            File.WriteAllText(path, textEditor.Text, Encoding.UTF8);
            FilePath = path;
            IsDirty = false;
        }

        /// <summary>
        /// 保存到当前 FilePath（若存在）
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(FilePath))
                throw new InvalidOperationException("未指定文件路径，请先设置 FilePath 或使用 SaveFile(string path)");
            SaveFile(FilePath);
        }

        /// <summary>
        /// 检查并保存（若脏），返回是否成功（取消时返回 false）
        /// </summary>
        public bool CheckSave()
        {
            if (!IsDirty) return true;
            var result = MessageBox.Show("当前文件已修改，是否保存？", "保存提示",
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
                    // 若保存失败，让用户选择另存为或取消
                    var saveAs = MessageBox.Show("保存失败，是否另存为？", "错误", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (saveAs == MessageBoxResult.Yes)
                    {
                        // 触发外部另存为事件
                        RequestSaveAs?.Invoke(this, EventArgs.Empty);
                        return false; // 由外部处理
                    }
                    return false;
                }
            }
            return result != MessageBoxResult.Cancel;
        }

        // 当需要另存为时触发，由宿主处理
        public event EventHandler RequestSaveAs;

        /// <summary>
        /// 获取光标位置信息
        /// </summary>
        public (int Line, int Column) GetCaretPosition()
        {
            var caret = textEditor.TextArea.Caret;
            return (caret.Line, caret.Column);
        }

        /// <summary>
        /// 滚动到指定行
        /// </summary>
        public void ScrollToLine(int line)
        {
            if (line >= 1 && line <= textEditor.Document.LineCount)
                textEditor.ScrollTo(line, 0);
        }

        /// <summary>
        /// 选中并定位到指定行首
        /// </summary>
        public void SelectLine(int line)
        {
            if (line >= 1 && line <= textEditor.Document.LineCount)
            {
                int offset = textEditor.Document.GetLineByNumber(line).Offset;
                textEditor.Select(offset, 0);
                ScrollToLine(line);
            }
        }

        #endregion

        #region 快捷键支持（Ctrl+M 切换折叠）

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var folds = _foldingManager?.GetFoldingsAt(textEditor.TextArea.Caret.Offset);
                if (folds != null && folds.Any())
                {
                    var section = folds.First();
                    section.IsFolded = !section.IsFolded;
                }
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion
    }
}
