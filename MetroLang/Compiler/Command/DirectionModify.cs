using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class DirectionModify : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 1. 解析方向ID
            string dirId = parser.ParseId();
            if (!context.Directions.TryGetValue(dirId, out var dirInfo))
                throw new SyntaxException($"方向 '{dirId}' 未定义", parser.Peek());

            // 2. 解析修改子命令
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) break;

                string subCmd = parser.ParseId().ToUpperInvariant();
                switch (subCmd)
                {
                    case "ADD":
                        string addAttr = parser.ParseId().ToUpperInvariant();
                        if (addAttr == "ROUTING")
                        {
                            AddRouting(parser, dirInfo);
                            break;
                        }
                        if (!IsValidAttribute(addAttr))
                            throw new SyntaxException($"无效的属性名 '{addAttr}'，允许：LOOP、ROUTING", parser.Peek());
                        dirInfo.Attrs.Add(addAttr);
                        // 如果添加了 LOOP，需确保 DIRECTION_TYPE 已设置
                        if (addAttr == "LOOP" && string.IsNullOrEmpty(dirInfo.DirectionType))
                            dirInfo.DirectionType = "inner"; // 默认内环
                        break;

                    case "REMOVE":
                        string removeAttr = parser.ParseId().ToUpperInvariant();
                        if (removeAttr == "ROUTING")
                        {
                            RemoveRouting(parser, dirInfo);
                            break;
                        }
                        if (!IsValidAttribute(removeAttr))
                            throw new SyntaxException($"无效的属性名 '{removeAttr}'，允许：LOOP、ROUTING", parser.Peek());
                        dirInfo.Attrs.Remove(removeAttr);
                        // 如果移除了 LOOP，清空 DIRECTION_TYPE
                        if (removeAttr == "LOOP")
                            dirInfo.DirectionType = null;
                        break;

                    case "SET":
                        string field = parser.ParseId().ToUpperInvariant();
                        switch (field)
                        {
                            case "CONNECT_STATION":
                                dirInfo.ConnectStation = parser.ParseString();
                                break;
                            case "IS_FUTURE":
                                string futureVal = parser.ParseId().ToUpperInvariant();
                                if (futureVal != "TRUE" && futureVal != "FALSE")
                                    throw new SyntaxException($"IS_FUTURE 的值必须为 TRUE 或 FALSE", parser.Peek());
                                dirInfo.IsFuture = (futureVal == "TRUE");
                                break;
                            case "DIRECTION_TYPE":
                                if (!dirInfo.Attrs.Contains("LOOP"))
                                    throw new SyntaxException($"只能为环线方向设置 DIRECTION_TYPE", parser.Peek());
                                string typeVal = parser.ParseId().ToLowerInvariant();
                                if (typeVal != "inner" && typeVal != "outer")
                                    throw new SyntaxException($"DIRECTION_TYPE 必须为 'inner' 或 'outer'", parser.Peek());
                                dirInfo.DirectionType = typeVal;
                                break;
                            case "EXPRESS_STOPS":
                                dirInfo.ExpressStops = parser.ParseStringList();
                                break;
                            case "ROUTING":
                                SetRouting(parser, dirInfo);
                                break;
                            default:
                                throw new SyntaxException($"未知的 SET 字段 '{field}'，允许：CONNECT_STATION, IS_FUTURE, DIRECTION_TYPE, EXPRESS_STOPS, ROUTING", parser.Peek());
                        }
                        break;

                    default:
                        throw new SyntaxException($"DIRECTION_MODIFY 内未知子命令 '{subCmd}'，允许：ADD, REMOVE, SET", parser.Peek());
                }
            }

            // 更新上下文（字典已持有引用，无需额外操作）
        }

        private bool IsValidAttribute(string attr)
        {
            return attr == "LOOP";
        }

        /// <summary>ADD ROUTING：添加一个交路（名称不能重复）</summary>
        private static void AddRouting(Parser parser, DirectionInfo dirInfo)
        {
            string name = parser.ParseString();
            var stations = parser.ParseStringList();
            if (string.IsNullOrWhiteSpace(name))
                throw new SyntaxException("ROUTING 的交路名称不能为空", parser.Peek());
            if (dirInfo.Routings.Any(r => r.Name == name))
                throw new SyntaxException($"交路 '{name}' 已存在，如需修改请使用 SET ROUTING", parser.Peek());
            dirInfo.Routings.Add(new RoutingInfo { Name = name, Stations = stations });
        }

        /// <summary>REMOVE ROUTING：移除指定名称的交路</summary>
        private static void RemoveRouting(Parser parser, DirectionInfo dirInfo)
        {
            string name = parser.ParseString();
            int removed = dirInfo.Routings.RemoveAll(r => r.Name == name);
            if (removed == 0)
                parser.AddWarning($"交路 '{name}' 不存在，无需移除");
        }

        /// <summary>SET ROUTING：覆盖同名交路（不存在则添加）</summary>
        private static void SetRouting(Parser parser, DirectionInfo dirInfo)
        {
            string name = parser.ParseString();
            var stations = parser.ParseStringList();
            if (string.IsNullOrWhiteSpace(name))
                throw new SyntaxException("ROUTING 的交路名称不能为空", parser.Peek());
            var existing = dirInfo.Routings.FirstOrDefault(r => r.Name == name);
            if (existing != null)
                existing.Stations = stations;
            else
                dirInfo.Routings.Add(new RoutingInfo { Name = name, Stations = stations });
        }
    }
}