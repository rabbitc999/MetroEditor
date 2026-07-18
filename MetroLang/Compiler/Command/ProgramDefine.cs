namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class ProgramDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 清空旧数据，确保每次解析都是全新的状态
            context.MainCity = string.Empty;
            context.Cities.Clear();
            context.Version = string.Empty;
            context.UpdateDate = string.Empty;
            context.CreateDate = string.Empty;
            context.Author = string.Empty;
            context.LanguageOrder.Clear();  // 清空语言顺序

            // 解析块内子命令
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) break;

                string command = parser.ParseId().ToUpperInvariant();
                switch (command)
                {
                    case "AUTHOR":
                        context.Author = parser.ParseString();
                        break;
                    case "KEY_CITY":
                        context.MainCity = parser.ParseString();
                        break;
                    case "OTHER_CITY":
                        var cities = ParseCityListOrSingle(parser);
                        foreach (var city in cities)
                            if (!context.Cities.ContainsKey(city))
                                context.Cities[city] = new Dictionary<string, string>();
                        break;
                    case "VERSION":
                        context.Version = parser.ParseString();
                        break;
                    case "CREATE_DATE":
                        context.CreateDate = parser.ParseString();
                        break;
                    case "UPDATE_DATE":
                        context.UpdateDate = parser.ParseString();
                        break;
                    case "LANGUAGE_ORDER":   // 新增：语言顺序定义
                        // 支持数组格式：['en', 'zh-Hant', 'zh-Hans']
                        context.LanguageOrder = parser.ParseStringList();
                        break;
                    default:
                        throw new SyntaxException($"PROGRAM_DEFINE 内未知子命令 '{command}'", parser.Peek());
                }
            }

            // 校验必填项：主城市必须定义
            if (string.IsNullOrEmpty(context.MainCity))
                throw new SyntaxException("PROGRAM_DEFINE 必须包含 KEY_CITY", parser.Peek());

            // 设置默认值（如果未显式指定）
            if (string.IsNullOrEmpty(context.Version)) context.Version = "V1.0";
            if (string.IsNullOrEmpty(context.UpdateDate)) context.UpdateDate = DateTime.Now.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(context.CreateDate)) context.CreateDate = DateTime.Now.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(context.Author)) context.Author = "匿名";

            // 将主城市纳入城市管理
            if (!context.Cities.ContainsKey(context.MainCity))
                context.Cities[context.MainCity] = new Dictionary<string, string>();
            context.Cities[context.MainCity]["version"] = context.Version;
            context.Cities[context.MainCity]["update_date"] = context.UpdateDate;
        }

        private List<string> ParseCityListOrSingle(Parser parser)
        {
            if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                return parser.ParseStringList();
            else
                return new List<string> { parser.ParseString() };
        }
    }
}