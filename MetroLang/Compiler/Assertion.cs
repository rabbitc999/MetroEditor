namespace MetroEditor.MetroLang.Compiler
{
    /// <summary>
    /// 编译时断言谓词类型枚举。
    /// 用于 <c>ASSERT</c> 指令中指定要检查的条件类别。
    /// </summary>
    public enum PredicateType
    {
        /// <summary>检查方向是否存在</summary>
        DirectionExists,
        /// <summary>检查线路是否存在</summary>
        LineExists,
        /// <summary>检查车站是否存在</summary>
        StationExists,
        /// <summary>统计指定线路上的车站数量</summary>
        StationCount,
        /// <summary>检查两线路间是否存在换乘关系</summary>
        TransferExists,
        /// <summary>计算两站之间的地理距离（米）</summary>
        DistanceBetween,
        /// <summary>检查方向是否具有指定属性</summary>
        DirectionAttr
    }

    /// <summary>
    /// 断言比较运算符枚举。
    /// 用于将谓词返回值与期望值进行比较。
    /// </summary>
    public enum CompareOp
    {
        /// <summary>不进行比较，谓词直接返回布尔值</summary>
        None,
        /// <summary>等于 (==)</summary>
        Equal,
        /// <summary>不等于 (!=)</summary>
        NotEqual,
        /// <summary>小于 (&lt;)</summary>
        LessThan,
        /// <summary>小于等于 (&lt;=)</summary>
        LessOrEqual,
        /// <summary>大于 (&gt;)</summary>
        GreaterThan,
        /// <summary>大于等于 (&gt;=)</summary>
        GreaterOrEqual
    }

    /// <summary>
    /// 编译时断言定义。
    /// <para>支持布尔谓词（如 <see cref="PredicateType.LineExists"/>）和数值比较谓词（如 <see cref="PredicateType.StationCount"/>），
    /// 并可通过 <see cref="Operator"/> 组合多个断言（AND/OR/NOT）。</para>
    /// <para>断言在后处理阶段由 <see cref="PostProcessor.EvaluateAssertions"/> 统一执行，
    /// 失败时将错误信息添加到解析器错误列表中。</para>
    /// </summary>
    public class Assertion
    {
        /// <summary>断言谓词类型</summary>
        public PredicateType Predicate { get; set; }
        /// <summary>谓词参数列表（字符串或数字）</summary>
        public List<object> Arguments { get; set; } = new();
        /// <summary>比较运算符，默认为 <see cref="CompareOp.None"/></summary>
        public CompareOp Comparison { get; set; } = CompareOp.None;
        /// <summary>比较目标值（仅当 <see cref="Comparison"/> 不为 None 时有效）</summary>
        public double? CompareValue { get; set; }
        /// <summary>自定义断言失败消息，为 null 时使用默认格式</summary>
        public string? Message { get; set; }
        /// <summary>逻辑运算的左操作数（用于 AND/OR/NOT 组合）</summary>
        public Assertion? Left { get; set; }
        /// <summary>逻辑运算的右操作数（用于 AND/OR 组合）</summary>
        public Assertion? Right { get; set; }
        /// <summary>逻辑运算符：AND、OR 或 NOT</summary>
        public string? Operator { get; set; }
    }
}