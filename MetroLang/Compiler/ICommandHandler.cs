namespace MetroEditor.MetroLang.Compiler
{
    public interface ICommandHandler
    {
        /// <summary>解析当前指令，并更新上下文</summary>
        /// <param name="parser">提供 Token 消费、预读等能力</param>
        /// <param name="context">编译上下文</param>
        void Handle(Parser parser, Context context);
    }
}
