using MetroEditor.MetroLang.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // STATION_STATUS 解析：车站状态定义
    // 定义车站在特定线路上的运营状态
    // 语法示例：STATION_STATUS ["车站A", "车站B"] LINE1 pending
    // ====================================================================
    internal class StationStatus : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 解析车站名称列表（支持单个或数组）
            List<string> stationNames;
            if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                stationNames = parser.ParseStringList();
            else
                stationNames = new List<string> { parser.ParseString() };

            // 可选的线路ID（空字符串表示所有线路）
            string lineId = null;
            if (parser.Peek().TokenType == TokenType.Id && !IsStatusKeyword(parser.Peek().Value))
                lineId = parser.ParseId();

            // 解析状态值
            string status = parser.ParseId().ToLowerInvariant();
            if (!new[] { "operating", "pending", "out_station" }.Contains(status))
                throw new SyntaxException($"无效状态 '{status}'", parser.Peek());

            // 为每个车站设置状态
            foreach (var stationName in stationNames)
            {
                if (!context.Stations.ContainsKey(stationName))
                    throw new SyntaxException($"站台 '{stationName}' 未定义", parser.Peek());
                if (!string.IsNullOrEmpty(lineId) && !context.Lines.ContainsKey(lineId))
                    throw new SyntaxException($"线路 '{lineId}' 未定义", parser.Peek());

                context.StationStatus[(stationName, lineId ?? "")] = status;
            }
        }

        // ====================================================================
        // 状态关键字判断：检查给定单词是否为有效的状态关键字
        // ====================================================================
        private bool IsStatusKeyword(string word)
        {
            var upperWord = word.ToUpperInvariant();
            return upperWord == "OPERATING" || upperWord == "PENDING" || upperWord == "OUT_STATION";
        }
    }
}
