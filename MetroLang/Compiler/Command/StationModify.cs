using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class StationModify : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string stationName = parser.ParseString();
            if (!context.Stations.TryGetValue(stationName, out var coord))
                throw new SyntaxException($"车站 '{stationName}' 未定义", parser.Peek());

            // 临时变量，用于修改
            double? newLon = null;
            double? newLat = null;
            string newCity = null;
            List<string> otherNames = null;

            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) break;

                string subCmd = parser.ParseId().ToUpperInvariant();
                switch (subCmd)
                {
                    case "SET":
                        string field = parser.ParseId().ToUpperInvariant();
                        switch (field)
                        {
                            case "COORD":
                                newLon = parser.ParseDouble();
                                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == ",")
                                    parser.ParseSymbol(",");
                                newLat = parser.ParseDouble();
                                break;
                            case "CITY":
                                newCity = parser.ParseString();
                                if (!context.Cities.ContainsKey(newCity))
                                    throw new SyntaxException($"城市 '{newCity}' 未定义", parser.Peek());
                                break;
                            case "OTHER_NAMES":
                                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                                    otherNames = parser.ParseStringList();
                                else
                                    otherNames = new List<string> { parser.ParseString() };
                                break;
                            default:
                                throw new SyntaxException($"未知字段 '{field}'，允许: COORD, CITY, OTHER_NAMES", parser.Peek());
                        }
                        break;

                    default:
                        throw new SyntaxException($"未知子命令 '{subCmd}'，允许: SET, ADD, REMOVE", parser.Peek());
                }
            }

            // 应用修改
            if (newLon.HasValue && newLat.HasValue)
                context.Stations[stationName] = (newLon.Value, newLat.Value);
            if (!string.IsNullOrEmpty(newCity))
                context.StationCity[stationName] = newCity;
            if (otherNames != null)
                context.StationOtherNames[stationName] = otherNames;
        }
    }
}