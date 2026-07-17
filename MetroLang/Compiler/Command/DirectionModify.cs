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
                        if (!IsValidAttribute(addAttr))
                            throw new SyntaxException($"无效的属性名 '{addAttr}'，允许：BRANCH, FAST, LOOP", parser.Peek());
                        dirInfo.Attrs.Add(addAttr);
                        // 如果添加了 LOOP，需确保 DIRECTION_TYPE 已设置
                        if (addAttr == "LOOP" && string.IsNullOrEmpty(dirInfo.DirectionType))
                            dirInfo.DirectionType = "inner"; // 默认内环
                        break;

                    case "REMOVE":
                        string removeAttr = parser.ParseId().ToUpperInvariant();
                        if (!IsValidAttribute(removeAttr))
                            throw new SyntaxException($"无效的属性名 '{removeAttr}'，允许：BRANCH, FAST, LOOP", parser.Peek());
                        dirInfo.Attrs.Remove(removeAttr);
                        // 如果移除了 LOOP，清空 DIRECTION_TYPE
                        if (removeAttr == "LOOP")
                            dirInfo.DirectionType = null;
                        break;

                    case "SET":
                        string field = parser.ParseId().ToUpperInvariant();
                        switch (field)
                        {
                            case "NICK_NAME":
                                dirInfo.Alias = parser.ParseString();
                                break;
                            case "NICK_COLOR":
                                dirInfo.AliasColor = parser.ParseString();
                                break;
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
                            default:
                                throw new SyntaxException($"未知的 SET 字段 '{field}'，允许：NICK_NAME, NICK_COLOR, CONNECT_STATION, IS_FUTURE, DIRECTION_TYPE", parser.Peek());
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
            return attr == "BRANCH" || attr == "FAST" || attr == "LOOP";
        }
    }
}