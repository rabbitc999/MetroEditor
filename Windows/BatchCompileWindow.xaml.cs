using MetroEditor.MetroLang;
using MetroEditor.MetroLang.Compiler;
using MetroEditor.MetroLang.Exporter;
using MetroEditor.Simulator.Algorithms;
using MetroEditor.Simulator.Data;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 批量编译窗口：用于将多个Metro源文件（.metro）批量编译为二进制（.bmetro）或JSON（.json）格式
    /// </summary>
    public partial class BatchCompileWindow : Window
    {
        // 存储待编译文件列表的集合，绑定到DataGrid
        private readonly ObservableCollection<FileItem> _files = new ObservableCollection<FileItem>();

        /// <summary>
        /// 构造函数：初始化窗口，设置数据绑定和默认输出目录
        /// </summary>
        public BatchCompileWindow()
        {
            InitializeComponent();
            // 将文件列表绑定到DataGrid
            FileDataGrid.ItemsSource = _files;
            // 设置默认输出目录为用户文档文件夹
            OutputDirTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 添加文件按钮点击事件：打开文件选择对话框，允许用户选择多个.metro文件
        /// </summary>
        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Metro文件 (*.metro;*.m)|*.metro;*.m|所有文件 (*.*)|*.*",
                Multiselect = true,
                Title = "选择要编译的 Metro 文件"
            };
            if (dlg.ShowDialog() == true)
            {
                // 遍历选中的文件，避免重复添加
                foreach (var path in dlg.FileNames)
                {
                    if (!_files.Any(f => f.FullPath == path))
                    {
                        _files.Add(new FileItem
                        {
                            FileName = Path.GetFileName(path),
                            FullPath = path,
                            Status = "待编译"
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 移除选中文件按钮点击事件：从列表中移除当前选中的文件
        /// </summary>
        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = FileDataGrid.SelectedItems.Cast<FileItem>().ToList();
            foreach (var item in selected)
                _files.Remove(item);
        }

        /// <summary>
        /// 清空列表按钮点击事件：清空所有待编译文件
        /// </summary>
        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            _files.Clear();
        }

        /// <summary>
        /// 浏览输出目录按钮点击事件：打开文件夹选择对话框，设置输出目录
        /// </summary>
        private void BrowseOutputDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择输出目录",
                SelectedPath = OutputDirTextBox.Text
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OutputDirTextBox.Text = dlg.SelectedPath;
            }
        }

        /// <summary>
        /// 开始编译按钮点击事件：异步执行批量编译操作
        /// </summary>
        /// <summary>
        /// 开始编译按钮点击事件：异步执行批量编译操作
        /// </summary>
        private async void StartCompile_Click(object sender, RoutedEventArgs e)
        {
            // 验证输入
            if (_files.Count == 0)
            {
                MessageBox.Show("请先添加要编译的文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string outputDir = OutputDirTextBox.Text.Trim();
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                MessageBox.Show("请选择一个有效的输出目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 获取输出格式
            string format = (OutputFormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "bmetro";

            // 重置文件状态
            foreach (var item in _files)
            {
                item.Status = "待编译";
                item.Message = "";
            }
            ProgressBar.Value = 0;
            CompileButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;

            int total = _files.Count;
            int completed = 0;

            try
            {
                await Task.Run(() =>
                {
                    Parallel.ForEach(_files, (fileItem, state) =>
                    {
                        try
                        {
                            string content = File.ReadAllText(fileItem.FullPath, Encoding.UTF8);
                            string baseName = Path.GetFileNameWithoutExtension(fileItem.FullPath);

                            // -------- 编译 --------
                            var compiler = new MetroCompiler();
                            compiler.Parse(content, Path.GetDirectoryName(fileItem.FullPath));
                            var root = compiler.ExportJson();

                            string outputPath;

                            switch (format)
                            {
                                case "bmetro":
                                    // -------- 压缩 JSON (.bmetro) --------
                                    var jsonOptions = new JsonSerializerOptions
                                    {
                                        WriteIndented = false,
                                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                                    };
                                    string json = JsonSerializer.Serialize(root, jsonOptions);
                                    byte[] compressed = MainWindow.CompressJson(json);
                                    outputPath = Path.Combine(outputDir, baseName + ".bmetro");
                                    File.WriteAllBytes(outputPath, compressed);
                                    break;

                                case "json":
                                default:
                                    // -------- 明文 JSON (.json) --------
                                    var jsonOptions2 = new JsonSerializerOptions
                                    {
                                        WriteIndented = false,
                                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                                    };
                                    string jsonText = JsonSerializer.Serialize(root, jsonOptions2);
                                    outputPath = Path.Combine(outputDir, baseName + ".json");
                                    File.WriteAllText(outputPath, jsonText, Encoding.UTF8);
                                    break;
                            }

                            fileItem.Status = "成功";
                            fileItem.Message = $"已保存至 {Path.GetFileName(outputPath)}";
                        }
                        catch (Exception ex)
                        {
                            fileItem.Status = "失败";
                            fileItem.Message = ex.Message;
                        }
                        finally
                        {
                            Interlocked.Increment(ref completed);
                            Dispatcher.Invoke(() =>
                            {
                                ProgressBar.Value = (double)completed / total * 100;
                            });
                        }
                    });
                });
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    CompileButton.IsEnabled = true;
                    Mouse.OverrideCursor = null;
                });
            }

            MessageBox.Show($"批量编译完成。成功: {_files.Count(f => f.Status == "成功")}，失败: {_files.Count(f => f.Status == "失败")}",
                            "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 关闭按钮点击事件：关闭窗口
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 文件项类：用于在DataGrid中显示文件信息，并实现属性变更通知
        /// </summary>
        public class FileItem : INotifyPropertyChanged
        {
            private string _fileName;
            private string _fullPath;
            private string _status;
            private string _message;

            // 文件名（显示用）
            public string FileName { get => _fileName; set { _fileName = value; OnPropertyChanged(nameof(FileName)); } }
            // 完整路径（内部使用）
            public string FullPath { get => _fullPath; set { _fullPath = value; OnPropertyChanged(nameof(FullPath)); } }
            // 编译状态（待编译/成功/失败）
            public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
            // 附加消息（如错误信息）
            public string Message { get => _message; set { _message = value; OnPropertyChanged(nameof(Message)); } }

            // 属性变更事件，用于通知UI更新
            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}