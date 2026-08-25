using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 表示 MetroLang 语法解析过程中发现的语法错误。
    /// 包含出错位置对应的 Token 信息，便于精确定位和提示。
    /// </summary>
    public class SyntaxException : Exception
    {
        /// <summary>
        /// 触发异常的 Token（词法单元）。
        /// </summary>
        public Token Token { get; }

        /// <summary>
        /// 初始化语法异常的新实例。
        /// </summary>
        /// <param name="message">错误描述信息。</param>
        /// <param name="token">发生错误的 Token。</param>
        public SyntaxException(string message, Token token)
            : base(BuildErrorMessage(message, token))
        {
            Token = token;
        }

        /// <summary>
        /// 构建包含 Token 位置与内容信息的错误消息。
        /// </summary>
        private static string BuildErrorMessage(string message, Token token)
        {
            // 清理换行符，避免在错误信息中出现多行混乱
            string sanitizedValue = token.Value.Replace("\n", "").Replace("\r", "");
            string typeDescription = GetTokenTypeDescription(token);

            return $"第{token.Line}行[类型='{typeDescription}', 值='{sanitizedValue}']: {message}";
        }

        /// <summary>
        /// 将 Token 类型转换为更友好的中文描述。
        /// </summary>
        private static string GetTokenTypeDescription(Token token) => token.TokenType switch
        {
            TokenType.Id => "命令或名称",
            TokenType.Number => "数字",
            TokenType.String => "文本",
            TokenType.Symbol => "符号",
            TokenType.Newline => "换行",
            TokenType.Mismatch => "非法字符",
            TokenType.End => "文件结束",
            _ => token.TokenType.ToString() // 兜底：返回枚举的原始名称
        };
    }
}
