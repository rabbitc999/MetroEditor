using MetroEditor.MetroLang.Compiler;
using System.Collections.Generic;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>
    /// 处理 LINE_MERGE 指令，将多个线路合并为目标线路。
    /// </summary>
    internal class LineMerge : ICommandHandler
    {
        /// <summary>
        /// 解析源线路、目标线路及 KEEP_ORIGINAL 选项并执行合并。
        /// </summary>
        /// <param name="parser">当前源文件解析器</param>
        /// <param name="context">编译中的路网数据上下文</param>
        public void Handle(Parser parser, Context context)
        {
            var sourceIds = new List<string>();
            while (parser.Peek().TokenType == TokenType.Id && !parser.IsPeekId("INTO"))
                sourceIds.Add(parser.ParseId());

            if (sourceIds.Count < 2)
                throw new SyntaxException("LINE_MERGE 至少需要两条源线路", parser.Peek());

            parser.ParseId("INTO");
            string targetId = parser.ParseId();

            bool keep = false;
            if (parser.IsPeekId("KEEP_ORIGINAL"))
            {
                parser.ParseId();
                keep = true;
            }

            Utils.MergeLines(context, sourceIds, targetId, keep);
        }
    }
}