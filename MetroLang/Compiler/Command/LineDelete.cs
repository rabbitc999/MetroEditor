<<<<<<< HEAD
﻿using MetroEditor.MetroLang.Compiler;

internal class LineDelete : ICommandHandler
{
    /// <summary>
    /// 按线路 ID 或通配符批量删除线路。
    /// </summary>
    public void Handle(Parser parser, Context context)
    {
        var patterns = new List<string>();
        // 先收集全部模式，保证同一条命令中的匹配结果一次性计算。
        while (parser.Peek().TokenType == TokenType.Id)
            patterns.Add(parser.ParseId());

        if (patterns.Count == 0)
            throw new SyntaxException("至少指定一个线路ID或通配符模式", parser.Peek());

        var toDelete = new HashSet<string>();
        foreach (var pattern in patterns)
        {
            // 带星号的模式需要遍历当前线路键；精确 ID 则直接检查字典。
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

        // 去重后的集合避免多个模式重复触发删除逻辑。
        foreach (var id in toDelete)
            Utils.DeleteLine(context, id);
    }
}
=======
﻿using MetroEditor.MetroLang.Compiler;

/// <summary>
/// 处理 LINE_DELETE 指令，按线路 ID 或通配符删除线路。
/// </summary>
internal class LineDelete : ICommandHandler
{
    /// <summary>
    /// 收集待删除线路并委托工具层执行关联数据清理。
    /// </summary>
    /// <param name="parser">当前源文件解析器</param>
    /// <param name="context">编译中的路网数据上下文</param>
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
>>>>>>> 68704bec0197aa2f176e12f57e1de8fb0a964814
