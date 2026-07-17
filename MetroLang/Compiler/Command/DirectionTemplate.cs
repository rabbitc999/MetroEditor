// DirectionTemplate.cs
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class DirectionTemplateCommand : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string templateId = parser.ParseId();
            if (context.DirectionTemplates.ContainsKey(templateId))
                throw new SyntaxException($"方向模板 '{templateId}' 已存在", parser.Peek());

            var template = new DirectionTemplate();

            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) break;

                string key = parser.ParseId().ToUpperInvariant();
                switch (key)
                {
                    case "INHERITS":
                        template.Inherits = parser.ParseId();
                        break;
                    case "NICK_NAME":
                        template.Alias = parser.ParseString();
                        break;
                    case "NICK_COLOR":
                        template.AliasColor = parser.ParseString();
                        break;
                    case "IS_FUTURE":
                        string val = parser.ParseId().ToUpperInvariant();
                        template.IsFuture = (val == "TRUE");
                        break;
                    case "CONNECT_STATION":
                        template.ConnectStation = parser.ParseString();
                        break;
                    case "LOOP":
                        template.Attrs.Add("LOOP");
                        // 可指定 inner/outer
                        if (parser.Peek().TokenType == TokenType.Id)
                        {
                            string type = parser.ParseId().ToUpperInvariant();
                            if (type == "INNER" || type == "OUTER")
                                template.DirectionType = type.ToLowerInvariant();
                            else
                                throw new SyntaxException($"LOOP 类型必须是 INNER 或 OUTER", parser.Peek());
                        }
                        break;
                    case "BRANCH":
                        template.Attrs.Add("BRANCH");
                        break;
                    case "FAST":
                        template.Attrs.Add("FAST");
                        break;
                    default:
                        throw new SyntaxException($"DIRECTION_TEMPLATE 内未知属性 '{key}'", parser.Peek());
                }
            }

            context.DirectionTemplates[templateId] = template;
        }
    }
}