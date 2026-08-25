<<<<<<< HEAD
﻿using MetroEditor.MetroLang.Compiler;

internal class LineRename : ICommandHandler
{
    /// <summary>
    /// 重命名线路，支持精确名称和通配符批量替换。
    /// </summary>
    public void Handle(Parser parser, Context context)
    {
        string sourcePattern = parser.ParseId();
        string targetPattern = parser.ParseId();

        if (!sourcePattern.Contains('*') && !targetPattern.Contains('*'))
        {
            // 精确重命名由工具方法处理所有引用关系。
            Utils.RenameLine(context, sourcePattern, targetPattern);
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
            // 目标已存在时跳过该项，避免覆盖另一条线路。
            string target = Utils.ApplyWildcard(targetPattern, capture);
            if (context.Lines.ContainsKey(target))
                parser.AddWarning($"目标线路 '{target}' 已存在，跳过");
            else
                Utils.RenameLine(context, source, target);
        }
    }
}
=======
﻿using MetroEditor.MetroLang.Compiler;

/// <summary>
/// 处理 LINE_RENAME 指令，支持按线路 ID 或通配符批量重命名线路。
/// </summary>
internal class LineRename : ICommandHandler
{
    /// <summary>
    /// 解析源/目标线路模式并更新上下文中的线路标识。
    /// </summary>
    /// <param name="parser">当前源文件解析器</param>
    /// <param name="context">编译中的路网数据上下文</param>
    public void Handle(Parser parser, Context context)
    {
        string sourcePattern = parser.ParseId();
        string targetPattern = parser.ParseId();

        if (!sourcePattern.Contains('*') && !targetPattern.Contains('*'))
        {
            Utils.RenameLine(context, sourcePattern, targetPattern);
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
                Utils.RenameLine(context, source, target);
        }
    }
}
>>>>>>> 68704bec0197aa2f176e12f57e1de8fb0a964814
