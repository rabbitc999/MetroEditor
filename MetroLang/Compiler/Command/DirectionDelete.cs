using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>处理 DIRECTION_DELETE 指令，按 ID 或通配符删除方向及其派生规则。</summary>
    internal class DirectionDelete : ICommandHandler
    {
        /// <summary>解析待删除方向并清理距离、运营规则和 Via 引用。</summary>
        /// <param name="parser">当前源文件解析器</param><param name="context">编译数据上下文</param>
        public void Handle(Parser parser, Context context)
        {
            var patterns = new List<string>();
            while (parser.Peek().TokenType == TokenType.Id)
                patterns.Add(parser.ParseId());

            if (patterns.Count == 0)
                throw new SyntaxException("DIRECTION_DELETE 需要至少一个方向 ID 或通配符模式", parser.Peek());

            var toDelete = new List<string>();
            foreach (var pattern in patterns)
            {
                if (pattern.Contains('*'))
                {
                    // 通配符匹配
                    foreach (var id in context.Directions.Keys)
                    {
                        if (Utils.MatchWildcard(pattern, id).matches)
                            toDelete.Add(id);
                    }
                }
                else
                {
                    // 精确匹配
                    if (context.Directions.ContainsKey(pattern))
                        toDelete.Add(pattern);
                    else
                        parser.AddWarning($"方向 '{pattern}' 不存在，忽略删除");
                }
            }

            // 去重
            toDelete = toDelete.Distinct().ToList();

            // 执行删除（清理规则等，复用之前的清理逻辑）
            foreach (var id in toDelete)
            {
                string externalKey = Utils.ConvertToExternalDirKey(context, id);
                context.CrowdRules.RemoveAll(r => r.DirectionKey == externalKey);
                context.SpeedLimits.RemoveAll(r => r.DirectionKey == externalKey);
                context.Depots.RemoveAll(d => d.DirectionKey == externalKey);
                context.Directions.Remove(id);
                context.Distances.Remove(id);
            }

            // 清理 ViaDirections 引用
            foreach (var dir in context.Directions.Values)
                dir.ViaDirections.RemoveAll(v => toDelete.Contains(v));
        }
    }
}