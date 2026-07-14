using MetroEditor.MetroLang;
using MetroEditor.MetroLang.Decompiler;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.MetroLang.Tool;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 批量反编译窗口：用于将多个编译后的数据文件（.bmetro或.json）批量反编译为Metro源文件（.metro）
    /// </summary>
    public partial class BatchDecompileWindow : Window
    {
        // 存储待反编译文件列表的集合，绑定到DataGrid
        private readonly ObservableCollection<FileItem> _files = new ObservableCollection<FileItem>();

        /// <summary>
        /// 构造函数：初始化窗口，设置数据绑定和默认输出目录
        /// </summary>
        public BatchDecompileWindow()
        {
            InitializeComponent();
            FileDataGrid.ItemsSource = _files;
            OutputDirTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        /// <summary>
        /// 添加文件按钮点击事件：打开文件选择对话框，允许用户选择多个.bmetro或.json文件
        /// </summary>
        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "编译后数据 (*.bmetro)|*.bmetro|JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Multiselect = true,
                Title = "选择要反编译的数据文件"
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (var path in dlg.FileNames)
                {
                    if (!_files.Any(f => f.FullPath == path))
                    {
                        _files.Add(new FileItem
                        {
                            FileName = Path.GetFileName(path),
                            FullPath = path,
                            Status = "待反编译"
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
        /// 清空列表按钮点击事件：清空所有待反编译文件
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
        /// 开始反编译按钮点击事件：异步执行批量反编译操作
        /// </summary>
        private async void StartDecompile_Click(object sender, RoutedEventArgs e)
        {
            // 验证输入
            if (_files.Count == 0)
            {
                MessageBox.Show("请先添加要反编译的文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string outputDir = OutputDirTextBox.Text.Trim();
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                MessageBox.Show("请选择一个有效的输出目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 重置文件状态
            foreach (var item in _files)
            {
                item.Status = "待反编译";
                item.Message = "";
            }
            ProgressBar.Value = 0;
            DecompileButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;

            int total = _files.Count;
            int completed = 0;

            try
            {
                // 在后台线程中并行处理文件
                await Task.Run(() =>
                {
                    Parallel.ForEach(_files, (fileItem, state) =>
                    {
                        try
                        {
                            string baseName = Path.GetFileNameWithoutExtension(fileItem.FullPath);
                            string outputPath = Path.Combine(outputDir, baseName + ".metro");

                            // 读取数据文件内容
                            string jsonText;
                            string ext = Path.GetExtension(fileItem.FullPath).ToLowerInvariant();
                            if (ext == ".bmetro")
                            {
                                // 读取压缩的.bmetro文件，解压为JSON字符串
                                byte[] compressed = File.ReadAllBytes(fileItem.FullPath);
                                using var input = new MemoryStream(compressed);
                                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                                using var reader = new StreamReader(gzip, Encoding.UTF8);
                                jsonText = reader.ReadToEnd();
                            }
                            else if (ext == ".json")
                            {
                                // 直接读取JSON文件
                                jsonText = File.ReadAllText(fileItem.FullPath, Encoding.UTF8);
                            }
                            else
                            {
                                // 尝试按JSON读取（未知扩展名）
                                jsonText = File.ReadAllText(fileItem.FullPath, Encoding.UTF8);
                            }

                            // 反序列化JSON为MetroJsonRoot对象
                            var root = JsonSerializer.Deserialize<JsonRoot>(jsonText, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (root == null)
                                throw new Exception("无法解析数据文件");

                            // 使用MetroDecompiler将JSON反编译为Metro源文件
                            var decompiler = new MetroDecompiler(root);
                            string source = decompiler.Decompile();

                            // 写入.metro文件
                            File.WriteAllText(outputPath, source, Encoding.UTF8);

                            // 更新状态为成功
                            fileItem.Status = "成功";
                            fileItem.Message = $"已保存至 {Path.GetFileName(outputPath)}";
                        }
                        catch (Exception ex)
                        {
                            // 记录失败状态和异常信息
                            fileItem.Status = "失败";
                            fileItem.Message = ex.Message;
                        }
                        finally
                        {
                            // 原子操作更新完成计数，并刷新进度条
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
                // 恢复UI状态
                Dispatcher.Invoke(() =>
                {
                    DecompileButton.IsEnabled = true;
                    Mouse.OverrideCursor = null;
                });
            }

            // 显示完成消息
            MessageBox.Show($"批量反编译完成。成功: {_files.Count(f => f.Status == "成功")}，失败: {_files.Count(f => f.Status == "失败")}",
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
            // 反编译状态（待反编译/成功/失败）
            public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
            // 附加消息（如错误信息）
            public string Message { get => _message; set { _message = value; OnPropertyChanged(nameof(Message)); } }

            // 属性变更事件，用于通知UI更新
            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}