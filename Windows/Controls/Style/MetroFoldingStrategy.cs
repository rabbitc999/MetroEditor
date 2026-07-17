using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MetroEditor.Windows.Controls.Style
{
    /// <summary>
    /// Metro 语言专用代码折叠生成器
    /// 为 AvalonEdit 编辑器提供智能折叠区域识别功能
    /// 支持三种折叠类型：多行注释、代码块（DEFINE/IF）、自定义REGION区域
    /// </summary>
    public static class MetroFoldingGenerator
    {
        // ===== 代码块起始关键字集合 =====
        // 包含所有支持块结构的 MetroLang 命令（包括新增的 TEMPLATE 和 MODIFY 块）
        private static readonly HashSet<string> BlockStartKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PROGRAM_DEFINE",
            "LINE_DEFINE",
            "DIRECTION_DEFINE",
            "STATION_DEFINE",
            "PRICE_DEFINE",
            "TRANSFER_DEFINE",
            "TRANSFER_TYPE",
            "VIRTUAL_TRANSFER_DEFINE",
            "OPERATION_DEFINE",
            "HEADWAY_PROFILE",
            "CROWD_PROFILE",
            "DWELL_TIME_DEFINE",
            "SPEED_LIMIT_DEFINE",
            "LABEL_DEFINE",
            "SPECIAL_DEFINE",
            "FACILITY_DEFINE",
            "PROMOTION_DEFINE",
            // 新增：模板与修改块
            "LINE_TEMPLATE",
            "DIRECTION_TEMPLATE",
            "LINE_MODIFY",
            "DIRECTION_MODIFY",
            "POLICY",          // PROMOTION_DEFINE 内部策略块
            "IF",              // 普通 IF / %if
        };

        /// <summary>
        /// 获取文档中所有多行注释的范围
        /// </summary>
        private static List<(int Start, int End)> GetMultiLineCommentRanges(TextDocument document)
        {
            var ranges = new List<(int, int)>();
            var text = document.Text;
            int index = 0;
            while (index < text.Length)
            {
                if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '*')
                {
                    int start = index;
                    int end = text.IndexOf("*/", index + 2);
                    if (end == -1) end = text.Length;
                    else end += 2;
                    ranges.Add((start, end));
                    index = end;
                }
                else index++;
            }
            return ranges;
        }

        /// <summary>
        /// 生成完整的折叠区域列表
        /// </summary>
        public static IEnumerable<NewFolding> CreateFolds(TextDocument document)
        {
            var foldings = new List<NewFolding>();
            if (document == null || document.TextLength == 0) return foldings;

            // 多行注释折叠
            foreach (var (start, end) in GetMultiLineCommentRanges(document))
                if (end - start > 20)
                    foldings.Add(new NewFolding(start, end) { Name = "/* 注释 */", DefaultClosed = false });

            // 代码块折叠
            var lines = document.Lines;
            var blockStack = new Stack<BlockInfo>();
            var regionStack = new Stack<RegionInfo>();

            for (int lineIdx = 0; lineIdx < lines.Count; lineIdx++)
            {
                var line = lines[lineIdx];
                string lineText = document.GetText(line).Trim();

                if (string.IsNullOrEmpty(lineText)) continue;

                // ----- #REGION 处理 -----
                if (IsRegionStart(lineText, out string regionComment))
                {
                    regionStack.Push(new RegionInfo { StartLine = lineIdx, StartOffset = line.Offset, Comment = regionComment });
                    continue;
                }
                if (IsRegionEnd(lineText))
                {
                    if (regionStack.Count > 0)
                    {
                        var reg = regionStack.Pop();
                        if (reg.StartLine < lineIdx)
                            foldings.Add(new NewFolding(reg.StartOffset, line.EndOffset)
                            { Name = $"# REGION {reg.Comment}".Trim(), DefaultClosed = false });
                    }
                    continue;
                }

                // 跳过单行注释
                if (lineText.StartsWith("#")) continue;

                // ----- 检测块起始 (支持普通和预处理 %if) -----
                if (IsBlockStart(lineText, out string keyword))
                {
                    // 跳过单行块（同一行有结束标记）
                    if (lineText.IndexOf("END_DEFINE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        lineText.IndexOf("END_IF", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        lineText.IndexOf("END_TEMPLATE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        lineText.IndexOf("END_POLICY", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    // 跳过 REV 简写方向
                    if (keyword.Equals("DIRECTION_DEFINE", StringComparison.OrdinalIgnoreCase) &&
                        lineText.IndexOf("REV", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    blockStack.Push(new BlockInfo
                    {
                        StartLine = lineIdx,
                        Keyword = keyword,
                        StartOffset = line.Offset,
                    });
                }

                // ----- 检测块结束 (支持 END_DEFINE, END_IF, END_TEMPLATE, END_POLICY, %endif) -----
                if (IsBlockEnd(lineText, out string endKeyword))
                {
                    if (blockStack.Count == 0) continue;

                    var block = blockStack.Pop();

                    // 匹配检查：IF 必须与 END_IF 配对，其他与 END_DEFINE/END_TEMPLATE/END_POLICY 配对
                    bool isMatch = false;
                    if (block.Keyword.Equals("IF", StringComparison.OrdinalIgnoreCase))
                        isMatch = endKeyword.Equals("END_IF", StringComparison.OrdinalIgnoreCase);
                    else if (block.Keyword.Equals("LINE_TEMPLATE", StringComparison.OrdinalIgnoreCase) ||
                             block.Keyword.Equals("DIRECTION_TEMPLATE", StringComparison.OrdinalIgnoreCase))
                        isMatch = endKeyword.Equals("END_TEMPLATE", StringComparison.OrdinalIgnoreCase);
                    else if (block.Keyword.Equals("POLICY", StringComparison.OrdinalIgnoreCase))
                        isMatch = endKeyword.Equals("END_POLICY", StringComparison.OrdinalIgnoreCase);
                    else
                        isMatch = endKeyword.Equals("END_DEFINE", StringComparison.OrdinalIgnoreCase);

                    if (isMatch && block.StartLine < lineIdx - 1)
                    {
                        foldings.Add(new NewFolding(block.StartOffset, line.EndOffset)
                        {
                            Name = $"▼ {block.Keyword}",
                            DefaultClosed = false
                        });
                    }
                }
            }

            return foldings.OrderBy(f => f.StartOffset);
        }

        /// <summary>
        /// 检测是否为块起始行（支持普通命令和预处理 %if）
        /// </summary>
        private static bool IsBlockStart(string lineText, out string keyword)
        {
            keyword = null;
            var upper = lineText.ToUpperInvariant();

            // 预处理 %if 检测
            if (upper.StartsWith("%IF") && (upper.Length == 3 || upper[3] == ' '))
            {
                keyword = "IF";
                return true;
            }

            // 普通块起始关键字
            foreach (var k in BlockStartKeywords)
            {
                if (upper.StartsWith(k + " ", StringComparison.OrdinalIgnoreCase) ||
                    upper.Equals(k, StringComparison.OrdinalIgnoreCase))
                {
                    keyword = k;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检测是否为块结束行（支持 END_DEFINE, END_IF, END_TEMPLATE, END_POLICY, %endif）
        /// </summary>
        private static bool IsBlockEnd(string lineText, out string keyword)
        {
            keyword = null;
            var upper = lineText.ToUpperInvariant();

            // 预处理 %endif
            if (upper.StartsWith("%ENDIF") && (upper.Length == 6 || upper[6] == ' '))
            {
                keyword = "END_IF";
                return true;
            }

            // 普通结束标记
            if (upper.StartsWith("END_DEFINE", StringComparison.OrdinalIgnoreCase) ||
                upper.Equals("END_DEFINE", StringComparison.OrdinalIgnoreCase))
            { keyword = "END_DEFINE"; return true; }

            if (upper.StartsWith("END_IF", StringComparison.OrdinalIgnoreCase) ||
                upper.Equals("END_IF", StringComparison.OrdinalIgnoreCase))
            { keyword = "END_IF"; return true; }

            if (upper.StartsWith("END_TEMPLATE", StringComparison.OrdinalIgnoreCase) ||
                upper.Equals("END_TEMPLATE", StringComparison.OrdinalIgnoreCase))
            { keyword = "END_TEMPLATE"; return true; }

            if (upper.StartsWith("END_POLICY", StringComparison.OrdinalIgnoreCase) ||
                upper.Equals("END_POLICY", StringComparison.OrdinalIgnoreCase))
            { keyword = "END_POLICY"; return true; }

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