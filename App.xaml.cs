using MetroEditor.MetroLang.Compiler;
using MetroEditor.MetroLang.Decompiler;
using MetroEditor.MetroLang.JsonModel;
using System.Configuration;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Xml;

namespace MetroEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // 导入附加控制台的 Win32 API
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        protected override void OnStartup(StartupEventArgs e)
        {
            var args = e.Args;

            // 1. 检查是否是无头命令（编译/反编译）
            if (args.Length > 0)
            {
                string firstArg = args[0].ToLowerInvariant();
                if (firstArg == "--compile" || firstArg == "--decompile")
                {
                    AttachConsole(ATTACH_PARENT_PROCESS);
                    if (firstArg == "--compile") { HandleCompile(args); }
                    else { HandleDecompile(args); }
                    Shutdown();
                    return;
                }
            }

            // 2. 正常 GUI 启动流程
            base.OnStartup(e);
        }

        private void HandleDecompile(string[] args)
        {
            try
            {
                string inputFile = null;
                string outputFile = null;

                // 解析参数（与编译保持一致）
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i] == "--input" && i + 1 < args.Length)
                        inputFile = args[++i];
                    else if (args[i] == "--output" && i + 1 < args.Length)
                        outputFile = args[++i];
                }

                if (string.IsNullOrEmpty(inputFile))
                    throw new ArgumentException("未指定输入文件。用法: --decompile --input <文件路径>");

                // 检查输入文件是否存在
                if (!File.Exists(inputFile))
                    throw new FileNotFoundException($"找不到文件: {inputFile}");

                string json = null;
                JsonRoot jsonRoot = null;

                // 将 .bmetro 或 .json 替换为 .metro
                string ext = Path.GetExtension(inputFile).ToLower();

                // 自动生成输出路径（如果未指定）
                if (string.IsNullOrEmpty(outputFile))
                {
                    if (ext == ".bmetro" || ext == ".json")
                        outputFile = Path.ChangeExtension(inputFile, ".metro");
                    else
                        outputFile = inputFile + ".metro"; // 兜底
                }


                if (ext == ".bmetro")
                {
                    byte[] bytes = File.ReadAllBytes(inputFile);
                    json = MetroEditor.Windows.MainWindow.DecompressJson(bytes); // GZip 解压缩
                }
                else
                {
                    json = File.ReadAllText(inputFile);
                }

                jsonRoot = JsonSerializer.Deserialize<JsonRoot>(json);

                // 调用实现的反编译逻辑
                MetroDecompiler metroDecompiler = new MetroDecompiler(jsonRoot);
                string text = metroDecompiler.Decompile();
                File.WriteAllText(outputFile, text);

                Console.WriteLine($"反编译成功！输出文件：{outputFile}");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"反编译失败：{ex.Message}");
                Environment.Exit(1);
            }
        }

        private void HandleCompile(string[] args)
        {
            try
            {
                // 2. 解析参数（简单示例，实际可加入 -o, --format 等）
                string inputFile = null;
                string outputFile = null;
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i] == "--input" && i + 1 < args.Length) inputFile = args[++i];
                    else if (args[i] == "--output" && i + 1 < args.Length) outputFile = args[++i];
                }

                if (string.IsNullOrEmpty(inputFile))
                    throw new ArgumentException("未指定输入文件。用法: --compile --input <文件路径>");

                // 3. 读取源码并编译
                string sourceCode = File.ReadAllText(inputFile);

                MetroCompiler metorCompiler = new MetroCompiler();
                metorCompiler.Parse(sourceCode, Path.GetDirectoryName(inputFile));

                // 4. 输出文件（支持 .bmetro 或 .json）
                if (string.IsNullOrEmpty(outputFile))
                    outputFile = Path.ChangeExtension(inputFile, ".bmetro");

                var jsonRoot = metorCompiler.ExportJson(); // 转标准 JSON

                // 序列化为 JSON（紧凑，保留中文字符）
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = JsonSerializer.Serialize(jsonRoot, jsonOptions);

                if (outputFile.EndsWith(".bmetro"))
                {
                    var binary = MetroEditor.Windows.MainWindow.CompressJson(json); // GZip 压缩
                    File.WriteAllBytes(outputFile, binary);
                }
                else
                {
                    File.WriteAllText(outputFile, json);
                }

                Console.WriteLine($"编译成功！输出文件：{outputFile}");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"编译失败：{ex.Message}");
                Environment.Exit(1);
            }
            finally
            {
                // 强制终止 UI 线程
                Shutdown();
            }

        }
    }
}
