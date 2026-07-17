using MetroEditor.MetroLang.Compiler;
using System.Collections.Generic;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class LineMerge : ICommandHandler
    {
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