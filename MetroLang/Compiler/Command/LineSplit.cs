using MetroEditor.MetroLang.Compiler;
using System.Collections.Generic;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class LineSplit : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string sourceId = parser.ParseId();
            if (!context.Lines.ContainsKey(sourceId))
                throw new SyntaxException($"线路 '{sourceId}' 未定义", parser.Peek());

            parser.ParseId("INTO");
            var targetIds = new List<string>();
            while (parser.Peek().TokenType == TokenType.Id && !parser.IsPeekId("KEEP_ORIGINAL") && !parser.IsPeekId("DIRECTIONS") && !parser.IsPeekId("REMAINING"))
                targetIds.Add(parser.ParseId());

            if (targetIds.Count < 2)
                throw new SyntaxException("至少拆分为2条目标线路", parser.Peek());

            var dirMapping = new Dictionary<string, string>();
            string remainingTarget = null;
            bool keep = false;

            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) break;

                string sub = parser.ParseId().ToUpperInvariant();
                if (sub == "DIRECTIONS")
                {
                    string pattern = parser.ParseId(); // 支持通配符
                    parser.ParseId("TO");
                    string target = parser.ParseId();
                    if (!targetIds.Contains(target))
                        throw new SyntaxException($"目标线路 '{target}' 不在列表", parser.Peek());

                    // 匹配方向
                    foreach (var dirId in context.Directions.Keys)
                    {
                        if (dirId.StartsWith(pattern.Replace("*", "")) && dirId.EndsWith(pattern.Contains("*") ? "" : pattern))
                        {
                            if (dirMapping.ContainsKey(dirId))
                                throw new SyntaxException($"方向 '{dirId}' 被多次分配", parser.Peek());
                            if (context.Directions[dirId].LineId != sourceId)
                                continue; // 只处理源线路的方向
                            dirMapping[dirId] = target;
                        }
                    }
                }
                else if (sub == "REMAINING")
                {
                    parser.ParseId("TO");
                    remainingTarget = parser.ParseId();
                    if (!targetIds.Contains(remainingTarget))
                        throw new SyntaxException($"REMAINING 目标 '{remainingTarget}' 不在列表中", parser.Peek());
                }
                else if (sub == "KEEP_ORIGINAL")
                {
                    keep = true;
                }
                else
                {
                    throw new SyntaxException($"未知子命令 '{sub}'，允许 DIRECTIONS, REMAINING, KEEP_ORIGINAL", parser.Peek());
                }
            }

            // 执行拆分
            Utils.SplitLine(context, sourceId, targetIds, dirMapping, remainingTarget, keep);
        }
    }
}