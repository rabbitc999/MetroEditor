// LineTemplate.cs
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>
    /// 解析线路模板定义，为后续线路创建提供可继承的默认字段。
    /// </summary>
    internal class LineTemplateCommand : ICommandHandler
    {
        /// <summary>读取模板 ID 和属性，完成后注册到编译上下文。</summary>
        public void Handle(Parser parser, Context context)
        {
            string templateId = parser.ParseId();
            if (context.LineTemplates.ContainsKey(templateId))
                throw new SyntaxException($"线路模板 '{templateId}' 已存在", parser.Peek());

            var template = new LineTemplate();

            while (true)
            {
                // 模板属性按键值顺序读取，直到遇到定义结束标记。
                parser.ParseNewlines();
                if (parser.IsEndDefine()) break;

                string key = parser.ParseId().ToUpperInvariant();
                switch (key)
                {
                    case "INHERITS":
                        template.Inherits = parser.ParseId();
                        break;
                    case "NAME": template.Name = parser.ParseString(); break;
                    case "COLOR": template.Color = parser.ParseString(); break;
                    case "CITY": template.City = parser.ParseString(); break;
                    case "OPEN_YEAR": template.OpenYear = parser.ParseInt(); break;
                    case "FORMATION": template.Formation = parser.ParseString(); break;
                    case "MAXSPEED": template.Maxspeed = parser.ParseDouble(); break;
                    case "POWER": template.PowerKw = parser.ParseDouble(); break;
                    case "BRAKE": template.Brake = parser.ParseDouble(); break;
                    case "DISABLED":
                        string val = parser.ParseId().ToUpperInvariant();
                        template.Disabled = (val == "TRUE");
                        break;
                    case "IMAGE": template.ImageBase64 = parser.ParseString(); break;
                    case "LINK": template.Link = parser.ParseString(); break;
                    case "SPECIAL": template.IsSpecial = true; break;
                    default:
                        throw new SyntaxException($"LINE_TEMPLATE 内未知属性 '{key}'", parser.Peek());
                }
            }

            context.LineTemplates[templateId] = template;
        }
    }
}
