namespace MetroEditor.MetroLang.Compiler
{
    public enum PredicateType
    {
        DirectionExists,
        LineExists,
        StationExists,
        StationCount,
        TransferExists,
        DistanceBetween,
        DirectionAttr
    }

    public enum CompareOp
    {
        None,   // 用于布尔谓词（直接返回真/假）
        Equal,
        NotEqual,
        LessThan,
        LessOrEqual,
        GreaterThan,
        GreaterOrEqual
    }

    public class Assertion
    {
        public PredicateType Predicate { get; set; }
        public List<object> Arguments { get; set; } = new(); // 参数（字符串或数字）
        public CompareOp Comparison { get; set; } = CompareOp.None;
        public double? CompareValue { get; set; }
        public string? Message { get; set; }
        public Assertion? Left { get; set; }   // 用于逻辑运算
        public Assertion? Right { get; set; }
        public string? Operator { get; set; }  // "AND", "OR", "NOT"
    }
}