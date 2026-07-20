using MetroEditor.MetroLang.Compiler.Compiler;
using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.Exporter;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.Simulator.Algorithms;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 将.metro 源文件编译为标准 JSON 路网数据。
    /// </summary>
    public partial class MetroCompiler
    {
        // ---------- 编译中间数据存储（语义模型核心） ----------
        /// <summary>语法指令处理器映射：Key=指令名（忽略大小写），Value=对应的解析方法</summary>
        private List<string> _defaultLanguageOrder = new List<string>();
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

        public MetroCompiler()
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
            Context _context = context.DeepClone();
            JsonExporter jsonRoot = new JsonExporter(_context);
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
        public void Parse(string sourceText, string baseDirectory)
        {
            // 1. 词法分析
            var rawParser = Lexer.Tokenize(sourceText);
            var rawTokens = rawParser.Tokens;

            // 2. 预处理（过滤条件编译指令）
            var processedTokens = Preprocessor.Process(rawTokens, baseDirectory, context, out var preprocessorErrors);

            // 3. 重新初始化解析器（使用过滤后的 Token 流）
            parser = new Parser(processedTokens);
            parser.ErrorList.Clear();
            if (preprocessorErrors.Count > 0)
            {
                parser.ErrorList.AddRange(preprocessorErrors);
                // 如果预处理有致命错误，可以在这里选择停止
            }

            // 4. 语法分析
            while (parser.Position < parser.Length)
            {
                var currentToken = parser.SkipPeek();
                if (currentToken.TokenType == TokenType.End) break;

                try
                {
                    ParseCommand();
                }
                catch (SyntaxException ex)
                {
                    parser.ErrorList.Add(ex.Message);
                    if (parser.ErrorCount >= 49) break;
                    parser.SynchronizeToNextCommand();
                }
                catch (Exception ex)
                {
                    parser.ErrorList.Add($"第{currentToken.Line}行: {ex.Message}");
                    if (parser.ErrorCount >= 49) break;
                    parser.SynchronizeToNextCommand();
                }
            }

            // 5. 在解析完成后，如果 context.LanguageOrder 仍为空，则应用默认值
            if (context.LanguageOrder.Count == 0 && _defaultLanguageOrder.Count > 0)
            {
                context.LanguageOrder = new List<string>(_defaultLanguageOrder);
            }

            // 6. 后处理（与之前相同）
            if (parser.ErrorCount < 49)
            {
                PostProcessor processor = new PostProcessor(parser, context);
                processor.DoProcess();
            }

            if (parser.ErrorCount > 0)
                throw new AggregateException("解析发现以下错误:\n" + string.Join("\n", parser.ErrorList));
        }
    }
}