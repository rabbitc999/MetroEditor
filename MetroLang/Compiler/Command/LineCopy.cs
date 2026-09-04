using MetroEditor.MetroLang.Compiler;

internal class LineCopy : ICommandHandler
{
    /// <summary>
    /// 复制线路，支持精确 ID 或成对的通配符模式。
    /// </summary>
    public void Handle(Parser parser, Context context)
    {
        string sourcePattern = parser.ParseId();
        string targetPattern = parser.ParseId();

        if (!sourcePattern.Contains('*') && !targetPattern.Contains('*'))
        {
            // 精确复制直接交给工具层，保留线路下所有方向和属性。
            Utils.CopyLine(context, sourcePattern, targetPattern);
            return;
        }

        if (!sourcePattern.Contains('*') || !targetPattern.Contains('*'))
            throw new SyntaxException("源和目标模式必须同时包含 '*'", parser.Peek());

        var matched = context.Lines.Keys
            // 记录通配符捕获内容，后续用于生成对应目标 ID。
            .Select(id => (id, Utils.MatchWildcard(sourcePattern, id)))
            .Where(x => x.Item2.matches)
            .ToList();

        if (!matched.Any())
            parser.AddWarning($"没有线路匹配模式 '{sourcePattern}'");

        foreach (var (source, (_, capture)) in matched)
        {
            // 将源 ID 的捕获片段套入目标模式，逐条创建新线路。
            string target = Utils.ApplyWildcard(targetPattern, capture);
            if (context.Lines.ContainsKey(target))
                parser.AddWarning($"目标线路 '{target}' 已存在，跳过");
            else
                Utils.CopyLine(context, source, target);
        }
    }
}
