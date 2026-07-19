using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MetroEditor.Windows.Controls
{
    public partial class DocumentWorkspace : UserControl
    {
        private readonly ObservableCollection<MetroEditorTab> _tabs = new();
        private MetroEditorTab? _currentTab;

        private void UpdateHasTabs()
        {
            bool has = _tabs.Count > 0;
            documentTabControl.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            placeholderBorder.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
            gridSplitter.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        }

        public DocumentWorkspace()
        {
            InitializeComponent();
            documentTabControl.ItemsSource = _tabs;
        }

        // 公开的集合（只读）
        public IList<MetroEditorTab> Tabs => _tabs;

        // 当前标签页
        public MetroEditorTab? CurrentTab => _currentTab;

        // 事件：标签页切换
        public event EventHandler<MetroEditorTab?>? CurrentTabChanged;

        // 事件：请求关闭标签页（由外部决定是否保存）
        public event EventHandler<TabClosingEventArgs> TabClosing;

        // 添加新标签页
        public void AddTab(MetroEditorTab tab)
        {
            _tabs.Add(tab);
            documentTabControl.SelectedItem = tab;
            UpdateHasTabs();
        }

        // 移除标签页（外部调用，确认保存后）
        public void RemoveTab(MetroEditorTab tab)
        {
            if (_tabs.Remove(tab))
            {
                if (_currentTab == tab) _currentTab = null;
            }
            UpdateHasTabs();
        }

        // 更新错误列表
        public void UpdateErrors(IEnumerable<CompileMessage> messages)
        {
            errorListBox.ItemsSource = messages;
        }

        // 选择行（外部调用，用于错误双击定位）
        public void SelectLineInCurrentEditor(int line)
        {
            _currentTab?.Editor.SelectLine(line);
        }

        private void DocumentTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentTab = documentTabControl.SelectedItem as MetroEditorTab;
            CurrentTabChanged?.Invoke(this, _currentTab);
        }

        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var tab = button?.Tag as MetroEditorTab;
            if (tab == null) return;

            // 触发外部事件，让宿主（如 MainWindow）决定是否允许关闭（例如保存确认）
            var args = new TabClosingEventArgs(tab);
            TabClosing?.Invoke(this, args);
            if (args.Cancel) return;  // 外部通过设置 e.Cancel = true 来取消关闭

            // 外部未取消，则内部移除标签页
            RemoveTab(tab);
        }

        private void ErrorListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (errorListBox.SelectedItem is CompileMessage msg)
            {
                var match = Regex.Match(msg.Text, @"第(\d+)行");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int line))
                {
                    SelectLineInCurrentEditor(line);
                }
            }
        }
    }

    public class TabClosingEventArgs : EventArgs
    {
        public MetroEditorTab Tab { get; }
        public bool Cancel { get; set; }
        public TabClosingEventArgs(MetroEditorTab tab) => Tab = tab;
    }
}
