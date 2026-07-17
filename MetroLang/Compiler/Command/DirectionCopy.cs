using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class DirectionCopy : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string sourcePattern = parser.ParseId();
            string targetPattern = parser.ParseId();

            if (!sourcePattern.Contains('*') && !targetPattern.Contains('*'))
            {
                // 无通配符，旧行为
                CopySingleDirection(sourcePattern, targetPattern, context);
                return;
            }

            if (!sourcePattern.Contains('*') || !targetPattern.Contains('*'))
                throw new SyntaxException("源模式和目标模式必须同时包含 '*' 或同时不包含", parser.Peek());

            // 通配符模式
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
                    parser.AddWarning($"目标方向 '{target}' 已存在，跳过复制");
                    continue;
                }
                CopySingleDirection(source, target, context);
            }
        }

        private void CopySingleDirection(string sourceId, string targetId, Context context)
        {
            // 复制方向和距离
            var sourceDir = context.Directions[sourceId];
            var newDir = new DirectionInfo
            {
                LineId = sourceDir.LineId,
                Stations = new List<string>(sourceDir.Stations),
                Attrs = new HashSet<string>(sourceDir.Attrs),
                Alias = sourceDir.Alias,
                AliasColor = sourceDir.AliasColor,
                DirectionType = sourceDir.DirectionType,
                IsFuture = sourceDir.IsFuture,
                ConnectStation = sourceDir.ConnectStation,
                ViaDirections = new List<string>(sourceDir.ViaDirections)
            };
            context.Directions[targetId] = newDir;

            if (context.Distances.TryGetValue(sourceId, out var dist))
                context.Distances[targetId] = new List<double>(dist);
        }
    }
}