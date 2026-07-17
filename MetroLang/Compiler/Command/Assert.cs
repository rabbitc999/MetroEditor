using MetroEditor.MetroLang.Compiler;
using System;
using System.Collections.Generic;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class Assert : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            var assertion = ParseExpression(parser);
            // 可选的 MESSAGE
            if (parser.IsPeekId("MESSAGE"))
            {
                parser.ParseId();
                assertion.Message = parser.ParseString();
            }
            context.Assertions.Add(assertion);
        }

        private Assertion ParseExpression(Parser parser)
        {
            // 实现递归下降解析 AND/OR/NOT
            return ParseOr(parser);
        }

        private Assertion ParseOr(Parser parser)
        {
            var left = ParseAnd(parser);
            while (parser.IsPeekId("OR"))
            {
                parser.ParseId();
                var right = ParseAnd(parser);
                left = new Assertion { Left = left, Operator = "OR", Right = right };
            }
            return left;
        }

        private Assertion ParseAnd(Parser parser)
        {
            var left = ParseNot(parser);
            while (parser.IsPeekId("AND"))
            {
                parser.ParseId();
                var right = ParseNot(parser);
                left = new Assertion { Left = left, Operator = "AND", Right = right };
            }
            return left;
        }

        private Assertion ParseNot(Parser parser)
        {
            if (parser.IsPeekId("NOT"))
            {
                parser.ParseId();
                var inner = ParsePrimary(parser);
                return new Assertion { Operator = "NOT", Left = inner };
            }
            return ParsePrimary(parser);
        }

        private Assertion ParsePrimary(Parser parser)
        {
            if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "(")
            {
                parser.ParseSymbol("(");
                var expr = ParseExpression(parser);
                parser.ParseSymbol(")");
                return expr;
            }

            // 谓词解析
            string pred = parser.ParseId().ToUpperInvariant();
            Assertion assertion = new Assertion();

            switch (pred)
            {
                case "DIRECTION_EXISTS":
                    parser.ParseSymbol("(");
                    assertion.Predicate = PredicateType.DirectionExists;
                    assertion.Arguments.Add(parser.ParseString());
                    parser.ParseSymbol(")");
                    break;
                case "LINE_EXISTS":
                    parser.ParseSymbol("(");
                    assertion.Predicate = PredicateType.LineExists;
                    assertion.Arguments.Add(parser.ParseString());
                    parser.ParseSymbol(")");
                    break;
                case "STATION_EXISTS":
                    parser.ParseSymbol("(");
                    assertion.Predicate = PredicateType.StationExists;
                    assertion.Arguments.Add(parser.ParseString());
                    parser.ParseSymbol(")");
                    break;
                case "STATION_COUNT":
                    parser.ParseSymbol("(");
                    assertion.Predicate = PredicateType.StationCount;
                    assertion.Arguments.Add(parser.ParseString()); // 线路ID
                    parser.ParseSymbol(")");
                    break;
                case "TRANSFER_EXISTS":
                    parser.ParseSymbol("(");
                    assertion.Predicate = PredicateType.TransferExists;
                    assertion.Arguments.Add(parser.ParseString()); // 站名
                    assertion.Arguments.Add(parser.ParseString()); // 线路A
                    assertion.Arguments.Add(parser.ParseString()); // 线路B
                    parser.ParseSymbol(")");
                    break;
                case "DISTANCE_BETWEEN":
                    parser.ParseSymbol("(");
                    assertion.Predicate = PredicateType.DistanceBetween;
                    assertion.Arguments.Add(parser.ParseString()); // 站A
                    assertion.Arguments.Add(parser.ParseString()); // 站B
                    parser.ParseSymbol(")");
                    break;
                case "DIRECTION_ATTR":
                    parser.ParseSymbol("(");
                    assertion.Predicate = PredicateType.DirectionAttr;
                    assertion.Arguments.Add(parser.ParseString()); // 方向ID
                    assertion.Arguments.Add(parser.ParseString()); // 属性名（如 "BRANCH"）
                    parser.ParseSymbol(")");
                    break;
                default:
                    throw new SyntaxException($"未知谓词 '{pred}'", parser.Peek());
            }

            // 检查是否有比较操作符
            if (parser.Peek().TokenType == TokenType.Symbol)
            {
                string op = parser.Peek().Value;
                CompareOp compareOp = CompareOp.None;
                if (op == "<") compareOp = CompareOp.LessThan;
                else if (op == ">") compareOp = CompareOp.GreaterThan;
                else if (op == "<=") compareOp = CompareOp.LessOrEqual;
                else if (op == ">=") compareOp = CompareOp.GreaterOrEqual;
                else if (op == "==") compareOp = CompareOp.Equal;
                else if (op == "!=") compareOp = CompareOp.NotEqual;
                else return assertion;

                parser.ParseSymbol(op);
                double value = parser.ParseDouble();
                assertion.Comparison = compareOp;
                assertion.CompareValue = value;
            }

            return assertion;
        }
    }
}