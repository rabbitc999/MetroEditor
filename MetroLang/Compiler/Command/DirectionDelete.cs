using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class DirectionDelete : ICommandHandler
    {
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