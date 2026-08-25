using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 词法分析器
    /// </summary>
    public static class Lexer
    {
        /// <summary>
        /// 核心分词正则表达式。
        /// 匹配顺序即优先级：空白 -> 注释 -> 数字 -> 字符串 -> 标识符 -> 符号 -> 非法字符。
        /// 符号组现已支持多字符比较运算符（<=, >=, ==, !=）以及单字符符号。
        /// 组索引说明:
        /// 1: 空白符
        /// 2: 注释 (#...)
        /// 3: 数字
        /// 4: 字符串 ("..." 或 '...')
        /// 5: 标识符
        /// 6: 符号（包含多字符运算符）
        /// 7: 非法字符
        /// </summary>
        /// <summary>
        /// 核心分词正则表达式。
        /// 匹配顺序即优先级：空白 -> 注释 -> 数字 -> 字符串 -> 标识符 -> 符号 -> 非法字符。
        /// 标识符现在允许包含 *（用于通配符模式）。
        /// 符号组已移除 *，避免拆分通配符模式。
        /// 组索引说明:
        /// 1: 空白符
        /// 2: 注释 (#...)
        /// 3: 数字
        /// 4: 字符串 ("..." 或 '...')
        /// 5: 标识符（允许字母、数字、下划线、星号）
        /// 6: 符号（多字符运算符和单字符符号，不含 *）
        /// 7: 非法字符
        /// </summary>
        private static readonly Regex _tokenRegex = new Regex(
            @"([ \t]+)|(#[^\n]*)|([+\-]?(?:\d+\.\d+|\d+\.|\.\d+|\d+)(?:[Ee][+\-]?\d+)?)|(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')|([A-Za-z_][A-Za-z0-9_*]*)|(<=|>=|==|!=|[\[\](){}:,=<%>!])|(.)",
            RegexOptions.ECMAScript | RegexOptions.Compiled);

        /// <summary>
        /// 预处理文本，移除注释但保留位置信息（换行符和长度），
        /// 以防止后续解析行号和列号时出现偏差。
        /// </summary>
        private static string RemoveComments(string sourceText)
        {
            var placeholderMap = new Dictionary<string, string>();

            // 1. 保护字符串内容（防止 URL 中的 // 被误判为注释）
            //    字符串支持反斜杠转义（\' \" \\），需完整匹配含转义的字符串
            string textWithPlaceholders = Regex.Replace(sourceText, @"(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')", match =>
            {
                string uniqueKey = Guid.NewGuid().ToString("N");
                placeholderMap[uniqueKey] = match.Value;
                return uniqueKey;
            });

            // 2. 处理 // 单行注释（替换为等长空格，保持 Token 位置）
            string textWithoutSingleLineComments = Regex.Replace(
                textWithPlaceholders,
                @"//.*?$",
                match => new string(' ', match.Length),
                RegexOptions.Multiline);

            // 3. 处理 /* ... */ 多行注释（保留换行符，其余替换为空格）
            string textWithoutMultiLineComments = Regex.Replace(
                textWithoutSingleLineComments,
                @"/\*.*?\*/",
                match =>
                {
                    var commentBlock = match.Value;
                    var replacementBuilder = new StringBuilder();
                    foreach (char character in commentBlock)
                    {
                        if (character == '\n' || character == '\r')
                            replacementBuilder.Append(character);
                        else
                            replacementBuilder.Append(' ');
                    }
                    return replacementBuilder.ToString();
                },
                RegexOptions.Singleline);

            // 4. 还原字符串占位符
            string finalText = textWithoutMultiLineComments;
            foreach (var keyValuePair in placeholderMap)
            {
                finalText = finalText.Replace(keyValuePair.Key, keyValuePair.Value);
            }

            return finalText;
        }

        /// <summary>
        /// 词法分析器
        /// </summary>
        /// <param name="sourceText">源代码</param>
        /// <param name="sourceDirectory">源文件所在目录（用于 Token 来源追踪）</param>
        /// <returns>词法分析后的 Parser 对象</returns>
        public static Parser Tokenize(string sourceText, string sourceDirectory = "")
        {
            sourceText = " " + sourceText;
            var generatedTokens = new List<Token>();
            string processedText = RemoveComments(sourceText);

            // 处理 UTF-8 BOM
            if (processedText.StartsWith("\uFEFF"))
                processedText = processedText[1..];

            string[] sourceLines = processedText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int currentLineNumber = 1;

            foreach (var rawLineContent in sourceLines)
            {
                string currentLine = rawLineContent;

                // 匹配行内的所有 Token
                foreach (Match regexMatch in _tokenRegex.Matches(currentLine))
                {
                    // 组 1: 空白符 -> 跳过
                    if (regexMatch.Groups[1].Success) continue;

                    // 组 2: 注释 -> 跳过 (虽然已预处理，但正则仍可能捕获到残留)
                    if (regexMatch.Groups[2].Success) continue;

                    // 组 3: 数字
                    if (regexMatch.Groups[3].Success)
                    {
                        generatedTokens.Add(new Token(TokenType.Number, regexMatch.Groups[3].Value, currentLineNumber, sourceDirectory));
                    }
                    // 组 4: 字符串
                    else if (regexMatch.Groups[4].Success)
                    {
                        generatedTokens.Add(new Token(TokenType.String, regexMatch.Groups[4].Value, currentLineNumber, sourceDirectory));
                    }
                    // 组 5: 标识符
                    else if (regexMatch.Groups[5].Success)
                    {
                        generatedTokens.Add(new Token(TokenType.Id, regexMatch.Groups[5].Value, currentLineNumber, sourceDirectory));
                    }
                    // 组 6: 符号（包含多字符运算符）
                    else if (regexMatch.Groups[6].Success)
                    {
                        generatedTokens.Add(new Token(TokenType.Symbol, regexMatch.Groups[6].Value, currentLineNumber, sourceDirectory));
                    }
                    // 组 7: 非法字符
                    else if (regexMatch.Groups[7].Success)
                    {
                        generatedTokens.Add(new Token(TokenType.Mismatch, regexMatch.Groups[7].Value, currentLineNumber, sourceDirectory));
                    }
                }

                // 每行结束后添加 Newline Token（最后一行若没有换行符则不加）
                bool isLastLine = currentLineNumber >= sourceLines.Length;
                bool endsWithNewline = rawLineContent.EndsWith("\n") || rawLineContent.EndsWith("\r\n");

                if (!isLastLine || endsWithNewline)
                {
                    generatedTokens.Add(new Token(TokenType.Newline, "\n", currentLineNumber));
                }

                currentLineNumber++;
            }

            return new Parser(generatedTokens);
        }
    }
}