using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>处理 DIRECTION_RENAME 指令，并同步迁移方向距离索引。</summary>
    internal class DirectionRename : ICommandHandler
    {
        /// <summary>解析方向重命名模式并更新上下文中的方向标识。</summary>
        /// <param name="parser">当前源文件解析器</param><param name="context">编译数据上下文</param>
        public void Handle(Parser parser, Context context)
        {
            string sourcePattern = parser.ParseId();
            string targetPattern = parser.ParseId();

            if (!sourcePattern.Contains('*') && !targetPattern.Contains('*'))
            {
                // 精确重命名
                RenameSingleDirection(sourcePattern, targetPattern, parser, context);
                return;
            }

            if (!sourcePattern.Contains('*') || !targetPattern.Contains('*'))
                throw new SyntaxException("源模式和目标模式必须同时包含 '*' 或同时不包含", parser.Peek());

            var matched = new List<(string source, string capture)>();
            foreach (var id in context.Directions.Keys)
            {
                var (matches, capture) = Utils.MatchWildcard(sourcePattern, id);
                if (matches)
                    matched.Add((id, capture));
            }

            if (matched.Count == 0)
            {
                parser.AddWarning($"没有方向匹配模式 '{sourcePattern}'");
                return;
            }

            foreach (var (source, capture) in matched)
            {
                string target = Utils.ApplyWildcard(targetPattern, capture);
                if (context.Directions.ContainsKey(target))
                {
                    parser.AddWarning($"目标方向 '{target}' 已存在，跳过重命名");
                    continue;
                }
                RenameSingleDirection(source, target, parser, context);
            }
        }

        private void RenameSingleDirection(string sourceId, string targetId, Parser parser, Context context)
        {
            if (!context.Directions.ContainsKey(sourceId))
            {
                parser.AddWarning($"方向 '{sourceId}' 不存在，跳过重命名");
                return;
            }

            // 移动方向和距离
            var dir = context.Directions[sourceId];
            context.Directions.Remove(sourceId);
            context.Directions[targetId] = dir;

            if (context.Distances.TryGetValue(sourceId, out var dist))
            {
                context.Distances.Remove(sourceId);
                context.Distances[targetId] = dist;
            }
        }
    }
}