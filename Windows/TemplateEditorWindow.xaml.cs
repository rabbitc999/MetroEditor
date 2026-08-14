using MetroEditor.MetroLang.Decompiler;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 反编译模板编辑器：列出并编辑反编译器使用的 .template 模板文件。
    /// 保存后下次反编译立即采用新模板，无需重新编译程序。
    /// </summary>
    public partial class TemplateEditorWindow : Window
    {
        private readonly TemplateEngine _engine;
        private string? _currentTemplate;
        private bool _dirty;
        private bool _suppressTextChanged;

        public TemplateEditorWindow()
        {
            InitializeComponent();
            _engine = new TemplateEngine();
            DirText.Text = "目录：" + _engine.TemplateDirectory;
            RefreshList();

            editor.TextChanged += (s, e) =>
            {
                if (_suppressTextChanged) return;
                if (!_dirty)
                {
                    _dirty = true;
                    DirtyFlag.Text = "已修改*";
                }
            };

            Loaded += (o, e) => Topmost = false;
        }

        private void RefreshList()
        {
            var names = _engine.GetTemplateNames();
            TemplateList.ItemsSource = names;
            if (names.Count > 0 && TemplateList.SelectedIndex < 0)
                TemplateList.SelectedIndex = 0;
        }

        private void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TemplateList.SelectedItem is not string name) return;

            if (_dirty && !ConfirmDiscard()) return;

            LoadTemplate(name);
        }

        private void LoadTemplate(string name)
        {
            _suppressTextChanged = true;
            try
            {
                _engine.ClearCache();
                editor.Text = _engine.LoadRaw(name);
                _currentTemplate = name;
                CurrentTemplateText.Text = name + ".template";
                _dirty = false;
                DirtyFlag.Text = "未修改";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载模板失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _suppressTextChanged = false;
            }
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTemplate)) return;
            if (_dirty && !ConfirmDiscard()) return;
            LoadTemplate(_currentTemplate);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTemplate)) return;

            try
            {
                string path = Path.Combine(_engine.TemplateDirectory, _currentTemplate + ".template");
                File.WriteAllText(path, editor.Text);
                _engine.ClearCache();
                _dirty = false;
                DirtyFlag.Text = "已保存";
                MessageBox.Show("模板已保存，下次反编译将使用新内容。", "保存成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private bool ConfirmDiscard()
        {
            var result = MessageBox.Show("当前模板有未保存的修改，是否放弃修改？", "确认",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                // 阻止切换：恢复选中项不切回。这里不主动改回，保留当前编辑内容。
                return false;
            }
            return true;
        }
    }
}
