using MetroEditor.MetroLang.Compiler.Compiler;
using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 将.metro 源文件编译为标准 JSON 路网数据。
    /// </summary>
    public partial class MetorCompiler
    {
        // ---------- 编译中间数据存储（语义模型核心） ----------
        /// <summary>语法指令处理器映射：Key=指令名（忽略大小写），Value=对应的解析方法</summary>
        private Dictionary<string, ICommandHandler> _commandHandlers;
        private Context context = new Context();
        private Parser parser = null;

        public List<string> Warnings
        {
            get
            {
                List<string> warnings = [.. context.Warnings, .. parser.WarningList];
                return warnings;
            }
        }

        public List<string> Errors
        {
            get { return parser.ErrorList; }
        }

        public MetorCompiler()
        {
            _commandHandlers = CommandUtils.GetCommandList();
        }

        public ParsedData GetParsedData()
        {
            ParsedData parsedData = context.GetParsedData();
            parsedData.Warnings = Warnings;
            return parsedData;
        }

        public JsonRoot ExportJson()
        {
            JsonExport jsonRoot = new JsonExport(context);
            return jsonRoot.ExportJson();
        }

        // ====================================================================
        // 命令分发入口
        // 根据当前 Token 查找对应的处理函数并执行
        // ====================================================================
        private void ParseCommand()
        {
            var commandToken = parser.ParseId();
            if (commandToken == "END") return;
            string command = commandToken.Trim();
            if (_commandHandlers.TryGetValue(command, out var handler))
            {
                handler.Handle(parser, context);
            }
            else
                throw new SyntaxException($"未知命令 '{command}'", new Token(TokenType.Id, commandToken, parser.GetCurrentLine()));
        }

        // ====================================================================
        // 主解析入口：将源代码文本解析为中间数据结构
        // 执行词法分析、语法分析和语义分析，构建编译中间态
        // ====================================================================
        public void Parse(string sourceText)
        {
            // 词法分析：将文本转换为 Token 流
            parser = Lexer.Tokenize(sourceText);
            parser.ErrorList.Clear();

            const int maxErrorCount = 49;   // 错误数量上限，防止过多错误导致性能问题

            // 语法分析主循环
            while (parser.Position < parser.Length)
            {
                var currentToken = parser.SkipPeek();
                if (currentToken.TokenType == TokenType.End) return;

                try
                {
                    // 解析单个命令
                    ParseCommand();
                }
                catch (SyntaxException syntaxException)
                {
                    parser.ErrorList.Add(syntaxException.Message);

                    if (parser.ErrorCount >= maxErrorCount)
                        break;    // 错误过多，停止解析
                    parser.SynchronizeToNextCommand();
                }
                catch (Exception generalException)
                {
                    parser.ErrorList.Add($"第{currentToken.Line}行: {generalException.Message}");

                    if (parser.ErrorCount >= maxErrorCount)
                        break;
                    parser.SynchronizeToNextCommand();
                }
            }

            // 语义分析和后处理（仅在错误较少时执行）
            if (parser.ErrorCount < maxErrorCount)
            {
                PostProcessor processing = new PostProcessor(parser, context);
                processing.DoProcess();
            }

            // 如果存在错误，抛出聚合异常
            if (parser.ErrorCount > 0)
                throw new AggregateException("解析发现以下错误:\n" + string.Join("\n", parser.ErrorList));
        }
    }
}