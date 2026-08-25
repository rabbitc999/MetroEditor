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
            if (parser.IsPeekId("MESSAGE"))
            {
                parser.ParseId();
                assertion.Message = parser.ParseString();
            }
            context.Assertions.Add(assertion);
        }

        private Assertion ParseExpression(Parser parser) => ParseOr(parser);

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

            string pred = parser.ParseId().ToUpperInvariant();
            var assertion = new Assertion();

            // ----- 解析参数列表 -----
            parser.ParseSymbol("(");
            var args = new List<string>();
            while (true)
            {
                if (parser.Peek().TokenType != TokenType.String)
                    throw new SyntaxException($"期望字符串参数，但遇到了 '{parser.Peek().Value}'", parser.Peek());
                args.Add(parser.ParseString());

                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == ",")
                    parser.ParseSymbol(",");
                else if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == ")")
                    break;
                else
                    throw new SyntaxException($"期望逗号或右括号，但遇到了 '{parser.Peek().Value}'", parser.Peek());
            }
            parser.ParseSymbol(")");

            // ----- 根据谓词类型填充断言 -----
            switch (pred)
            {
                case "DIRECTION_EXISTS":
                    assertion.Predicate = PredicateType.DirectionExists;
                    assertion.Arguments.Add(args[0]);
                    break;
                case "LINE_EXISTS":
                    assertion.Predicate = PredicateType.LineExists;
                    assertion.Arguments.Add(args[0]);
                    break;
                case "STATION_EXISTS":
                    assertion.Predicate = PredicateType.StationExists;
                    assertion.Arguments.Add(args[0]);
                    break;
                case "STATION_COUNT":
                    assertion.Predicate = PredicateType.StationCount;
                    assertion.Arguments.Add(args[0]);
                    break;
                case "TRANSFER_EXISTS":
                    assertion.Predicate = PredicateType.TransferExists;
                    assertion.Arguments.Add(args[0]);
                    assertion.Arguments.Add(args[1]);
                    assertion.Arguments.Add(args[2]);
                    break;
                case "DISTANCE_BETWEEN":
                    assertion.Predicate = PredicateType.DistanceBetween;
                    assertion.Arguments.Add(args[0]);
                    assertion.Arguments.Add(args[1]);
                    break;
                case "DIRECTION_ATTR":
                    assertion.Predicate = PredicateType.DirectionAttr;
                    assertion.Arguments.Add(args[0]);
                    assertion.Arguments.Add(args[1]);
                    break;
                default:
                    throw new SyntaxException($"未知谓词 '{pred}'", parser.Peek());
            }

            // ----- 检查比较操作符 -----
            if (parser.Peek().TokenType == TokenType.Symbol)
            {
                string op = parser.Peek().Value;
                CompareOp compareOp = CompareOp.None;
                switch (op)
                {
                    case "<": compareOp = CompareOp.LessThan; break;
                    case ">": compareOp = CompareOp.GreaterThan; break;
                    case "<=": compareOp = CompareOp.LessOrEqual; break;
                    case ">=": compareOp = CompareOp.GreaterOrEqual; break;
                    case "==": compareOp = CompareOp.Equal; break;
                    case "!=": compareOp = CompareOp.NotEqual; break;
                    default: return assertion;
                }
                parser.ParseSymbol(op);
                double value = parser.ParseDouble();
                assertion.Comparison = compareOp;
                assertion.CompareValue = value;
            }

            return assertion;
        }
    }
}