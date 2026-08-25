using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 表示一个词法单元（Token），是 MetroLang 源代码的最小语义单位。
    /// </summary>
    public partial class Token
    {
        /// <summary>
        /// Token 的类型。
        /// </summary>
        public TokenType TokenType { get; }

        /// <summary>
        /// Token 对应的原始文本（词素）。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Token 所在行号（从 1 开始）。
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// 该 Token 所在文件的绝对目录
        /// </summary>
        public string SourceDirectory { get; }

        /// <summary>
        /// 构造一个新的 Token。
        /// </summary>
        /// <param name="type">Token 类型。</param>
        /// <param name="value">原始文本。</param>
        /// <param name="line">行号。</param>
        public Token(TokenType type, string value, int line = 0, string sourceDirectory = "")
        {
            TokenType = type;
            Value = value;
            Line = line;
            SourceDirectory = sourceDirectory;
        }

        /// <summary>
        /// 便于调试与日志输出的字符串表示。
        /// </summary>
        public override string ToString()
        {
            return $"[{TokenType}] '{Value}' (Line: {Line})";
        }
    }
}
