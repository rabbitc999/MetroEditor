using MetroEditor.MetroLang.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // STATION_DEFINE 解析：车站坐标定义
    //   - 解析时直接存储 "城市::站名"（复合键）
    //   - 后处理根据冲突情况归一化为纯键或复合键
    //   - OTHER_NAME 紧随 NODE 出现，键为当前站点的复合键
    // ====================================================================
    internal class StationDefine : ICommandHandler
    {
        private string _currentCompositeKey = ""; // 当前 NODE 的复合键

        public void Handle(Parser parser, Context context)
        {
            // ----- 前处理：将已有键统一转为 "城市::站名"（兼容旧数据） -----
            PreprocessExistingKeys(context);

            // ----- 解析主体 -----
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) break;

                string subCommand = parser.ParseId().ToUpperInvariant();
                if (subCommand != "NODE")
                {
                    // 处理 OTHER_NAME（紧随 NODE 之后）
                    if (subCommand == "OTHER_NAME" && !string.IsNullOrEmpty(_currentCompositeKey))
                    {
                        parser.ParseId(); // 消耗 OTHER_NAME
                        List<string> nameList;
                        if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == "[")
                            nameList = parser.ParseStringList();
                        else
                            nameList = new List<string> { parser.ParseString() };

                        // 以当前复合键存储别名（后处理会跟随键迁移）
                        context.StationOtherNames[_currentCompositeKey] = nameList;
                        continue;
                    }
                    throw new SyntaxException($"STATION_DEFINE 内只能使用 NODE 或 OTHER_NAME，但遇到了 '{subCommand}'", parser.Peek());
                }

                // ----- 解析 NODE -----
                string stationName = parser.ParseString();
                parser.ParseId("COORD");
                double longitude = parser.ParseDouble();
                if (parser.Peek().TokenType == TokenType.Symbol && parser.Peek().Value == ",")
                    parser.ParseSymbol(",");
                double latitude = parser.ParseDouble();

                string city = context.MainCity;
                if (parser.IsPeekId("CITY"))
                {
                    parser.ParseId();
                    city = parser.ParseString();
                }

                // 构建复合键
                string compositeKey = $"{city}::{stationName}";
                _currentCompositeKey = compositeKey; // 供后续 OTHER_NAME 使用

                // 存储（覆盖时警告）
                if (context.Stations.ContainsKey(compositeKey))
                    parser.AddWarning($"站台 '{compositeKey}' 已存在，将覆盖");
                context.Stations[compositeKey] = (longitude, latitude);
                context.StationCity[compositeKey] = city;
            }

            // ----- 后处理归一化 -----
            NormalizeStationKeys(context);
        }

        /// <summary>
        /// 前处理：将现有所有键统一转为 "城市::站名"
        /// </summary>
        private void PreprocessExistingKeys(Context context)
        {
            var newStations = new Dictionary<string, (double Lon, double Lat)>();
            var newStationCity = new Dictionary<string, string>();
            var newOtherNames = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var kv in context.Stations)
            {
                string oldKey = kv.Key;
                var coord = kv.Value;
                string city = context.StationCity.TryGetValue(oldKey, out var c) ? c : context.MainCity;

                // 若已包含 "::" 则保持不变，否则添加前缀
                string newKey = oldKey.Contains("::") ? oldKey : $"{city}::{oldKey}";

                newStations[newKey] = coord;
                newStationCity[newKey] = city;

                if (context.StationOtherNames.TryGetValue(oldKey, out var aliases))
                    newOtherNames[newKey] = aliases.ToList();
            }

            // 替换
            context.Stations.Clear();
            foreach (var kv in newStations)
                context.Stations[kv.Key] = kv.Value;

            context.StationCity.Clear();
            foreach (var kv in newStationCity)
                context.StationCity[kv.Key] = kv.Value;

            context.StationOtherNames.Clear();
            foreach (var kv in newOtherNames)
                context.StationOtherNames[kv.Key] = kv.Value;
        }

        /// <summary>
        /// 归一化：若站名仅一个城市则降为纯键，否则保留复合键
        /// </summary>
        private void NormalizeStationKeys(Context context)
        {
            // 1. 统计每个站名对应的城市集合
            var nameCities = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var kv in context.Stations)
            {
                string key = kv.Key;
                if (!key.Contains("::")) continue; // 纯键（理论上已不存在，但保留兼容）
                string name = key.Split(new[] { "::" }, StringSplitOptions.None)[1];
                string city = context.StationCity.TryGetValue(key, out var c) ? c : context.MainCity;
                if (!nameCities.TryGetValue(name, out var cities))
                {
                    cities = new HashSet<string>(StringComparer.Ordinal);
                    nameCities[name] = cities;
                }
                cities.Add(city);
            }

            // 2. 构建新字典
            var newStations = new Dictionary<string, (double Lon, double Lat)>();
            var newStationCity = new Dictionary<string, string>();
            var newOtherNames = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var kv in context.Stations)
            {
                string oldKey = kv.Key;
                var coord = kv.Value;
                string city = context.StationCity.TryGetValue(oldKey, out var c) ? c : context.MainCity;
                string name = oldKey.Contains("::") ? oldKey.Split(new[] { "::" }, StringSplitOptions.None)[1] : oldKey;

                string newKey;
                if (nameCities.TryGetValue(name, out var cities) && cities.Count > 1)
                {
                    // 冲突时：主城市站点不加前缀，其他城市加前缀
                    if (city == context.MainCity)
                        newKey = name;
                    else
                        newKey = $"{city}::{name}";
                }
                else
                {
                    // 唯一时直接纯键
                    newKey = name;
                }

                newStations[newKey] = coord;
                newStationCity[newKey] = city;

                // 迁移别名
                if (context.StationOtherNames.TryGetValue(oldKey, out var aliases))
                {
                    if (newOtherNames.TryGetValue(newKey, out var existing))
                    {
                        foreach (var a in aliases)
                            if (!existing.Contains(a))
                                existing.Add(a);
                    }
                    else
                    {
                        newOtherNames[newKey] = aliases.ToList();
                    }
                }
            }

           

            // 替换
            context.Stations.Clear();
            foreach (var kv in newStations)
                context.Stations[kv.Key] = kv.Value;

            context.StationCity.Clear();
            foreach (var kv in newStationCity)
                context.StationCity[kv.Key] = kv.Value;

            context.StationOtherNames.Clear();
            foreach (var kv in newOtherNames)
                context.StationOtherNames[kv.Key] = kv.Value;
        }
    }
}