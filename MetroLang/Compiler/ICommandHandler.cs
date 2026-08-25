namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 命令处理器接口，定义了 MetroLang 源码中每个顶级指令的解析契约。
    /// <para>每个实现类负责解析一种特定的语法指令（如 <c>LINE_DEFINE</c>、<c>STATION_DEFINE</c> 等），
    /// 从 <see cref="Parser"/> 消费 Token，并将解析结果写入 <see cref="Context"/>。</para>
    /// <para>所有命令处理器在 <see cref="Compiler.CommandUtils.GetCommandList"/> 中注册，
    /// 编译器通过指令名查找并分发到对应处理器。</para>
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>
        /// 解析当前指令，并更新编译上下文。
        /// </summary>
        /// <param name="parser">提供 Token 消费、预读等能力的语法解析器</param>
        /// <param name="context">编译数据上下文，用于存储解析结果</param>
        /// <remarks>
        /// 实现方应从 <paramref name="parser"/> 当前位置开始消费 Token，
        /// 解析指令参数并将结果写入 <paramref name="context"/>。
        /// 若遇到语法错误，应抛出 <see cref="SyntaxException"/>。
        /// </remarks>
        void Handle(Parser parser, Context context);
    }
}
