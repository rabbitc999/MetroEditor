namespace MetroEditor.MetroLang.Compiler
{

    /// <summary>
    /// Token 的类型枚举。
    /// </summary>
    public enum TokenType
    {
        /// <summary>
        /// 换行符（LF / CRLF），用于行号维护与语句分隔。
        /// </summary>
        Newline,

        /// <summary>
        /// 数值字面量（整数或浮点数）。
        /// </summary>
        Number,

        /// <summary>
        /// 字符串字面量（单引号或双引号包裹）。
        /// </summary>
        String,

        /// <summary>
        /// 标识符：命令名、变量名、线路 ID、方向 ID 等。
        /// </summary>
        Id,

        /// <summary>
        /// 符号：标点符号，如 [] () {} : , = 等。
        /// </summary>
        Symbol,

        /// <summary>
        /// 无法识别的非法字符，用于错误恢复与提示。
        /// </summary>
        Mismatch,

        /// <summary>
        /// 文件结束标记（EOF），标识 Token 流结束。
        /// </summary>
        End
    }
}
