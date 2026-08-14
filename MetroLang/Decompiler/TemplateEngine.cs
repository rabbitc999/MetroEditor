using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MetroEditor.MetroLang.Decompiler
{
    /// <summary>
    /// 反编译器模板引擎：从 .template 文件加载代码模板并渲染。
    /// </summary>
    /// <remarks>
    /// 模板语法：
    ///  1. 占位符：<c>{{Key}}</c>，渲染时替换为变量字典中对应的字符串值。
    ///  2. 空值语义：若某一行中<b>所有</b>占位符均解析为 null 或空字符串，则整行被删除；
    ///     部分为空时仅将空值替换为空串保留该行（用于后缀式占位符，如 {{Suffix}}）。
    ///  3. 变量字典中不存在的 Key 视为模板错误，抛出 <see cref="TemplateException"/>，
    ///     以保证模板与反编译器代码的一致性。
    /// 模板文件默认位于程序输出目录的 Templates 子目录（由 csproj 自动拷贝），
    /// 也可通过构造函数指定自定义目录（供模板编辑器等场景使用）。
    /// </remarks>
    public class TemplateEngine
    {
        /// <summary>占位符匹配：{{Key}}，Key 为字母/数字/下划线。</summary>
        private static readonly Regex PlaceholderPattern = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

        private readonly string _templateDirectory;
        private readonly Dictionary<string, string> _cache = new();

        /// <summary>
        /// 初始化模板引擎。
        /// </summary>
        /// <param name="templateDirectory">模板目录；为 null 时使用默认目录。</param>
        public TemplateEngine(string? templateDirectory = null)
        {
            _templateDirectory = templateDirectory ?? GetDefaultTemplateDirectory();
        }

        /// <summary>当前模板目录。</summary>
        public string TemplateDirectory => _templateDirectory;

        /// <summary>默认模板目录：程序集输出目录下的 Templates 文件夹。</summary>
        public static string GetDefaultTemplateDirectory()
        {
            string baseDir = AppContext.BaseDirectory;
            string dir = Path.Combine(baseDir, "Templates");

            // 开发环境（dotnet run / 单文件发布）回退：向上查找 MetroLang/Decompiler/Templates
            if (!Directory.Exists(dir))
            {
                string? probe = baseDir;
                for (int i = 0; i < 6 && probe != null; i++)
                {
                    string candidate = Path.Combine(probe, "MetroLang", "Decompiler", "Templates");
                    if (Directory.Exists(candidate))
                        return candidate;
                    probe = Path.GetDirectoryName(probe);
                }
            }
            return dir;
        }

        /// <summary>列出模板目录下所有模板名（不含扩展名，按名称排序）。</summary>
        public IReadOnlyList<string> GetTemplateNames()
        {
            if (!Directory.Exists(_templateDirectory))
                return Array.Empty<string>();
            return Directory.GetFiles(_templateDirectory, "*.template")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>读取模板原始文本（带缓存）。</summary>
        public string LoadRaw(string templateName)
        {
            if (_cache.TryGetValue(templateName, out var cached))
                return cached;

            string path = Path.Combine(_templateDirectory, templateName + ".template");
            if (!File.Exists(path))
                throw new TemplateException($"模板文件不存在: {path}");

            string content = File.ReadAllText(path, Encoding.UTF8);
            _cache[templateName] = content;
            return content;
        }

        /// <summary>清除模板缓存（模板文件被修改后调用）。</summary>
        public void ClearCache() => _cache.Clear();

        /// <summary>
        /// 渲染单个模板。
        /// </summary>
        /// <param name="templateName">模板名（不含 .template 扩展名）。</param>
        /// <param name="variables">变量字典；值允许为 null/空（触发整行删除）。</param>
        /// <returns>渲染结果；若模板所有行均被删除则返回空字符串。</returns>
        public string Render(string templateName, IReadOnlyDictionary<string, string?> variables)
        {
            string raw = LoadRaw(templateName);
            var outputLines = new List<string>();

            foreach (var line in raw.Split('\n'))
            {
                string trimmed = line.TrimEnd('\r');
                var matches = PlaceholderPattern.Matches(trimmed);

                // 无占位符的行原样保留（含空行）
                if (matches.Count == 0)
                {
                    outputLines.Add(trimmed);
                    continue;
                }

                // 校验变量并解析
                int emptyCount = 0;
                string resolved = PlaceholderPattern.Replace(trimmed, m =>
                {
                    string key = m.Groups[1].Value;
                    if (!variables.ContainsKey(key))
                        throw new TemplateException($"模板 {templateName} 使用了未提供的变量 {{{{{key}}}}}");
                    string? value = variables[key];
                    if (string.IsNullOrEmpty(value))
                    {
                        emptyCount++;
                        return string.Empty;
                    }
                    return value;
                });

                if (emptyCount == matches.Count) continue; // 空值语义：全部占位符为空则删除整行
                outputLines.Add(resolved);
            }

            // 去除首尾空行，保留中间结构
            return string.Join("\n", outputLines).Trim('\n');
        }

        /// <summary>
        /// 渲染一个模板的多个实例并拼接（用于列表项，如站点、PERIOD 等）。
        /// </summary>
        /// <param name="templateName">项模板名。</param>
        /// <param name="items">每一项的变量字典。</param>
        /// <param name="separator">项之间的分隔符，默认换行。</param>
        public string RenderMany(string templateName, IEnumerable<IReadOnlyDictionary<string, string?>> items, string separator = "\n")
        {
            return string.Join(separator, items.Select(v => Render(templateName, v)));
        }
    }

    /// <summary>模板渲染异常。</summary>
    public class TemplateException : Exception
    {
        public TemplateException(string message) : base(message) { }
    }
}
