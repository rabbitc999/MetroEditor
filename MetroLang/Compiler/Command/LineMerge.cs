using MetroEditor.MetroLang.Compiler;
using System.Collections.Generic;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>
    /// 将多条线路合并为一条目标线路，可选择保留源线路。
    /// </summary>
    internal class LineMerge : ICommandHandler
    {
        /// <summary>
        /// 读取源线路列表、目标线路和可选的 <c>KEEP_ORIGINAL</c> 标志。
        /// </summary>
        public void Handle(Parser parser, Context context)
        {
            var sourceIds = new List<string>();
            // 源线路以连续 ID 表示，遇到 INTO 后切换到目标线路解析。
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

            // 由工具层统一处理方向、站点和线路元数据的合并规则。
            Utils.MergeLines(context, sourceIds, targetId, keep);
        }
    }
}
