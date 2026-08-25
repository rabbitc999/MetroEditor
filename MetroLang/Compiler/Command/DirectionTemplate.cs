<<<<<<< HEAD
﻿// DirectionTemplate.cs
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>
    /// 解析方向模板定义，并保存可被方向命令继承的默认属性。
    /// </summary>
    internal class DirectionTemplateCommand : ICommandHandler
    {
        /// <summary>读取模板属性直到定义结束标记。</summary>
        public void Handle(Parser parser, Context context)
        {
            string templateId = parser.ParseId();
            if (context.DirectionTemplates.ContainsKey(templateId))
                throw new SyntaxException($"方向模板 '{templateId}' 已存在", parser.Peek());

            var template = new DirectionTemplate();

            while (true)
            {
                // 每一轮处理一个模板属性，ParseNewlines 负责跨行读取。
                parser.ParseNewlines();
                if (parser.IsEndDefine()) break;

                string key = parser.ParseId().ToUpperInvariant();
                switch (key)
                {
                    case "INHERITS":
                        template.Inherits = parser.ParseId();
                        break;
                    case "IS_FUTURE":
                        string val = parser.ParseId().ToUpperInvariant();
                        template.IsFuture = (val == "TRUE");
                        break;
                    case "CONNECT_STATION":
                        template.ConnectStation = parser.ParseString();
                        break;
                    case "LOOP":
                        // LOOP 可选地携带 INNER/OUTER，用于确定环线方向类型。
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
                    default:
                        throw new SyntaxException($"DIRECTION_TEMPLATE 内未知属性 '{key}'", parser.Peek());
                }
            }

            context.DirectionTemplates[templateId] = template;
        }
    }
}
=======
﻿// DirectionTemplate.cs
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
                    default:
                        throw new SyntaxException($"DIRECTION_TEMPLATE 内未知属性 '{key}'", parser.Peek());
                }
            }

            context.DirectionTemplates[templateId] = template;
        }
    }
}
>>>>>>> 68704bec0197aa2f176e12f57e1de8fb0a964814
