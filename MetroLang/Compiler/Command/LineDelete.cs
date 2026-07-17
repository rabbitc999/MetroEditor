using MetroEditor.MetroLang.Compiler;

internal class LineDelete : ICommandHandler
{
    public void Handle(Parser parser, Context context)
    {
        var patterns = new List<string>();
        while (parser.Peek().TokenType == TokenType.Id)
            patterns.Add(parser.ParseId());

        if (patterns.Count == 0)
            throw new SyntaxException("至少指定一个线路ID或通配符模式", parser.Peek());

        var toDelete = new HashSet<string>();
        foreach (var pattern in patterns)
        {
            if (pattern.Contains('*'))
            {
                foreach (var id in context.Lines.Keys)
                    if (Utils.MatchWildcard(pattern, id).matches)
                        toDelete.Add(id);
            }
            else
            {
                if (context.Lines.ContainsKey(pattern))
                    toDelete.Add(pattern);
                else
                    parser.AddWarning($"线路 '{pattern}' 不存在，忽略");
            }
        }

        foreach (var id in toDelete)
            Utils.DeleteLine(context, id);
    }
}