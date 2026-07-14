using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MetroEditor.Controls.Style
{
    /// <summary>
    /// Metro 语言专用代码折叠生成器
    /// 为 AvalonEdit 编辑器提供智能折叠区域识别功能
    /// 支持三种折叠类型：多行注释、代码块（DEFINE/IF）、自定义REGION区域
    /// </summary>
    public static class MetroFoldingGenerator
    {
        // ===== 代码块起始关键字集合 =====
        // 包含所有支持块结构的 MetroLang 命令
        private static readonly HashSet<string> BlockStartKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PROGRAM_DEFINE",      // 程序定义块
            "LINE_DEFINE",         // 线路定义块
            "DIRECTION_DEFINE",    // 方向定义块
            "STATION_DEFINE",      // 车站定义块
            "PRICE_DEFINE",        // 票价定义块
            "TRANSFER_DEFINE",     // 换乘定义块
            "TRANSFER_TYPE",       // 换乘类型定义块
            "VIRTUAL_TRANSFER_DEFINE", // 虚拟换乘定义块
            "OPERATION_DEFINE",    // 运营参数定义块
            "HEADWAY_PROFILE",     // 发车间隔配置块
            "CROWD_PROFILE",       // 拥挤度配置块
            "DWELL_TIME_DEFINE",   // 停站时间定义块
            "SPEED_LIMIT_DEFINE",  // 限速定义块
            "LABEL_DEFINE",        // 标注定义块
            "SPECIAL_DEFINE",      // 特殊线路定义块
            "FACILITY_DEFINE",     // 设施定义块
            "PROMOTION_DEFINE",    // 优惠策略定义块
            "IF",                  // 条件编译块
        };

        /// <summary>
        /// 获取文档中所有多行注释的范围
        /// 扫描 /* ... */ 格式的注释块
        /// </summary>
        /// <param name="document">文本文档对象</param>
        /// <returns>多行注释的起始和结束位置列表</returns>
        private static List<(int Start, int End)> GetMultiLineCommentRanges(TextDocument document)
        {
            var ranges = new List<(int, int)>();
            var text = document.Text;
            int index = 0;

            // 遍历整个文档文本
            while (index < text.Length)
            {
                // 检测多行注释起始标记 /*
                if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '*')
                {
                    int startPosition = index;
                    // 查找多行注释结束标记 */
                    int endPosition = text.IndexOf("*/", index + 2);
                    if (endPosition == -1)
                        endPosition = text.Length;  // 未找到结束标记，扩展到文档末尾
                    else
                        endPosition += 2;  // 包含结束标记本身

                    ranges.Add((startPosition, endPosition));
                    index = endPosition;  // 跳过已处理的注释块
                }
                else
                {
                    index++;
                }
            }
            return ranges;
        }

        /// <summary>
        /// 生成完整的折叠区域列表
        /// 整合多行注释折叠、代码块折叠和REGION折叠
        /// </summary>
        /// <param name="document">文本文档对象</param>
        /// <returns>按起始位置排序的折叠区域集合</returns>
        public static IEnumerable<NewFolding> CreateFolds(TextDocument document)
        {
            var foldings = new List<NewFolding>();
            if (document == null || document.TextLength == 0)
                return foldings;

            // ===== 第一阶段：处理多行注释折叠 =====
            var commentRanges = GetMultiLineCommentRanges(document);
            foreach (var (start, end) in commentRanges)
            {
                // 仅对长度超过20字符的注释进行折叠，避免小注释干扰编辑体验
                if (end - start > 20)
                {
                    var folding = new NewFolding(start, end)
                    {
                        Name = "/* 注释 */",      // 折叠后显示的标题
                        DefaultClosed = false     // 默认展开状态
                    };
                    foldings.Add(folding);
                }
            }

            // ===== 第二阶段：处理代码块和REGION折叠 =====
            var lines = document.Lines;
            var blockStack = new Stack<BlockInfo>();      // 代码块堆栈（处理DEFINE/IF）
            var regionStack = new Stack<RegionInfo>();   // REGION堆栈（处理自定义区域）

            // 逐行扫描文档
            for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                string lineText = document.GetText(line).Trim();

                // 跳过空行
                if (string.IsNullOrEmpty(lineText))
                    continue;

                // ----- 处理 #REGION 开始标记 -----
                if (IsRegionStart(lineText, out string regionComment))
                {
                    regionStack.Push(new RegionInfo
                    {
                        StartLine = lineIndex,
                        StartOffset = line.Offset,
                        Comment = regionComment ?? ""
                    });
                    continue;  // REGION行本身不作为普通代码块处理
                }

                // ----- 处理 #END_REGION 结束标记 -----
                if (IsRegionEnd(lineText))
                {
                    if (regionStack.Count > 0)
                    {
                        var regionInfo = regionStack.Pop();
                        // 仅当REGION跨越多行时才创建折叠区域
                        if (regionInfo.StartLine < lineIndex)
                        {
                            var folding = new NewFolding(regionInfo.StartOffset, line.EndOffset)
                            {
                                Name = $"# REGION {regionInfo.Comment}".Trim(),
                                DefaultClosed = false
                            };
                            foldings.Add(folding);
                        }
                    }
                    continue;  // END_REGION行本身不作为普通代码块处理
                }

                // ----- 跳过普通注释行（不以#开头的注释）-----
                if (lineText.StartsWith("#"))
                    continue;

                // ----- 检测代码块起始标记 -----
                if (IsBlockStart(lineText, out string keyword))
                {
                    // 规则1：跳过同一行内包含END_DEFINE/END_IF的单行块
                    if (lineText.IndexOf("END_DEFINE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        lineText.IndexOf("END_IF", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    // 规则2：特殊处理DIRECTION_DEFINE的REV简写（单行结构）
                    if (keyword.Equals("DIRECTION_DEFINE", StringComparison.OrdinalIgnoreCase) &&
                        lineText.IndexOf("REV", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    // 将代码块起始信息压入堆栈
                    blockStack.Push(new BlockInfo
                    {
                        StartLine = lineIndex,
                        Keyword = keyword,
                        StartOffset = line.Offset,
                    });
                }

                // ----- 检测代码块结束标记 -----
                if (IsBlockEnd(lineText, out string endKeyword))
                {
                    if (blockStack.Count == 0) continue;

                    var blockInfo = blockStack.Pop();

                    // 验证块起始和结束标记的匹配性
                    bool isMatch = blockInfo.Keyword.Equals("IF", StringComparison.OrdinalIgnoreCase) &&
                                   endKeyword.Equals("END_IF", StringComparison.OrdinalIgnoreCase) ||
                                  blockInfo.Keyword != "IF" &&
                                   endKeyword.Equals("END_DEFINE", StringComparison.OrdinalIgnoreCase);

                    // 仅当块跨越多行时才创建折叠区域
                    if (isMatch && blockInfo.StartLine < lineIndex - 1)
                    {
                        var folding = new NewFolding(blockInfo.StartOffset, line.EndOffset)
                        {
                            Name = $"▼ {blockInfo.Keyword}",  // 折叠后显示的标题
                            DefaultClosed = false
                        };
                        foldings.Add(folding);
                    }
                }
            }

            // 按起始位置排序，确保折叠区域正确嵌套
            return foldings.OrderBy(f => f.StartOffset);
        }

        /// <summary>
        /// 检测是否为代码块起始行
        /// </summary>
        /// <param name="lineText">行文本内容</param>
        /// <param name="keyword">输出的关键字</param>
        /// <returns>是否为代码块起始行</returns>
        private static bool IsBlockStart(string lineText, out string keyword)
        {
            keyword = null;
            var upperLine = lineText.ToUpperInvariant();

            // 遍历所有预定义的块起始关键字
            foreach (var blockKeyword in BlockStartKeywords)
            {
                // 检查行是否以关键字开头（后跟空格或整行就是关键字）
                if (upperLine.StartsWith(blockKeyword + " ", StringComparison.OrdinalIgnoreCase) ||
                    upperLine.Equals(blockKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    keyword = blockKeyword;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检测是否为代码块结束行
        /// </summary>
        /// <param name="lineText">行文本内容</param>
        /// <param name="keyword">输出的结束关键字</param>
        /// <returns>是否为代码块结束行</returns>
        private static bool IsBlockEnd(string lineText, out string keyword)
        {
            keyword = null;
            var upperLine = lineText.ToUpperInvariant();

            // 检测 END_DEFINE 结束标记
            if (upperLine.StartsWith("END_DEFINE", StringComparison.OrdinalIgnoreCase) ||
                upperLine.Equals("END_DEFINE", StringComparison.OrdinalIgnoreCase))
            {
                keyword = "END_DEFINE";
                return true;
            }

            // 检测 END_IF 结束标记
            if (upperLine.StartsWith("END_IF", StringComparison.OrdinalIgnoreCase) ||
                upperLine.Equals("END_IF", StringComparison.OrdinalIgnoreCase))
            {
                keyword = "END_IF";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检测是否为 #REGION 开始标记
        /// </summary>
        /// <param name="lineText">行文本内容</param>
        /// <param name="comment">输出的REGION注释内容</param>
        /// <returns>是否为REGION开始标记</returns>
        private static bool IsRegionStart(string lineText, out string comment)
        {
            comment = null;
            var trimmedLine = lineText.TrimStart();

            // 检查是否以 #REGION 或 # REGION 开头（不区分大小写）
            if (trimmedLine.StartsWith("#REGION", StringComparison.OrdinalIgnoreCase) ||
                trimmedLine.StartsWith("# REGION", StringComparison.OrdinalIgnoreCase) ||
                trimmedLine.StartsWith("//REGION", StringComparison.OrdinalIgnoreCase) ||
                trimmedLine.StartsWith("// REGION", StringComparison.OrdinalIgnoreCase))
            {
                // 提取REGION后的注释内容
                int regionIndex = trimmedLine.IndexOf("REGION", StringComparison.OrdinalIgnoreCase);
                if (regionIndex >= 0)
                {
                    int commentStart = regionIndex + "REGION".Length;
                    if (commentStart < trimmedLine.Length)
                    {
                        comment = trimmedLine.Substring(commentStart).Trim();
                    }
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检测是否为 #END_REGION 结束标记
        /// </summary>
        /// <param name="lineText">行文本内容</param>
        /// <returns>是否为END_REGION结束标记</returns>
        private static bool IsRegionEnd(string lineText)
        {
            var trimmedLine = lineText.TrimStart();

            // 检查是否以 #END_REGION 或 # END_REGION 开头（不区分大小写）
            return trimmedLine.StartsWith("#END_REGION", StringComparison.OrdinalIgnoreCase) ||
                   trimmedLine.StartsWith("# END_REGION", StringComparison.OrdinalIgnoreCase) ||
                   trimmedLine.StartsWith("//END_REGION", StringComparison.OrdinalIgnoreCase) ||
                   trimmedLine.StartsWith("// END_REGION", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 代码块信息类：存储代码块的起始位置和关键字
        /// </summary>
        private class BlockInfo
        {
            public int StartLine { get; set; }      // 起始行号
            public string Keyword { get; set; }    // 块关键字（如LINE_DEFINE）
            public int StartOffset { get; set; }   // 起始字符偏移量
        }

        /// <summary>
        /// REGION信息类：存储自定义区域的起始位置和注释
        /// </summary>
        private class RegionInfo
        {
            public int StartLine { get; set; }      // 起始行号
            public int StartOffset { get; set; }   // 起始字符偏移量
            public string Comment { get; set; }    // REGION注释内容
        }
    }
}