<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 预处理阶段：处理 %define、%undefine、%if、%else、%endif、%include、%import 指令，
    /// 生成过滤后的 Token 流供语法分析使用。
    /// </summary>
    public static class Preprocessor
    {
        private class PreprocessorState
        {
            public HashSet<string> Symbols { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Stack<bool> ConditionStack { get; } = new Stack<bool>();
            public HashSet<string> IncludedFiles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> ImportedFiles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 预处理入口
        /// </summary>
        public static List<Token> Process(List<Token> tokens, string baseDirectory, Context context, out List<string> errors)
        {
            var state = new PreprocessorState();
            // 所有嵌套 include/import 共用状态，避免重复读取文件或突破递归深度。
            return ProcessInternal(tokens, baseDirectory, context, state, out errors, 0);
        }

        private static List<Token> ProcessInternal(
            List<Token> tokens,
            string baseDirectory,
            Context context,
            PreprocessorState state,
            out List<string> errors,
            int depth)
        {
            const int MAX_DEPTH = 100;
            errors = new List<string>();
            var output = new List<Token>();

            string currentDirectory = string.IsNullOrEmpty(baseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetFullPath(baseDirectory);

            for (int i = 0; i < tokens.Count; i++)
            {
                var current = tokens[i];

                // 预处理指令本身不进入语法分析器，普通 Token 仅在当前分支启用时转发。
                // ---------- 检测 % 指令 ----------
                if (current.TokenType == TokenType.Symbol && current.Value == "%")
                {
                    if (i + 1 >= tokens.Count || tokens[i + 1].TokenType != TokenType.Id)
                    {
                        errors.Add($"第 {current.Line} 行: '%' 后必须跟指令名称");
                        continue;
                    }

                    string directive = tokens[i + 1].Value.ToUpperInvariant();
                    bool isActive = (state.ConditionStack.Count == 0 || state.ConditionStack.Peek() == true);

                    // ----- %include -----
                    if (directive == "INCLUDE")
                    {
                        i += 2;
                        if (i < tokens.Count && tokens[i].TokenType == TokenType.String)
                        {
                            string rawPath = StripQuotes(tokens[i].Value);
                            string absolutePath = Path.GetFullPath(Path.Combine(currentDirectory, rawPath));

                            if (depth >= MAX_DEPTH)
                            {
                                errors.Add($"包含层级过深（超过 {MAX_DEPTH} 层），可能循环引用: {absolutePath}");
                                i++;
                                continue;
                            }

                            if (state.IncludedFiles.Contains(absolutePath))
                            {
                                // 已包含过，跳过
                                i++;
                                continue;
                            }

                            if (!File.Exists(absolutePath))
                            {
                                errors.Add($"文件不存在: {absolutePath}");
                                i++;
                                continue;
                            }

                            state.IncludedFiles.Add(absolutePath);

                            if (isActive)
                            {
                                string content = File.ReadAllText(absolutePath, Encoding.UTF8);
                                var subTokens = Lexer.Tokenize(content, Path.GetDirectoryName(absolutePath)).Tokens;
                                var processedSub = ProcessInternal(
                                    subTokens,
                                    Path.GetDirectoryName(absolutePath),
                                    context,
                                    state,
                                    out var subErrors,
                                    depth + 1
                                );
                                errors.AddRange(subErrors);
                                output.AddRange(processedSub);
                            }
                            i++;
                        }
                        else
                        {
                            errors.Add($"第 {tokens[i - 2].Line} 行: %include 后必须跟字符串路径");
                        }
                        continue;
                    }

                    // ----- %import -----
                    if (directive == "IMPORT")
                    {
                        i += 2;
                        if (i < tokens.Count && tokens[i].TokenType == TokenType.String)
                        {
                            string rawPath = StripQuotes(tokens[i].Value);
                            string absolutePath = Path.GetFullPath(Path.Combine(currentDirectory, rawPath));

                            if (depth >= MAX_DEPTH)
                            {
                                errors.Add($"导入层级过深（超过 {MAX_DEPTH} 层）: {absolutePath}");
                                i++;
                                continue;
                            }

                            if (state.ImportedFiles.Contains(absolutePath))
                            {
                                i++;
                                continue;
                            }

                            if (!File.Exists(absolutePath))
                            {
                                errors.Add($"导入文件不存在: {absolutePath}");
                                i++;
                                continue;
                            }

                            state.ImportedFiles.Add(absolutePath);

                            // 检查 AS 关键字
                            if (i + 1 >= tokens.Count || tokens[i + 1].TokenType != TokenType.Id ||
                                tokens[i + 1].Value.ToUpperInvariant() != "AS")
                            {
                                errors.Add($"%import 需要 'AS' 关键字指定导入类型");
                                i++;
                                continue;
                            }
                            i += 2; // 跳过 AS
                            if (i >= tokens.Count || tokens[i].TokenType != TokenType.Id)
                            {
                                errors.Add($"%import 需要指定导入类型 (STATIONS 或 DISTANCES)");
                                i++;
                                continue;
                            }
                            string importType = tokens[i].Value.ToUpperInvariant();

                            if (isActive)
                            {
                                try
                                {
                                    var importedTokens = ImportData(absolutePath, importType, context);
                                    var processedImport = ProcessInternal(
                                        importedTokens,
                                        Path.GetDirectoryName(absolutePath),
                                        context,
                                        state,
                                        out var importErrors,
                                        depth + 1
                                    );
                                    errors.AddRange(importErrors);
                                    output.AddRange(processedImport);
                                }
                                catch (Exception ex)
                                {
                                    errors.Add($"导入文件 '{absolutePath}' 失败: {ex.Message}");
                                }
                            }
                            i++; // 跳过类型 Token
                        }
                        else
                        {
                            errors.Add($"第 {tokens[i - 2].Line} 行: %import 后必须跟字符串路径");
                        }
                        continue;
                    }

                    // ----- 其他预处理指令 -----
                    i = ProcessDirective(directive, i, tokens, state, isActive, errors);
                    continue;
                }

                // ---------- 普通 Token ----------
                bool shouldOutput = (state.ConditionStack.Count == 0 || state.ConditionStack.Peek() == true);
                if (shouldOutput)
                {
                    output.Add(current);
                }
            }

            if (state.ConditionStack.Count > 0)
                errors.Add($"存在未闭合的 %if（缺少 {state.ConditionStack.Count} 个 %endif）");

            return output;
        }

        private static int ProcessDirective(
            string directive,
            int index,
            List<Token> tokens,
            PreprocessorState state,
            bool isActive,
            List<string> errors)
        {
            int i = index;
            Token current = tokens[i];

            switch (directive)
            {
                case "DEFINE":
                    if (i + 2 < tokens.Count && tokens[i + 2].TokenType == TokenType.Id)
                    {
                        if (isActive)
                            state.Symbols.Add(tokens[i + 2].Value);
                        i += 2;
                    }
                    else
                    {
                        errors.Add($"第 {current.Line} 行: %define 后必须跟符号名称");
                        i += 1;
                    }
                    break;

                case "UNDEFINE":
                    if (i + 2 < tokens.Count && tokens[i + 2].TokenType == TokenType.Id)
                    {
                        if (isActive)
                            state.Symbols.Remove(tokens[i + 2].Value);
                        i += 2;
                    }
                    else
                    {
                        errors.Add($"第 {current.Line} 行: %undefine 后必须跟符号名称");
                        i += 1;
                    }
                    break;

                case "IF":
                    if (i + 2 < tokens.Count && tokens[i + 2].TokenType == TokenType.Id)
                    {
                        bool condition = state.Symbols.Contains(tokens[i + 2].Value);
                        state.ConditionStack.Push(condition);
                        i += 2;
                    }
                    else
                    {
                        errors.Add($"第 {current.Line} 行: %if 后必须跟符号名称");
                        i += 1;
                    }
                    break;

                case "ELSE":
                    if (state.ConditionStack.Count == 0)
                        errors.Add($"第 {current.Line} 行: 多余的 %else，缺少匹配的 %if");
                    else
                    {
                        bool currentCondition = state.ConditionStack.Pop();
                        state.ConditionStack.Push(!currentCondition);
                    }
                    i += 1;
                    break;

                case "ENDIF":
                    if (state.ConditionStack.Count == 0)
                        errors.Add($"第 {current.Line} 行: 多余的 %endif，缺少匹配的 %if");
                    else
                        state.ConditionStack.Pop();
                    i += 1;
                    break;

                default:
                    errors.Add($"第 {current.Line} 行: 未知预处理指令 '%{directive}'");
                    i += 1;
                    break;
            }

            // 跳过行剩余 Token（直到换行）
            while (i + 1 < tokens.Count && tokens[i + 1].TokenType != TokenType.Newline)
                i++;

            return i;
        }

        /// <summary>
        /// 导入外部数据，生成 Token 列表
        /// </summary>
        private static List<Token> ImportData(string filePath, string importType, Context context)
        {
            var tokens = new List<Token>();
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (importType == "STATIONS")
            {
                // 仅支持 CSV 格式
                if (ext != ".csv")
                    throw new InvalidOperationException("仅支持 .csv 格式导入车站");

                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length == 0)
                    throw new InvalidOperationException("CSV 文件为空");

                // 解析表头
                var header = ParseCsvLine(lines[0]);
                int nameIdx = -1, lonIdx = -1, latIdx = -1, cityIdx = -1, otherNamesIdx = -1;
                for (int j = 0; j < header.Length; j++)
                {
                    string h = header[j].Trim().ToUpperInvariant();
                    if (h == "站名" || h == "STATION") nameIdx = j;
                    else if (h == "经度" || h == "LON") lonIdx = j;
                    else if (h == "纬度" || h == "LAT") latIdx = j;
                    else if (h == "城市" || h == "CITY") cityIdx = j;
                    else if (h == "其他语言名称" || h == "英文名" || h == "OTHER_NAMES" || h == "EN_NAME") otherNamesIdx = j;
                }
                if (nameIdx == -1 || lonIdx == -1 || latIdx == -1)
                    throw new InvalidOperationException("CSV 必须包含 '站名', '经度', '纬度' 列");

                // 生成 STATION_DEFINE 块
                tokens.Add(new Token(TokenType.Id, "STATION_DEFINE", 0));
                tokens.Add(new Token(TokenType.Newline, "\n", 0));

                for (int row = 1; row < lines.Length; row++)
                {
                    if (string.IsNullOrWhiteSpace(lines[row])) continue;
                    var cols = ParseCsvLine(lines[row]);
                    if (cols.Length <= Math.Max(nameIdx, Math.Max(lonIdx, latIdx))) continue;

                    string name = cols[nameIdx].Trim();
                    double lon = double.Parse(cols[lonIdx].Trim());
                    double lat = double.Parse(cols[latIdx].Trim());

                    tokens.Add(new Token(TokenType.Id, "NODE", 0));
                    tokens.Add(new Token(TokenType.String, $"\"{name}\"", 0));
                    tokens.Add(new Token(TokenType.Id, "COORD", 0));
                    tokens.Add(new Token(TokenType.Number, lon.ToString(), 0));
                    tokens.Add(new Token(TokenType.Symbol, ",", 0));
                    tokens.Add(new Token(TokenType.Number, lat.ToString(), 0));

                    if (cityIdx != -1 && cityIdx < cols.Length && !string.IsNullOrWhiteSpace(cols[cityIdx]))
                    {
                        tokens.Add(new Token(TokenType.Id, "CITY", 0));
                        tokens.Add(new Token(TokenType.String, $"\"{cols[cityIdx].Trim()}\"", 0));
                    }

                    // 可选的"其他语言名称"列：一个或多个名称，用分号/竖线分隔 → OTHER_NAME ['a', 'b']
                    if (otherNamesIdx != -1 && otherNamesIdx < cols.Length && !string.IsNullOrWhiteSpace(cols[otherNamesIdx]))
                    {
                        var names = cols[otherNamesIdx]
                            .Split(new[] { ';', '|', '；' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(n => n.Trim())
                            .Where(n => n.Length > 0)
                            .ToArray();
                        if (names.Length > 0)
                        {
                            tokens.Add(new Token(TokenType.Id, "OTHER_NAME", 0));
                            tokens.Add(new Token(TokenType.Symbol, "[", 0));
                            for (int ni = 0; ni < names.Length; ni++)
                            {
                                if (ni > 0)
                                    tokens.Add(new Token(TokenType.Symbol, ",", 0));
                                tokens.Add(new Token(TokenType.String, $"\"{names[ni]}\"", 0));
                            }
                            tokens.Add(new Token(TokenType.Symbol, "]", 0));
                        }
                    }
                    tokens.Add(new Token(TokenType.Newline, "\n", 0));
                }

                tokens.Add(new Token(TokenType.Id, "END_DEFINE", 0));
                tokens.Add(new Token(TokenType.Newline, "\n", 0));
            }
            else
            {
                throw new InvalidOperationException($"不支持的导入类型: {importType}");
            }

            return tokens;
        }

        /// <summary>
        /// 简单 CSV 行解析（支持引号内的逗号）
        /// </summary>
        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString().Trim());
            return result.ToArray();
        }

        private static string StripQuotes(string input)
        {
            if (input.Length >= 2 &&
                (input[0] == '"' || input[0] == '\'') &&
                input[0] == input[input.Length - 1])
            {
                return input.Substring(1, input.Length - 2);
            }
            return input;
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 预处理阶段：处理 %define、%undefine、%if、%else、%endif、%include、%import 指令，
    /// 生成过滤后的 Token 流供语法分析使用。
    /// </summary>
    public static class Preprocessor
    {
        private class PreprocessorState
        {
            public HashSet<string> Symbols { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Stack<bool> ConditionStack { get; } = new Stack<bool>();
            public HashSet<string> IncludedFiles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> ImportedFiles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 预处理入口
        /// </summary>
        public static List<Token> Process(List<Token> tokens, string baseDirectory, Context context, out List<string> errors)
        {
            var state = new PreprocessorState();
            return ProcessInternal(tokens, baseDirectory, context, state, out errors, 0);
        }

        private static List<Token> ProcessInternal(
            List<Token> tokens,
            string baseDirectory,
            Context context,
            PreprocessorState state,
            out List<string> errors,
            int depth)
        {
            const int MAX_DEPTH = 100;
            errors = new List<string>();
            var output = new List<Token>();

            string currentDirectory = string.IsNullOrEmpty(baseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetFullPath(baseDirectory);

            for (int i = 0; i < tokens.Count; i++)
            {
                var current = tokens[i];

                // ---------- 检测 % 指令 ----------
                if (current.TokenType == TokenType.Symbol && current.Value == "%")
                {
                    if (i + 1 >= tokens.Count || tokens[i + 1].TokenType != TokenType.Id)
                    {
                        errors.Add($"第 {current.Line} 行: '%' 后必须跟指令名称");
                        continue;
                    }

                    string directive = tokens[i + 1].Value.ToUpperInvariant();
                    bool isActive = (state.ConditionStack.Count == 0 || state.ConditionStack.Peek() == true);

                    // ----- %include -----
                    if (directive == "INCLUDE")
                    {
                        i += 2;
                        if (i < tokens.Count && tokens[i].TokenType == TokenType.String)
                        {
                            string rawPath = StripQuotes(tokens[i].Value);
                            string absolutePath = Path.GetFullPath(Path.Combine(currentDirectory, rawPath));

                            if (depth >= MAX_DEPTH)
                            {
                                errors.Add($"包含层级过深（超过 {MAX_DEPTH} 层），可能循环引用: {absolutePath}");
                                i++;
                                continue;
                            }

                            if (state.IncludedFiles.Contains(absolutePath))
                            {
                                // 已包含过，跳过
                                i++;
                                continue;
                            }

                            if (!File.Exists(absolutePath))
                            {
                                errors.Add($"文件不存在: {absolutePath}");
                                i++;
                                continue;
                            }

                            state.IncludedFiles.Add(absolutePath);

                            if (isActive)
                            {
                                string content = File.ReadAllText(absolutePath, Encoding.UTF8);
                                var subTokens = Lexer.Tokenize(content, Path.GetDirectoryName(absolutePath)).Tokens;
                                var processedSub = ProcessInternal(
                                    subTokens,
                                    Path.GetDirectoryName(absolutePath),
                                    context,
                                    state,
                                    out var subErrors,
                                    depth + 1
                                );
                                errors.AddRange(subErrors);
                                output.AddRange(processedSub);
                            }
                            i++;
                        }
                        else
                        {
                            errors.Add($"第 {tokens[i - 2].Line} 行: %include 后必须跟字符串路径");
                        }
                        continue;
                    }

                    // ----- %import -----
                    if (directive == "IMPORT")
                    {
                        i += 2;
                        if (i < tokens.Count && tokens[i].TokenType == TokenType.String)
                        {
                            string rawPath = StripQuotes(tokens[i].Value);
                            string absolutePath = Path.GetFullPath(Path.Combine(currentDirectory, rawPath));

                            if (depth >= MAX_DEPTH)
                            {
                                errors.Add($"导入层级过深（超过 {MAX_DEPTH} 层）: {absolutePath}");
                                i++;
                                continue;
                            }

                            if (state.ImportedFiles.Contains(absolutePath))
                            {
                                i++;
                                continue;
                            }

                            if (!File.Exists(absolutePath))
                            {
                                errors.Add($"导入文件不存在: {absolutePath}");
                                i++;
                                continue;
                            }

                            state.ImportedFiles.Add(absolutePath);

                            // 检查 AS 关键字
                            if (i + 1 >= tokens.Count || tokens[i + 1].TokenType != TokenType.Id ||
                                tokens[i + 1].Value.ToUpperInvariant() != "AS")
                            {
                                errors.Add($"%import 需要 'AS' 关键字指定导入类型");
                                i++;
                                continue;
                            }
                            i += 2; // 跳过 AS
                            if (i >= tokens.Count || tokens[i].TokenType != TokenType.Id)
                            {
                                errors.Add($"%import 需要指定导入类型 (STATIONS 或 DISTANCES)");
                                i++;
                                continue;
                            }
                            string importType = tokens[i].Value.ToUpperInvariant();

                            if (isActive)
                            {
                                try
                                {
                                    var importedTokens = ImportData(absolutePath, importType, context);
                                    var processedImport = ProcessInternal(
                                        importedTokens,
                                        Path.GetDirectoryName(absolutePath),
                                        context,
                                        state,
                                        out var importErrors,
                                        depth + 1
                                    );
                                    errors.AddRange(importErrors);
                                    output.AddRange(processedImport);
                                }
                                catch (Exception ex)
                                {
                                    errors.Add($"导入文件 '{absolutePath}' 失败: {ex.Message}");
                                }
                            }
                            i++; // 跳过类型 Token
                        }
                        else
                        {
                            errors.Add($"第 {tokens[i - 2].Line} 行: %import 后必须跟字符串路径");
                        }
                        continue;
                    }

                    // ----- 其他预处理指令 -----
                    i = ProcessDirective(directive, i, tokens, state, isActive, errors);
                    continue;
                }

                // ---------- 普通 Token ----------
                bool shouldOutput = (state.ConditionStack.Count == 0 || state.ConditionStack.Peek() == true);
                if (shouldOutput)
                {
                    output.Add(current);
                }
            }

            if (state.ConditionStack.Count > 0)
                errors.Add($"存在未闭合的 %if（缺少 {state.ConditionStack.Count} 个 %endif）");

            return output;
        }

        private static int ProcessDirective(
            string directive,
            int index,
            List<Token> tokens,
            PreprocessorState state,
            bool isActive,
            List<string> errors)
        {
            int i = index;
            Token current = tokens[i];

            switch (directive)
            {
                case "DEFINE":
                    if (i + 2 < tokens.Count && tokens[i + 2].TokenType == TokenType.Id)
                    {
                        if (isActive)
                            state.Symbols.Add(tokens[i + 2].Value);
                        i += 2;
                    }
                    else
                    {
                        errors.Add($"第 {current.Line} 行: %define 后必须跟符号名称");
                        i += 1;
                    }
                    break;

                case "UNDEFINE":
                    if (i + 2 < tokens.Count && tokens[i + 2].TokenType == TokenType.Id)
                    {
                        if (isActive)
                            state.Symbols.Remove(tokens[i + 2].Value);
                        i += 2;
                    }
                    else
                    {
                        errors.Add($"第 {current.Line} 行: %undefine 后必须跟符号名称");
                        i += 1;
                    }
                    break;

                case "IF":
                    if (i + 2 < tokens.Count && tokens[i + 2].TokenType == TokenType.Id)
                    {
                        bool condition = state.Symbols.Contains(tokens[i + 2].Value);
                        state.ConditionStack.Push(condition);
                        i += 2;
                    }
                    else
                    {
                        errors.Add($"第 {current.Line} 行: %if 后必须跟符号名称");
                        i += 1;
                    }
                    break;

                case "ELSE":
                    if (state.ConditionStack.Count == 0)
                        errors.Add($"第 {current.Line} 行: 多余的 %else，缺少匹配的 %if");
                    else
                    {
                        bool currentCondition = state.ConditionStack.Pop();
                        state.ConditionStack.Push(!currentCondition);
                    }
                    i += 1;
                    break;

                case "ENDIF":
                    if (state.ConditionStack.Count == 0)
                        errors.Add($"第 {current.Line} 行: 多余的 %endif，缺少匹配的 %if");
                    else
                        state.ConditionStack.Pop();
                    i += 1;
                    break;

                default:
                    errors.Add($"第 {current.Line} 行: 未知预处理指令 '%{directive}'");
                    i += 1;
                    break;
            }

            // 跳过行剩余 Token（直到换行）
            while (i + 1 < tokens.Count && tokens[i + 1].TokenType != TokenType.Newline)
                i++;

            return i;
        }

        /// <summary>
        /// 导入外部数据，生成 Token 列表
        /// </summary>
        private static List<Token> ImportData(string filePath, string importType, Context context)
        {
            var tokens = new List<Token>();
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (importType == "STATIONS")
            {
                // 仅支持 CSV 格式
                if (ext != ".csv")
                    throw new InvalidOperationException("仅支持 .csv 格式导入车站");

                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length == 0)
                    throw new InvalidOperationException("CSV 文件为空");

                // 解析表头
                var header = ParseCsvLine(lines[0]);
                int nameIdx = -1, lonIdx = -1, latIdx = -1, cityIdx = -1, otherNamesIdx = -1;
                for (int j = 0; j < header.Length; j++)
                {
                    string h = header[j].Trim().ToUpperInvariant();
                    if (h == "站名" || h == "STATION") nameIdx = j;
                    else if (h == "经度" || h == "LON") lonIdx = j;
                    else if (h == "纬度" || h == "LAT") latIdx = j;
                    else if (h == "城市" || h == "CITY") cityIdx = j;
                    else if (h == "其他语言名称" || h == "英文名" || h == "OTHER_NAMES" || h == "EN_NAME") otherNamesIdx = j;
                }
                if (nameIdx == -1 || lonIdx == -1 || latIdx == -1)
                    throw new InvalidOperationException("CSV 必须包含 '站名', '经度', '纬度' 列");

                // 生成 STATION_DEFINE 块
                tokens.Add(new Token(TokenType.Id, "STATION_DEFINE", 0));
                tokens.Add(new Token(TokenType.Newline, "\n", 0));

                for (int row = 1; row < lines.Length; row++)
                {
                    if (string.IsNullOrWhiteSpace(lines[row])) continue;
                    var cols = ParseCsvLine(lines[row]);
                    if (cols.Length <= Math.Max(nameIdx, Math.Max(lonIdx, latIdx))) continue;

                    string name = cols[nameIdx].Trim();
                    double lon = double.Parse(cols[lonIdx].Trim());
                    double lat = double.Parse(cols[latIdx].Trim());

                    tokens.Add(new Token(TokenType.Id, "NODE", 0));
                    tokens.Add(new Token(TokenType.String, $"\"{name}\"", 0));
                    tokens.Add(new Token(TokenType.Id, "COORD", 0));
                    tokens.Add(new Token(TokenType.Number, lon.ToString(), 0));
                    tokens.Add(new Token(TokenType.Symbol, ",", 0));
                    tokens.Add(new Token(TokenType.Number, lat.ToString(), 0));

                    if (cityIdx != -1 && cityIdx < cols.Length && !string.IsNullOrWhiteSpace(cols[cityIdx]))
                    {
                        tokens.Add(new Token(TokenType.Id, "CITY", 0));
                        tokens.Add(new Token(TokenType.String, $"\"{cols[cityIdx].Trim()}\"", 0));
                    }

                    // 可选的"其他语言名称"列：一个或多个名称，用分号/竖线分隔 → OTHER_NAME ['a', 'b']
                    if (otherNamesIdx != -1 && otherNamesIdx < cols.Length && !string.IsNullOrWhiteSpace(cols[otherNamesIdx]))
                    {
                        var names = cols[otherNamesIdx]
                            .Split(new[] { ';', '|', '；' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(n => n.Trim())
                            .Where(n => n.Length > 0)
                            .ToArray();
                        if (names.Length > 0)
                        {
                            tokens.Add(new Token(TokenType.Id, "OTHER_NAME", 0));
                            tokens.Add(new Token(TokenType.Symbol, "[", 0));
                            for (int ni = 0; ni < names.Length; ni++)
                            {
                                if (ni > 0)
                                    tokens.Add(new Token(TokenType.Symbol, ",", 0));
                                tokens.Add(new Token(TokenType.String, $"\"{names[ni]}\"", 0));
                            }
                            tokens.Add(new Token(TokenType.Symbol, "]", 0));
                        }
                    }
                    tokens.Add(new Token(TokenType.Newline, "\n", 0));
                }

                tokens.Add(new Token(TokenType.Id, "END_DEFINE", 0));
                tokens.Add(new Token(TokenType.Newline, "\n", 0));
            }
            else
            {
                throw new InvalidOperationException($"不支持的导入类型: {importType}");
            }

            return tokens;
        }

        /// <summary>
        /// 简单 CSV 行解析（支持引号内的逗号）
        /// </summary>
        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString().Trim());
            return result.ToArray();
        }

        private static string StripQuotes(string input)
        {
            if (input.Length >= 2 &&
                (input[0] == '"' || input[0] == '\'') &&
                input[0] == input[input.Length - 1])
            {
                return input.Substring(1, input.Length - 2);
            }
            return input;
        }
    }
}
>>>>>>> 68704bec0197aa2f176e12f57e1de8fb0a964814
