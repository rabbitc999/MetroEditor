using MetroEditor.MetroLang.Compiler;
using System.Collections.Generic;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class Echo : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string expr = ParseExpression(parser);
            context.EchoMessages.Add(expr);
        }

        private string ParseExpression(Parser parser)
        {
            var parts = new List<string>();
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