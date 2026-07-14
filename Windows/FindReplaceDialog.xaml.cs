using System.Text.RegularExpressions;
using System.Windows;
using ICSharpCode.AvalonEdit;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 查找替换对话框
    /// 功能：在 AvalonEdit 编辑器中执行查找和替换操作
    /// 核心特性：
    /// 1. 支持正则表达式查找
    /// 2. 支持大小写敏感和整词匹配
    /// 3. 循环查找（到达文末后从头开始）
    /// 4. 异步查找避免 UI 阻塞
    /// 5. 替换单个或全部匹配项
    /// </summary>
    /// <remarks>
    /// 设计要点：
    /// - 使用 Regex 缓存提高重复查找性能
    /// - 设置 RegexTimeout 防止恶意正则导致死锁
    /// - 从后往前替换避免索引偏移问题
    /// </remarks>
    public partial class FindReplaceDialog : Window
    {
        private readonly TextEditor _editor;                          // AvalonEdit 编辑器实例
        private readonly Dictionary<string, Regex> _regexCache = new(); // 正则表达式缓存
        private struct SearchState                                      // 查找状态（用于循环查找）
        {
            public string Text { get; set; }          // 上次查找的文本
            public int LastMatchOffset { get; set; }  // 上次匹配的起始位置
        }
        private SearchState _searchState;

        // 正则表达式超时设置（防止复杂正则卡死UI）
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="editor">AvalonEdit 编辑器实例</param>
        public FindReplaceDialog(TextEditor editor)
        {
            InitializeComponent();
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
            FindTextBox.Focus();

            // 初始化查找状态
            _searchState = new SearchState { Text = "", LastMatchOffset = -1 };

            // 智能填充：如果有选中文本，填入查找框；否则尝试从剪贴板获取
            if (_editor.SelectionLength > 0)
            {
                FindTextBox.Text = _editor.SelectedText;
                FindTextBox.SelectAll();
                _searchState.Text = FindTextBox.Text;
            }
            else if (Clipboard.ContainsText())
            {
                ReplaceTextBox.Text = Clipboard.GetText();
            }
        }

        #region 核心查找逻辑
        /// <summary>
        /// 异步执行查找操作（避免 UI 阻塞）
        /// </summary>
        /// <param name="replaceMode">是否为替换模式（替换模式会额外处理选中状态）</param>
        private async void PerformFindAsync(bool replaceMode)
        {
            string findText = FindTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(findText))
            {
                StatusTextBlock.Text = "请输入查找内容";
                return;
            }

            // 获取或创建正则表达式（带缓存）
            Regex regex = GetOrCreateRegex(findText);
            if (regex == null) return;

            string documentText = _editor.Document.Text;
            int startOffset = _editor.CaretOffset;

            // 状态管理：如果查找文本变化，重置搜索起点；否则从上次匹配末尾继续
            if (findText != _searchState.Text)
            {
                _searchState.Text = findText;
                _searchState.LastMatchOffset = startOffset;
            }
            else
            {
                // 从上次匹配的末尾+1开始，避免重复匹配同一位置
                if (_searchState.LastMatchOffset >= 0)
                {
                    startOffset = _searchState.LastMatchOffset + _searchState.Text.Length;
                    if (startOffset > documentText.Length) startOffset = 0;
                }
            }

            // 在后台线程执行正则匹配
            Match? match = null;
            try
            {
                match = await Task.Run(() =>
                {
                    var m = regex.Match(documentText, startOffset);
                    // 如果未找到且不是从开头开始，则从头重试（循环查找）
                    if (!m.Success && startOffset != 0)
                        m = regex.Match(documentText, 0);
                    return m;
                });
            }
            catch (RegexMatchTimeoutException)
            {
                StatusTextBlock.Text = "查找超时，请简化搜索条件";
                return;
            }

            if (!match.Success)
            {
                StatusTextBlock.Text = "未找到";
                _searchState.Text = "";
                _searchState.LastMatchOffset = -1;
                return;
            }

            // 更新 UI（必须在 UI 线程）
            _editor.Select(match.Index, match.Length);
            var line = _editor.Document.GetLineByOffset(match.Index);
            _editor.ScrollTo(line.LineNumber, match.Index - line.Offset + 1);
            _searchState.LastMatchOffset = match.Index;
            StatusTextBlock.Text = $"找到 '{findText}' (位置 {match.Index})";

            if (!replaceMode)
                _editor.Focus();
        }

        /// <summary>
        /// 获取或创建正则表达式（带缓存机制）
        /// </summary>
        /// <param name="findText">查找文本</param>
        /// <returns>配置好的 Regex 对象</returns>
        private Regex GetOrCreateRegex(string findText)
        {
            bool caseSensitive = CaseSensitiveCheckBox.IsChecked.GetValueOrDefault();
            bool wholeWord = WholeWordCheckBox.IsChecked.GetValueOrDefault();

            // 构建正则模式：整词匹配需要添加 \b 边界
            string pattern = wholeWord
                ? $@"\b{Regex.Escape(findText)}\b"
                : Regex.Escape(findText);

            // 缓存键：模式 + 大小写敏感标志
            string cacheKey = $"{pattern}_{caseSensitive}";
            if (_regexCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var options = RegexOptions.None;
            if (!caseSensitive)
                options |= RegexOptions.IgnoreCase;

            var regex = new Regex(pattern, options, RegexTimeout);
            _regexCache[cacheKey] = regex;
            return regex;
        }
        #endregion

        #region 事件处理
        /// <summary>
        /// 查找下一个按钮点击事件
        /// </summary>
        private void FindNext_Click(object sender, RoutedEventArgs e)
        {
            PerformFindAsync(replaceMode: false);
        }

        /// <summary>
        /// 替换按钮点击事件
        /// </summary>
        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            // 先确认当前选中内容是否匹配查找文本
            if (_editor.SelectionLength == 0 || _editor.SelectedText != FindTextBox.Text)
            {
                if (!PerformFindSync(replaceMode: false))
                    return;
            }

            // 执行替换
            string replaceText = ReplaceTextBox.Text ?? "";
            int start = _editor.SelectionStart;
            int length = _editor.SelectionLength;
            _editor.Document.Replace(start, length, replaceText);

            // 更新光标位置和查找状态
            int newOffset = start + replaceText.Length;
            _editor.CaretOffset = newOffset;
            _searchState.LastMatchOffset = newOffset;

            // 继续查找下一个
            PerformFindAsync(replaceMode: false);
        }

        /// <summary>
        /// 同步查找（用于替换前的确认，避免异步复杂性）
        /// </summary>
        private bool PerformFindSync(bool replaceMode)
        {
            // 逻辑与 PerformFindAsync 类似，但直接在 UI 线程执行
            string findText = FindTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(findText))
            {
                StatusTextBlock.Text = "请输入查找内容";
                return false;
            }

            Regex regex = GetOrCreateRegex(findText);
            if (regex == null) return false;

            string documentText = _editor.Document.Text;
            int startOffset = _editor.CaretOffset;

            if (findText != _searchState.Text)
            {
                _searchState.Text = findText;
                _searchState.LastMatchOffset = startOffset;
            }
            else
            {
                if (_searchState.LastMatchOffset >= 0)
                {
                    startOffset = _searchState.LastMatchOffset + _searchState.Text.Length;
                    if (startOffset > documentText.Length) startOffset = 0;
                }
            }

            Match match;
            try
            {
                match = regex.Match(documentText, startOffset);
                if (!match.Success && startOffset != 0)
                    match = regex.Match(documentText, 0);
            }
            catch (RegexMatchTimeoutException)
            {
                StatusTextBlock.Text = "查找超时";
                return false;
            }

            if (!match.Success)
            {
                StatusTextBlock.Text = "未找到";
                _searchState.Text = "";
                _searchState.LastMatchOffset = -1;
                return false;
            }

            _editor.Select(match.Index, match.Length);
            var line = _editor.Document.GetLineByOffset(match.Index);
            _editor.ScrollTo(line.LineNumber, match.Index - line.Offset + 1);
            _searchState.LastMatchOffset = match.Index;
            StatusTextBlock.Text = $"找到 '{findText}' (位置 {match.Index})";

            if (!replaceMode)
                _editor.Focus();
            return true;
        }

        /// <summary>
        /// 替换全部按钮点击事件
        /// </summary>
        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            string findText = FindTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(findText))
            {
                StatusTextBlock.Text = "请输入查找内容";
                return;
            }

            Regex regex = GetOrCreateRegex(findText);
            if (regex == null) return;

            string documentText = _editor.Document.Text;
            MatchCollection matches;
            try
            {
                matches = regex.Matches(documentText);
            }
            catch (RegexMatchTimeoutException)
            {
                StatusTextBlock.Text = "替换超时";
                return;
            }

            if (matches.Count == 0)
            {
                StatusTextBlock.Text = "未找到可替换的内容";
                return;
            }

            string replaceText = ReplaceTextBox.Text ?? "";

            // 关键：必须从后往前替换，避免替换后索引偏移
            int replaced = 0;
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var m = matches[i];
                _editor.Document.Replace(m.Index, m.Length, replaceText);
                replaced++;
            }

            StatusTextBlock.Text = $"已替换 {replaced} 处";
            _searchState.Text = "";
            _searchState.LastMatchOffset = -1;
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>
        /// 查找框回车键事件（等同于点击查找下一个）
        /// </summary>
        private void FindTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                FindNext_Click(sender, e);
        }
        #endregion
    }
}