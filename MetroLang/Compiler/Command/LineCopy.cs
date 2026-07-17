using MetroEditor.MetroLang.Compiler;

internal class LineCopy : ICommandHandler
{
    public void Handle(Parser parser, Context context)
    {
        string sourcePattern = parser.ParseId();
        string targetPattern = parser.ParseId();

        if (!sourcePattern.Contains('*') && !targetPattern.Contains('*'))
        {
            Utils.CopyLine(context, sourcePattern, targetPattern);
            return;
        }

        if (!sourcePattern.Contains('*') || !targetPattern.Contains('*'))
            throw new SyntaxException("源和目标模式必须同时包含 '*'", parser.Peek());

        var matched = context.Lines.Keys
            .Select(id => (id, Utils.MatchWildcard(sourcePattern, id)))
            .Where(x => x.Item2.matches)
            .ToList();

        if (!matched.Any())
            parser.AddWarning($"没有线路匹配模式 '{sourcePattern}'");

        foreach (var (source, (_, capture)) in matched)
        {
            string target = Utils.ApplyWildcard(targetPattern, capture);
            if (context.Lines.ContainsKey(target))
                parser.AddWarning($"目标线路 '{target}' 已存在，跳过");
            else
                Utils.CopyLine(context, source, target);
        }
    }
}