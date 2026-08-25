<<<<<<< HEAD
﻿namespace MetroEditor.MetroLang.Compiler.Command;

/// <summary>
/// 定义一个方向，并通过统一方向解析器读取其线路、站点和属性。
/// </summary>
internal class DirectionDefine : ICommandHandler
{
    /// <summary>解析方向 ID，生成方向模型后注册到上下文。</summary>
    public void Handle(Parser parser, Context context)
    {
        // 读取方向ID
        string dirId = parser.ParseId();

        // 委托给统一解析器
        var dirInfo = DirectionParser.Parse(parser, context, dirId, explicitLineId: null);

        // 存入上下文
        context.Directions[dirId] = dirInfo;
    }
}
=======
﻿namespace MetroEditor.MetroLang.Compiler.Command;

internal class DirectionDefine : ICommandHandler
{
    public void Handle(Parser parser, Context context)
    {
        // 读取方向ID
        string dirId = parser.ParseId();

        // 委托给统一解析器
        var dirInfo = DirectionParser.Parse(parser, context, dirId, explicitLineId: null);

        // 存入上下文
        context.Directions[dirId] = dirInfo;
    }
}
>>>>>>> 68704bec0197aa2f176e12f57e1de8fb0a964814
