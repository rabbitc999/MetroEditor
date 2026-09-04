using MetroEditor.MetroLang.Compiler;
using System.Collections.Generic;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>
    /// 解析 ECHO 表达式并将其保存到上下文，供后处理阶段输出。
    /// </summary>
    internal class Echo : ICommandHandler
    {
        /// <summary>读取表达式并追加到消息列表。</summary>
        public void Handle(Parser parser, Context context)
        {
            string expr = ParseExpression(parser);
            context.EchoMessages.Add(expr);
        }

        private string ParseExpression(Parser parser)
        {
            var parts = new List<string>();
            // 表达式由字符串和标识符片段组成，使用加号连接；标识符保留为占位符。
            while (true)
            {
                if (parser.Peek().TokenType == TokenType.String)
                {
                    parts.Add(parser.ParseString());
                }
                else if (parser.Peek().TokenType == TokenType.Id)
                {
                    string id = parser.ParseId().ToUpperInvariant();
                    parts.Add($"{{{id}}}"); // 占位符，后处理时替换
                }
                else
                {
                    break;
                }

                // 遇到 + 继续拼接
                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "+")
                {
                    parser.ParseSymbol("+");
                    continue;
                }
                break;
            }
            return string.Join("", parts);
        }
    }
}
