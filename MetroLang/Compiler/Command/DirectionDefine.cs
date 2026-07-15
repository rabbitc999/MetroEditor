namespace MetroEditor.MetroLang.Compiler.Command;

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