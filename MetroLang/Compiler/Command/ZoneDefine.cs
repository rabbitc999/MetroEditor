using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class ZoneDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 1. 解析区域ID
            string zoneId = parser.ParseId();
            if (context.Zones.ContainsKey(zoneId))
                parser.AddWarning($"区域 '{zoneId}' 已存在，将覆盖");

            var zone = new ZoneInfo();
            context.Zones[zoneId] = zone;

            // 2. 解析主体
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) break;

                string cmd = parser.ParseId().ToUpperInvariant();
                switch (cmd)
                {
                    case "NAME":
                        zone.Name = parser.ParseString();
                        break;

                    case "TYPE":
                        zone.Type = parser.ParseId().ToUpperInvariant();
                        break;

                    case "CITY":
                        zone.City = parser.ParseString();
                        break;

                    case "COLOR":
                        zone.Color = parser.ParseString();
                        break;

                    case "CENTER":
                        zone.Center = ParseCoordinate(parser);
                        break;

                    case "RADIUS":
                        zone.Radius = parser.ParseDouble();
                        break;

                    case "POLYGON":
                        zone.Polygon = ParsePolygon(parser);
                        break;

                    case "STATIONS":
                        zone.Stations = parser.ParseStringList();
                        break;

                    case "ATTRIBUTES":
                        ParseAttributes(parser, zone);
                        break;

                    default:
                        throw new SyntaxException($"ZONE_DEFINE 内未知子命令 '{cmd}'", parser.Peek());
                }
            }

            // 3. 后置检查（部分有效性在 PostProcessor 中做）
        }

        /// <summary>解析坐标对（经度, 纬度）</summary>
        private double[] ParseCoordinate(Parser parser)
        {
            double lon = parser.ParseDouble();
            if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == ",")
                parser.ParseSymbol(",");
            double lat = parser.ParseDouble();
            return new double[] { lon, lat };
        }

        /// <summary>解析多边形：[[lon,lat], [lon,lat], ...]</summary>
        private List<double[]> ParsePolygon(Parser parser)
        {
            var polygon = new List<double[]>();
            parser.ParseSymbol("[");
            while (true)
            {
                parser.ParseNewlines();
                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "]")
                {
                    parser.ParseSymbol("]");
                    break;
                }

                // 解析每个点 [lon, lat]
                parser.ParseSymbol("[");
                double lon = parser.ParseDouble();
                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == ",")
                    parser.ParseSymbol(",");
                double lat = parser.ParseDouble();
                parser.ParseSymbol("]");
                polygon.Add(new double[] { lon, lat });

                parser.ParseNewlines();
                var next = parser.Peek();
                if (next.TokenType == TokenType.Symbol && next.Value == "]")
                    continue;
                if (next.TokenType == TokenType.Symbol && next.Value == ",")
                    parser.ParseSymbol(",");
                else
                    throw new SyntaxException("多边形点之间缺少逗号或列表未闭合", next);
            }
            return polygon;
        }

        /// <summary>解析 ATTRIBUTES 块</summary>
        private void ParseAttributes(Parser parser, ZoneInfo zone)
        {
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsPeekId("END_ATTRIBUTES"))
                {
                    parser.ParseId();
                    break;
                }

                string attrCmd = parser.ParseId().ToUpperInvariant();
                switch (attrCmd)
                {
                    case "PEAK_HOURS":
                        zone.Attributes.PeakHours = parser.ParseStringList();
                        break;
                    case "POPULATION_DENSITY":
                        zone.Attributes.PopulationDensity = parser.ParseDouble();
                        break;
                    case "AVG_RENT":
                        zone.Attributes.AvgRent = parser.ParseDouble();
                        break;
                    default:
                        throw new SyntaxException($"ATTRIBUTES 内未知子命令 '{attrCmd}'", parser.Peek());
                }
            }
        }
    }
}
