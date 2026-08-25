<<<<<<< HEAD
﻿using MetroEditor.MetroLang.Compiler;

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
=======
﻿using MetroEditor.MetroLang.Compiler;

/// <summary>
/// 处理 LINE_COPY 指令，支持按线路 ID 或通配符复制线路定义。
/// </summary>
internal class LineCopy : ICommandHandler
{
    /// <summary>
    /// 读取源/目标线路模式并将匹配的线路复制到编译上下文。
    /// </summary>
    /// <param name="parser">当前源文件解析器</param>
    /// <param name="context">编译中的路网数据上下文</param>
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
>>>>>>> 68704bec0197aa2f176e12f57e1de8fb0a964814
