<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 编译解析上下文（ParserContext）是整个编译流程的数据中枢。
    /// 它集中存放所有解析过程中产生的解析数据，并提供安全的访问和修改方法。
    /// 所有数据通过属性对外暴露，修改通过专门的方法完成，确保数据一致性。
    /// </summary>
    public class Parser
    {
        /// <summary>
        /// 词法分析产出的 Token 流，供语法分析遍历
        /// </summary>
        private List<Token> _tokens = new();
        /// <summary>
        /// 当前解析指针在 Token 流中的索引位置
        /// </summary>
        private int _pos;
        /// <summary>
        /// 错误列表信息
        /// </summary>
        public List<string> _errorList = new();
        public List<string> _warnings = new();

        public List<Token> Tokens { get { return _tokens; } }
        public int Position { get { return _pos; } }
        public int Length { get { return _tokens.Count; } }
        public List<string> ErrorList { get { return _errorList; } }
        public int ErrorCount { get { return _errorList.Count; } }
        public List<string> WarningList { get { return _warnings; } }
        public int WarningCount { get { return _warnings.Count; } }

        public Parser(List<Token> tokens)
        {
            // Parser 只维护当前位置和诊断集合；具体语义由各命令处理器完成。
            _tokens = tokens;
            _pos = 0;
        }

        public void AddError(string error)
        {
            _errorList.Add(error);
        }

        public void AddWarning(string warn)
        {
            _warnings.Add(warn);
        }

        // ====================================================================
        // Token 预读：查看当前位置的 Token 而不消费它
        // 用于语法分析时的前瞻（Lookahead）操作
        // 当到达 Token 流末尾时，返回一个类型为 End 的虚拟 Token
        // ====================================================================
        public Token Peek()
        {
            if (_pos < _tokens.Count)
            {
                return _tokens[_pos];
            }
            else
            {
                return new Token(TokenType.End, "END", GetCurrentLine());
            }
        }

        // ====================================================================
        // Token 消费：验证并消费当前 Token
        // 参数：
        //   expectedType: 期望的 Token 类型
        //   expectedValue: 可选的期望 Token 值（用于精确匹配）
        // 返回：被消费的 Token
        // 异常：当 Token 类型或值不匹配时抛出 SyntaxException
        // ====================================================================
        public Token Consume(TokenType expectedType, string expectedValue = null)
        {
            var currentToken = Peek();

            // 检查是否到达文件末尾
            if (currentToken.TokenType == TokenType.End)
            {
                // 末尾使用虚拟 End Token，让调用方统一处理 EOF。
                return currentToken;
            }

            // 验证 Token 类型
            if (currentToken.TokenType != expectedType)
            {
                // 在消费前完成类型校验，错误恢复时不会跳过当前 Token。
                string expectedDescription = GetTokenTypeDescription(expectedType);
                string actualDescription = GetTokenTypeDescription(currentToken.TokenType);
                string suggestion = GetSuggestion(expectedType, currentToken);
                throw new SyntaxException(
                    $"第{currentToken.Line}行：这里应该写「{expectedDescription}」，但写成了「{actualDescription}」{suggestion}",
                    currentToken);
            }

            // 验证 Token 值（如果指定了期望值）
            if (!string.IsNullOrEmpty(expectedValue) && currentToken.Value != expectedValue)
                throw new SyntaxException(
                    $"第{currentToken.Line}行：期望「{expectedValue}」，但得到了「{currentToken.Value}」",
                    currentToken);

            // 消费 Token 并移动指针
            _pos++;
            // 只有校验成功才推进指针，保证后续诊断仍指向正确位置。
            return currentToken;
        }

        public int GetCurrentLine() => Tokens[_pos - 1].Line;
        public string ParseId(string expectedValue = null) => Consume(TokenType.Id, expectedValue).Value;
        public void ParseSymbol(string expectedValue = null) => Consume(TokenType.Symbol, expectedValue);
        public double ParseDouble() => double.Parse(Consume(TokenType.Number).Value);
        public int ParseInt() => int.Parse(Consume(TokenType.Number).Value);
        public string ParseString() => StripQuotes(Consume(TokenType.String).Value);
        public bool IsEndDefine()
        {
            if (IsPeekId("END_DEFINE"))
            {
                ParseId();
                return true;
            }
            return false;
        }
        public Token SkipPeek()
        {
            while (_pos < Length)
            {
                var currentToken = Peek();

                // 跳过换行符
                if (currentToken.TokenType == TokenType.Newline)
                {
                    Consume(TokenType.Newline);
                    continue;
                }
                // 处理非法字符，记录警告但不中断解析
                else if (currentToken.TokenType == TokenType.Mismatch)
                {
                    Skip();
                    AddWarning($"第{currentToken.Line}行: 忽略非法字符 '{currentToken.Value}'");
                    continue;
                }
                else
                {
                    return currentToken;
                }
            }
            return Tokens[Tokens.Count - 1];
        }
        public void Skip() { _pos++; }


        public bool IsPeekId(string value) => Peek().TokenType == TokenType.Id && Peek().Value.Equals(value, StringComparison.OrdinalIgnoreCase);

        // ====================================================================
        // 跳过换行符：消费当前位置开始的所有连续换行符
        // 用于忽略源代码中的空白行，保持解析逻辑的简洁性
        // ====================================================================
        public void ParseNewlines()
        {
            while (Peek().TokenType == TokenType.Newline)
            {
                Consume(TokenType.Newline);
            }
        }

        // ====================================================================
        // Token 类型描述：将 Token 类型转换为用户友好的中文描述
        // 用于生成清晰的错误提示信息
        // ====================================================================
        private static string GetTokenTypeDescription(TokenType type) => type switch
        {
            TokenType.Id => "命令或名称",
            TokenType.Number => "数字",
            TokenType.String => "文本",
            TokenType.Symbol => "符号",
            TokenType.Newline => "换行",
            _ => type.ToString()
        };

        // ====================================================================
        // 错误提示建议：根据期望类型和实际 Token 提供针对性的修正建议
        // 帮助开发者快速定位和解决语法问题
        // ====================================================================
        private static string GetSuggestion(TokenType expected, Token actual)
        {
            if (expected == TokenType.Id && actual.TokenType == TokenType.Number)
                return "（比如这里应该写线路ID或命令，不能只写数字）";
            if (expected == TokenType.String && actual.TokenType == TokenType.Id)
                return "（文本需要用单引号或双引号括起来）";
            if (expected == TokenType.Number && actual.TokenType == TokenType.String)
                return "（这里需要数字，不能写文字）";
            return "";
        }

        // ====================================================================
        // 去除引号：移除字符串两端的单引号或双引号
        // 参数：s - 原始字符串
        // 返回：去除引号后的字符串
        // 用途：处理词法分析器生成的字符串字面量
        // ====================================================================
        private static string StripQuotes(string inputString)
        {
            // 长度至少为 2，且首尾是相同的引号
            if (inputString.Length >= 2 &&
                (inputString[0] == '"' || inputString[0] == '\'') &&
                inputString[0] == inputString[inputString.Length - 1])
            {
                return UnescapeString(inputString.Substring(1, inputString.Length - 2));
            }

            return inputString; // 无引号则原样返回
        }

        /// <summary>
        /// 反转义字符串字面量中的转义序列。
        /// 支持：\\ \' \" \n \t \r；未知转义 \x 保留后随字符 x（丢弃反斜杠）。
        /// </summary>
        private static string UnescapeString(string content)
        {
            if (content.IndexOf('\\') < 0) return content;

            var sb = new StringBuilder(content.Length);
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '\\' && i + 1 < content.Length)
                {
                    char next = content[++i];
                    sb.Append(next switch
                    {
                        '\\' => '\\',
                        '\'' => '\'',
                        '"' => '"',
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        _ => next
                    });
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        // ====================================================================
        // 字符串列表解析：支持数组格式的字符串列表
        // 语法示例：["车站A", "车站B", "车站C"]
        // ====================================================================
        public List<string> ParseStringList()
        {
            var stringList = new List<string>();
            // 列表项允许换行和逗号分隔，统一在这里处理边界与闭合括号。
            Consume(TokenType.Symbol, "[");

            while (true)
            {
                ParseNewlines();
                if (Peek().TokenType == TokenType.Symbol && Peek().Value == "]")
                {
                    Consume(TokenType.Symbol, "]");
                    break;
                }

                // 解析字符串值并去除引号
                string stringValue = ParseString();
                stringList.Add(stringValue);

                ParseNewlines();
                var nextToken = Peek();
                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == "]")
                    continue;  // 列表结束，下一次循环处理闭合括号

                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == ",")
                    Consume(TokenType.Symbol, ",");
                else
                    throw new SyntaxException($"列表项之间缺少逗号或列表未闭合", nextToken);
            }
            return stringList;
        }

        // ====================================================================
        // 星期列表解析：支持数组格式的星期枚举集合
        // 语法示例：[MONDAY, TUESDAY, WEDNESDAY]
        // ====================================================================
        public List<DayOfWeek> ParseDayOfWeekList()
        {
            var dayList = new List<DayOfWeek>();
            Consume(TokenType.Symbol, "[");

            while (true)
            {
                ParseNewlines();
                if (Peek().TokenType == TokenType.Symbol && Peek().Value == "]")
                {
                    Consume(TokenType.Symbol, "]");
                    break;
                }

                // 解析星期标识符
                string dayString = ParseId().ToUpperInvariant();
                DayOfWeek day = ParseDayOfWeekString(dayString);
                dayList.Add(day);

                ParseNewlines();
                var nextToken = Peek();
                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == "]")
                    continue;  // 列表结束，下一次循环处理闭合括号

                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == ",")
                    Consume(TokenType.Symbol, ",");
                else
                    throw new SyntaxException($"星期列表元素之间缺少逗号或列表未闭合", nextToken);
            }
            return dayList;
        }

        // ====================================================================
        // 星期字符串转换：将标识符转换为 DayOfWeek 枚举值
        // 支持标准的星期名称标识符
        // ====================================================================
        public DayOfWeek ParseDayOfWeekString(string identifier)
        {
            return identifier switch
            {
                "MONDAY" => DayOfWeek.Monday,
                "TUESDAY" => DayOfWeek.Tuesday,
                "WEDNESDAY" => DayOfWeek.Wednesday,
                "THURSDAY" => DayOfWeek.Thursday,
                "FRIDAY" => DayOfWeek.Friday,
                "SATURDAY" => DayOfWeek.Saturday,
                "SUNDAY" => DayOfWeek.Sunday,
                _ => throw new SyntaxException($"无效的星期标识符 '{identifier}'", Peek())
            };
        }

        // ====================================================================
        // 星期修饰符解析：处理 WORKDAY、WEEKEND 等快捷标识符
        // 将语义化的日期修饰符转换为具体的星期枚举集合
        // ====================================================================
        public List<DayOfWeek> ParseDayOfWeekModifier(string modifier)
        {
            var dayCollection = new List<DayOfWeek>();
            if (modifier == "WORKDAY" || modifier == "WEEKDAY")
            {
                // 工作日：周一至周五
                dayCollection.AddRange([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
            }
            else if (modifier == "WEEKEND")
            {
                // 周末：周六和周日
                dayCollection.AddRange(new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
            }
            else
            {
                // 单个星期标识符
                dayCollection.Add(ParseDayOfWeekString(modifier));
            }
            return dayCollection;
        }

        // ====================================================================
        // 星期修饰符解析：处理 WORKDAY、WEEKEND 等快捷标识符
        // 将语义化的日期修饰符转换为具体的星期枚举集合
        // ====================================================================
        public List<DayOfWeek> ParseDayOfWeekModifier()
        {
            return ParseDayOfWeekModifier(ParseId().ToUpperInvariant());
        }

        /// <summary>
        /// 解析时间值为秒数
        /// 支持两种格式：
        /// 1. 纯数字：直接作为秒数（如 "30"）
        /// 2. 分:秒格式：如 "1:30" 表示 90 秒
        /// 3. 分钟格式：如 "5:" 表示 300 秒
        /// </summary>
        /// <returns>解析得到的秒数</returns>
        public double ParseTimeInSeconds()
        {
            // 读取第一部分数值（可能是分钟或秒）
            double first = double.Parse(Consume(TokenType.Number).Value);

            // 检查后续是否为冒号，以判断是否为分:秒格式
            if (Peek().TokenType == TokenType.Symbol && Peek().Value == ":")
            {
                Consume(TokenType.Symbol, ":");  // 消耗冒号分隔符
                if (Peek().TokenType == TokenType.Number)
                {
                    // 冒号后有数字：完整的分:秒格式（如 "1:30"）
                    double second = double.Parse(Consume(TokenType.Number).Value);
                    return first * 60 + second;  // 转换为总秒数
                }
                else
                {
                    // 冒号后无数字：纯分钟格式（如 "5:"）
                    return first * 60;
                }
            }
            else
            {
                // 无冒号：纯数字，直接作为秒数
                return first;
            }
        }

        /// <summary>
        /// 解析时钟时间（时:分格式）
        /// 用于解析运营时间、发车时间等具体时刻
        /// </summary>
        /// <returns>解析得到的 TimeSpan 对象</returns>
        public TimeSpan ParseClockTime()
        {
            // 解析小时部分
            int hour = (int)double.Parse(Consume(TokenType.Number).Value);
            // 消耗冒号分隔符
            Consume(TokenType.Symbol, ":");
            // 解析分钟部分
            int minute = (int)double.Parse(Consume(TokenType.Number).Value);
            // 构造 TimeSpan（秒数固定为0）
            return new TimeSpan(hour, minute, 0);
        }

        /// <summary>
        /// 解析时间范围（起始时间-结束时间）
        /// 通常用于解析运营时段、高峰期定义等
        /// </summary>
        /// <returns>包含起始时间和结束时间的元组</returns>
        public (TimeSpan start, TimeSpan end) ParseTimeRange()
        {
            // 依次解析起始时间和结束时间
            var start = ParseClockTime();
            var end = ParseClockTime();
            return (start, end);
        }

        // ====================================================================
        // 错误同步：在发生语法错误后恢复到下一个可识别的命令位置
        // 通过跳过无效 Token 直到找到下一个命令标识符（Id）来实现错误恢复
        // 避免单个错误导致整个解析过程中断
        // ====================================================================
        public void SynchronizeToNextCommand()
        {
            // 跳过当前错误命令，恢复到下一个顶层标识符，避免单个错误终止整份文件解析。
            while (_pos < _tokens.Count)
            {
                var currentToken = Peek();

                // 如果遇到命令标识符，认为已找到下一个有效命令的起点
                if (currentToken.TokenType == TokenType.Id)
                    return;

                // 如果遇到换行符，消费它并检查下一位置
                if (currentToken.TokenType == TokenType.Newline)
                {
                    Consume(TokenType.Newline);
                    if (Peek().TokenType == TokenType.Id)
                        return;
                }
                else
                {
                    // 跳过其他类型的无效 Token
                    _pos++;
                }
            }
        }

        // ====================================================================
        // 跳过单个命令（用于条件编译不满足时）
        // 跳过当前命令及其参数，直到遇到换行或下一个顶级命令
        // ====================================================================
        public void SkipOneCommand()
        {
            while (_pos < _tokens.Count)
            {
                var token = Peek();
                // 遇到换行或下一个命令标识符时停止
                if (token.TokenType == TokenType.Newline || token.TokenType == TokenType.Id)
                    break;
                _pos++;
            }
            // 如果当前是换行，消耗掉
            if (Peek().TokenType == TokenType.Newline)
                Consume(TokenType.Newline);
        }

        // ====================================================================
        // 线路ID列表解析：支持单个ID或数组格式的线路ID列表
        // 语法示例：LINE1 或 [LINE1, LINE2, LINE3]
        // ====================================================================
        public List<string> ParseLineIdList()
        {
            var lineIdList = new List<string>();

            if (Peek().TokenType == TokenType.Symbol && Peek().Value == "[")
            {
                // 数组格式
                Consume(TokenType.Symbol, "[");
                while (true)
                {
                    ParseNewlines();
                    if (Peek().TokenType == TokenType.Symbol && Peek().Value == "]")
                    {
                        Consume(TokenType.Symbol, "]");
                        break;
                    }

                    string lineId = ParseId();
                    lineIdList.Add(lineId);

                    ParseNewlines();
                    var nextToken = Peek();
                    if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == "]")
                        continue;
                    if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == ",")
                        Consume(TokenType.Symbol, ",");
                    else
                        throw new SyntaxException($"线路列表项之间缺少逗号或列表未闭合", nextToken);
                }
            }
            else
            {
                // 单个线路ID
                string lineId = ParseId();
                lineIdList.Add(lineId);
            }

            return lineIdList;
        }

        // ====================================================================
        // 距离列表解析：支持数字和 NIL（用 1e50 表示无穷大/无连接）
        // 语法示例：[1000, 1500, NIL, 800]
        // ====================================================================
        public List<double> ParseNumberList()
        {
            var distanceList = new List<double>();
            ParseSymbol("[");

            while (true)
            {
                ParseNewlines();
                if (Peek().TokenType == TokenType.Symbol && Peek().Value == "]")
                {
                    ParseSymbol("]");
                    break;
                }

                Token currentToken = Peek();
                if (currentToken.TokenType == TokenType.Id && currentToken.Value.Equals("NIL"))
                {
                    // NIL 表示无距离（如终点站），用极大值 1e50 表示
                    ParseId();
                    distanceList.Add(1e50);
                }
                else if (currentToken.TokenType == TokenType.Number)
                {
                    distanceList.Add(ParseDouble());
                }
                else
                {
                    throw new SyntaxException($"距离列表中只允许数字或 NIL，但遇到了 '{currentToken.Value}'", currentToken);
                }

                ParseNewlines();
                var nextToken = Peek();
                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == "]")
                    continue;  // 列表结束，下一次循环处理闭合括号

                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == ",")
                {
                    ParseSymbol(",");
                    continue;
                }

                // 语法错误：缺少逗号或列表未正确闭合
                if (nextToken.TokenType == TokenType.Number ||
                   nextToken.TokenType == TokenType.Id && nextToken.Value.Equals("NIL"))
                    throw new SyntaxException($"距离列表元素之间缺少逗号", nextToken);

                throw new SyntaxException($"距离列表元素之间缺少逗号，或列表未正确闭合", nextToken);
            }
            return distanceList;
        }

        public string GetCurrentTokenSourceDirectory()
        {
            if (_pos > 0 && _pos <= _tokens.Count)
                return _tokens[_pos - 1].SourceDirectory;
            return "";
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 编译解析上下文（ParserContext）是整个编译流程的数据中枢。
    /// 它集中存放所有解析过程中产生的解析数据，并提供安全的访问和修改方法。
    /// 所有数据通过属性对外暴露，修改通过专门的方法完成，确保数据一致性。
    /// </summary>
    public class Parser
    {
        /// <summary>
        /// 词法分析产出的 Token 流，供语法分析遍历
        /// </summary>
        private List<Token> _tokens = new();
        /// <summary>
        /// 当前解析指针在 Token 流中的索引位置
        /// </summary>
        private int _pos;
        /// <summary>
        /// 错误列表信息
        /// </summary>
        public List<string> _errorList = new();
        public List<string> _warnings = new();

        public List<Token> Tokens { get { return _tokens; } }
        public int Position { get { return _pos; } }
        public int Length { get { return _tokens.Count; } }
        public List<string> ErrorList { get { return _errorList; } }
        public int ErrorCount { get { return _errorList.Count; } }
        public List<string> WarningList { get { return _warnings; } }
        public int WarningCount { get { return _warnings.Count; } }

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
            _pos = 0;
        }

        public void AddError(string error)
        {
            _errorList.Add(error);
        }

        public void AddWarning(string warn)
        {
            _warnings.Add(warn);
        }

        // ====================================================================
        // Token 预读：查看当前位置的 Token 而不消费它
        // 用于语法分析时的前瞻（Lookahead）操作
        // 当到达 Token 流末尾时，返回一个类型为 End 的虚拟 Token
        // ====================================================================
        public Token Peek()
        {
            if (_pos < _tokens.Count)
            {
                return _tokens[_pos];
            }
            else
            {
                return new Token(TokenType.End, "END", GetCurrentLine());
            }
        }

        // ====================================================================
        // Token 消费：验证并消费当前 Token
        // 参数：
        //   expectedType: 期望的 Token 类型
        //   expectedValue: 可选的期望 Token 值（用于精确匹配）
        // 返回：被消费的 Token
        // 异常：当 Token 类型或值不匹配时抛出 SyntaxException
        // ====================================================================
        public Token Consume(TokenType expectedType, string expectedValue = null)
        {
            var currentToken = Peek();

            // 检查是否到达文件末尾
            if (currentToken.TokenType == TokenType.End)
            {
                return currentToken;
            }

            // 验证 Token 类型
            if (currentToken.TokenType != expectedType)
            {
                string expectedDescription = GetTokenTypeDescription(expectedType);
                string actualDescription = GetTokenTypeDescription(currentToken.TokenType);
                string suggestion = GetSuggestion(expectedType, currentToken);
                throw new SyntaxException(
                    $"第{currentToken.Line}行：这里应该写「{expectedDescription}」，但写成了「{actualDescription}」{suggestion}",
                    currentToken);
            }

            // 验证 Token 值（如果指定了期望值）
            if (!string.IsNullOrEmpty(expectedValue) && currentToken.Value != expectedValue)
                throw new SyntaxException(
                    $"第{currentToken.Line}行：期望「{expectedValue}」，但得到了「{currentToken.Value}」",
                    currentToken);

            // 消费 Token 并移动指针
            _pos++;
            return currentToken;
        }

        public int GetCurrentLine() => Tokens[_pos - 1].Line;
        public string ParseId(string expectedValue = null) => Consume(TokenType.Id, expectedValue).Value;
        public void ParseSymbol(string expectedValue = null) => Consume(TokenType.Symbol, expectedValue);
        public double ParseDouble() => double.Parse(Consume(TokenType.Number).Value);
        public int ParseInt() => int.Parse(Consume(TokenType.Number).Value);
        public string ParseString() => StripQuotes(Consume(TokenType.String).Value);
        public bool IsEndDefine()
        {
            if (IsPeekId("END_DEFINE"))
            {
                ParseId();
                return true;
            }
            return false;
        }
        public Token SkipPeek()
        {
            while (_pos < Length)
            {
                var currentToken = Peek();

                // 跳过换行符
                if (currentToken.TokenType == TokenType.Newline)
                {
                    Consume(TokenType.Newline);
                    continue;
                }
                // 处理非法字符，记录警告但不中断解析
                else if (currentToken.TokenType == TokenType.Mismatch)
                {
                    Skip();
                    AddWarning($"第{currentToken.Line}行: 忽略非法字符 '{currentToken.Value}'");
                    continue;
                }
                else
                {
                    return currentToken;
                }
            }
            return Tokens[Tokens.Count - 1];
        }
        public void Skip() { _pos++; }


        public bool IsPeekId(string value) => Peek().TokenType == TokenType.Id && Peek().Value.Equals(value, StringComparison.OrdinalIgnoreCase);

        // ====================================================================
        // 跳过换行符：消费当前位置开始的所有连续换行符
        // 用于忽略源代码中的空白行，保持解析逻辑的简洁性
        // ====================================================================
        public void ParseNewlines()
        {
            while (Peek().TokenType == TokenType.Newline)
            {
                Consume(TokenType.Newline);
            }
        }

        // ====================================================================
        // Token 类型描述：将 Token 类型转换为用户友好的中文描述
        // 用于生成清晰的错误提示信息
        // ====================================================================
        private static string GetTokenTypeDescription(TokenType type) => type switch
        {
            TokenType.Id => "命令或名称",
            TokenType.Number => "数字",
            TokenType.String => "文本",
            TokenType.Symbol => "符号",
            TokenType.Newline => "换行",
            _ => type.ToString()
        };

        // ====================================================================
        // 错误提示建议：根据期望类型和实际 Token 提供针对性的修正建议
        // 帮助开发者快速定位和解决语法问题
        // ====================================================================
        private static string GetSuggestion(TokenType expected, Token actual)
        {
            if (expected == TokenType.Id && actual.TokenType == TokenType.Number)
                return "（比如这里应该写线路ID或命令，不能只写数字）";
            if (expected == TokenType.String && actual.TokenType == TokenType.Id)
                return "（文本需要用单引号或双引号括起来）";
            if (expected == TokenType.Number && actual.TokenType == TokenType.String)
                return "（这里需要数字，不能写文字）";
            return "";
        }

        // ====================================================================
        // 去除引号：移除字符串两端的单引号或双引号
        // 参数：s - 原始字符串
        // 返回：去除引号后的字符串
        // 用途：处理词法分析器生成的字符串字面量
        // ====================================================================
        private static string StripQuotes(string inputString)
        {
            // 长度至少为 2，且首尾是相同的引号
            if (inputString.Length >= 2 &&
                (inputString[0] == '"' || inputString[0] == '\'') &&
                inputString[0] == inputString[inputString.Length - 1])
            {
                return UnescapeString(inputString.Substring(1, inputString.Length - 2));
            }

            return inputString; // 无引号则原样返回
        }

        /// <summary>
        /// 反转义字符串字面量中的转义序列。
        /// 支持：\\ \' \" \n \t \r；未知转义 \x 保留后随字符 x（丢弃反斜杠）。
        /// </summary>
        private static string UnescapeString(string content)
        {
            if (content.IndexOf('\\') < 0) return content;

            var sb = new StringBuilder(content.Length);
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '\\' && i + 1 < content.Length)
                {
                    char next = content[++i];
                    sb.Append(next switch
                    {
                        '\\' => '\\',
                        '\'' => '\'',
                        '"' => '"',
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        _ => next
                    });
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        // ====================================================================
        // 字符串列表解析：支持数组格式的字符串列表
        // 语法示例：["车站A", "车站B", "车站C"]
        // ====================================================================
        public List<string> ParseStringList()
        {
            var stringList = new List<string>();
            Consume(TokenType.Symbol, "[");

            while (true)
            {
                ParseNewlines();
                if (Peek().TokenType == TokenType.Symbol && Peek().Value == "]")
                {
                    Consume(TokenType.Symbol, "]");
                    break;
                }

                // 解析字符串值并去除引号
                string stringValue = ParseString();
                stringList.Add(stringValue);

                ParseNewlines();
                var nextToken = Peek();
                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == "]")
                    continue;  // 列表结束，下一次循环处理闭合括号

                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == ",")
                    Consume(TokenType.Symbol, ",");
                else
                    throw new SyntaxException($"列表项之间缺少逗号或列表未闭合", nextToken);
            }
            return stringList;
        }

        // ====================================================================
        // 星期列表解析：支持数组格式的星期枚举集合
        // 语法示例：[MONDAY, TUESDAY, WEDNESDAY]
        // ====================================================================
        public List<DayOfWeek> ParseDayOfWeekList()
        {
            var dayList = new List<DayOfWeek>();
            Consume(TokenType.Symbol, "[");

            while (true)
            {
                ParseNewlines();
                if (Peek().TokenType == TokenType.Symbol && Peek().Value == "]")
                {
                    Consume(TokenType.Symbol, "]");
                    break;
                }

                // 解析星期标识符
                string dayString = ParseId().ToUpperInvariant();
                DayOfWeek day = ParseDayOfWeekString(dayString);
                dayList.Add(day);

                ParseNewlines();
                var nextToken = Peek();
                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == "]")
                    continue;  // 列表结束，下一次循环处理闭合括号

                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == ",")
                    Consume(TokenType.Symbol, ",");
                else
                    throw new SyntaxException($"星期列表元素之间缺少逗号或列表未闭合", nextToken);
            }
            return dayList;
        }

        // ====================================================================
        // 星期字符串转换：将标识符转换为 DayOfWeek 枚举值
        // 支持标准的星期名称标识符
        // ====================================================================
        public DayOfWeek ParseDayOfWeekString(string identifier)
        {
            return identifier switch
            {
                "MONDAY" => DayOfWeek.Monday,
                "TUESDAY" => DayOfWeek.Tuesday,
                "WEDNESDAY" => DayOfWeek.Wednesday,
                "THURSDAY" => DayOfWeek.Thursday,
                "FRIDAY" => DayOfWeek.Friday,
                "SATURDAY" => DayOfWeek.Saturday,
                "SUNDAY" => DayOfWeek.Sunday,
                _ => throw new SyntaxException($"无效的星期标识符 '{identifier}'", Peek())
            };
        }

        // ====================================================================
        // 星期修饰符解析：处理 WORKDAY、WEEKEND 等快捷标识符
        // 将语义化的日期修饰符转换为具体的星期枚举集合
        // ====================================================================
        public List<DayOfWeek> ParseDayOfWeekModifier(string modifier)
        {
            var dayCollection = new List<DayOfWeek>();
            if (modifier == "WORKDAY" || modifier == "WEEKDAY")
            {
                // 工作日：周一至周五
                dayCollection.AddRange([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
            }
            else if (modifier == "WEEKEND")
            {
                // 周末：周六和周日
                dayCollection.AddRange(new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
            }
            else
            {
                // 单个星期标识符
                dayCollection.Add(ParseDayOfWeekString(modifier));
            }
            return dayCollection;
        }

        // ====================================================================
        // 星期修饰符解析：处理 WORKDAY、WEEKEND 等快捷标识符
        // 将语义化的日期修饰符转换为具体的星期枚举集合
        // ====================================================================
        public List<DayOfWeek> ParseDayOfWeekModifier()
        {
            return ParseDayOfWeekModifier(ParseId().ToUpperInvariant());
        }

        /// <summary>
        /// 解析时间值为秒数
        /// 支持两种格式：
        /// 1. 纯数字：直接作为秒数（如 "30"）
        /// 2. 分:秒格式：如 "1:30" 表示 90 秒
        /// 3. 分钟格式：如 "5:" 表示 300 秒
        /// </summary>
        /// <returns>解析得到的秒数</returns>
        public double ParseTimeInSeconds()
        {
            // 读取第一部分数值（可能是分钟或秒）
            double first = double.Parse(Consume(TokenType.Number).Value);

            // 检查后续是否为冒号，以判断是否为分:秒格式
            if (Peek().TokenType == TokenType.Symbol && Peek().Value == ":")
            {
                Consume(TokenType.Symbol, ":");  // 消耗冒号分隔符
                if (Peek().TokenType == TokenType.Number)
                {
                    // 冒号后有数字：完整的分:秒格式（如 "1:30"）
                    double second = double.Parse(Consume(TokenType.Number).Value);
                    return first * 60 + second;  // 转换为总秒数
                }
                else
                {
                    // 冒号后无数字：纯分钟格式（如 "5:"）
                    return first * 60;
                }
            }
            else
            {
                // 无冒号：纯数字，直接作为秒数
                return first;
            }
        }

        /// <summary>
        /// 解析时钟时间（时:分格式）
        /// 用于解析运营时间、发车时间等具体时刻
        /// </summary>
        /// <returns>解析得到的 TimeSpan 对象</returns>
        public TimeSpan ParseClockTime()
        {
            // 解析小时部分
            int hour = (int)double.Parse(Consume(TokenType.Number).Value);
            // 消耗冒号分隔符
            Consume(TokenType.Symbol, ":");
            // 解析分钟部分
            int minute = (int)double.Parse(Consume(TokenType.Number).Value);
            // 构造 TimeSpan（秒数固定为0）
            return new TimeSpan(hour, minute, 0);
        }

        /// <summary>
        /// 解析时间范围（起始时间-结束时间）
        /// 通常用于解析运营时段、高峰期定义等
        /// </summary>
        /// <returns>包含起始时间和结束时间的元组</returns>
        public (TimeSpan start, TimeSpan end) ParseTimeRange()
        {
            // 依次解析起始时间和结束时间
            var start = ParseClockTime();
            var end = ParseClockTime();
            return (start, end);
        }

        // ====================================================================
        // 错误同步：在发生语法错误后恢复到下一个可识别的命令位置
        // 通过跳过无效 Token 直到找到下一个命令标识符（Id）来实现错误恢复
        // 避免单个错误导致整个解析过程中断
        // ====================================================================
        public void SynchronizeToNextCommand()
        {
            while (_pos < _tokens.Count)
            {
                var currentToken = Peek();

                // 如果遇到命令标识符，认为已找到下一个有效命令的起点
                if (currentToken.TokenType == TokenType.Id)
                    return;

                // 如果遇到换行符，消费它并检查下一位置
                if (currentToken.TokenType == TokenType.Newline)
                {
                    Consume(TokenType.Newline);
                    if (Peek().TokenType == TokenType.Id)
                        return;
                }
                else
                {
                    // 跳过其他类型的无效 Token
                    _pos++;
                }
            }
        }

        // ====================================================================
        // 跳过单个命令（用于条件编译不满足时）
        // 跳过当前命令及其参数，直到遇到换行或下一个顶级命令
        // ====================================================================
        public void SkipOneCommand()
        {
            while (_pos < _tokens.Count)
            {
                var token = Peek();
                // 遇到换行或下一个命令标识符时停止
                if (token.TokenType == TokenType.Newline || token.TokenType == TokenType.Id)
                    break;
                _pos++;
            }
            // 如果当前是换行，消耗掉
            if (Peek().TokenType == TokenType.Newline)
                Consume(TokenType.Newline);
        }

        // ====================================================================
        // 线路ID列表解析：支持单个ID或数组格式的线路ID列表
        // 语法示例：LINE1 或 [LINE1, LINE2, LINE3]
        // ====================================================================
        public List<string> ParseLineIdList()
        {
            var lineIdList = new List<string>();

            if (Peek().TokenType == TokenType.Symbol && Peek().Value == "[")
            {
                // 数组格式
                Consume(TokenType.Symbol, "[");
                while (true)
                {
                    ParseNewlines();
                    if (Peek().TokenType == TokenType.Symbol && Peek().Value == "]")
                    {
                        Consume(TokenType.Symbol, "]");
                        break;
                    }

                    string lineId = ParseId();
                    lineIdList.Add(lineId);

                    ParseNewlines();
                    var nextToken = Peek();
                    if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == "]")
                        continue;
                    if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == ",")
                        Consume(TokenType.Symbol, ",");
                    else
                        throw new SyntaxException($"线路列表项之间缺少逗号或列表未闭合", nextToken);
                }
            }
            else
            {
                // 单个线路ID
                string lineId = ParseId();
                lineIdList.Add(lineId);
            }

            return lineIdList;
        }

        // ====================================================================
        // 距离列表解析：支持数字和 NIL（用 1e50 表示无穷大/无连接）
        // 语法示例：[1000, 1500, NIL, 800]
        // ====================================================================
        public List<double> ParseNumberList()
        {
            var distanceList = new List<double>();
            ParseSymbol("[");

            while (true)
            {
                ParseNewlines();
                if (Peek().TokenType == TokenType.Symbol && Peek().Value == "]")
                {
                    ParseSymbol("]");
                    break;
                }

                Token currentToken = Peek();
                if (currentToken.TokenType == TokenType.Id && currentToken.Value.Equals("NIL"))
                {
                    // NIL 表示无距离（如终点站），用极大值 1e50 表示
                    ParseId();
                    distanceList.Add(1e50);
                }
                else if (currentToken.TokenType == TokenType.Number)
                {
                    distanceList.Add(ParseDouble());
                }
                else
                {
                    throw new SyntaxException($"距离列表中只允许数字或 NIL，但遇到了 '{currentToken.Value}'", currentToken);
                }

                ParseNewlines();
                var nextToken = Peek();
                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == "]")
                    continue;  // 列表结束，下一次循环处理闭合括号

                if (nextToken.TokenType == TokenType.Symbol && nextToken.Value == ",")
                {
                    ParseSymbol(",");
                    continue;
                }

                // 语法错误：缺少逗号或列表未正确闭合
                if (nextToken.TokenType == TokenType.Number ||
                   nextToken.TokenType == TokenType.Id && nextToken.Value.Equals("NIL"))
                    throw new SyntaxException($"距离列表元素之间缺少逗号", nextToken);

                throw new SyntaxException($"距离列表元素之间缺少逗号，或列表未正确闭合", nextToken);
            }
            return distanceList;
        }

        public string GetCurrentTokenSourceDirectory()
        {
            if (_pos > 0 && _pos <= _tokens.Count)
                return _tokens[_pos - 1].SourceDirectory;
            return "";
        }
    }
}
>>>>>>> 68704bec0197aa2f176e12f57e1de8fb0a964814
