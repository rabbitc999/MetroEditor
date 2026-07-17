using MetroEditor.MetroLang.Compiler;
using System.IO;

internal class LineModify : ICommandHandler
{
    public void Handle(Parser parser, Context context)
    {
        string lineId = parser.ParseId();
        if (!context.Lines.TryGetValue(lineId, out var lineInfo))
            throw new SyntaxException($"线路 '{lineId}' 未定义", parser.Peek());

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
                        case "NAME": lineInfo.Name = parser.ParseString(); break;
                        case "COLOR": lineInfo.Color = parser.ParseString(); break;
                        case "CITY": lineInfo.City = parser.ParseString(); break;
                        case "OPEN_YEAR": lineInfo.OpenYear = parser.ParseInt(); break;
                        case "FORMATION": lineInfo.Formation = parser.ParseString(); break;
                        case "MAXSPEED": lineInfo.Maxspeed = parser.ParseDouble(); break;
                        case "POWER_KW": lineInfo.PowerKw = parser.ParseDouble(); break;
                        case "BRAKE": lineInfo.Brake = parser.ParseDouble(); break;
                        case "DISABLED":
                            string val = parser.ParseId().ToUpperInvariant();
                            lineInfo.Disabled = val == "TRUE";
                            break;
                        case "IMAGE":
                            string imgPath = parser.ParseString();
                            string fullPath = Path.GetFullPath(Path.Combine(parser.GetCurrentTokenSourceDirectory(), imgPath));
                            if (!File.Exists(fullPath))
                                throw new SyntaxException($"图片文件不存在: {fullPath}", parser.Peek());
                            lineInfo.ImageBase64 = Convert.ToBase64String(File.ReadAllBytes(fullPath));
                            break;
                        case "LINK":
                            context.LineLinks[lineId] = parser.ParseString();
                            break;
                        default:
                            throw new SyntaxException($"未知字段 '{field}'，允许: NAME, COLOR, CITY, OPEN_YEAR, FORMATION, MAXSPEED, POWER_KW, BRAKE, DISABLED, IMAGE, LINK", parser.Peek());
                    }
                    break;

                case "ADD":
                    string addAttr = parser.ParseId().ToUpperInvariant();
                    if (addAttr == "SPECIAL")
                        context.SpecialLineIds.Add(lineId);
                    else
                        throw new SyntaxException($"ADD 只支持 SPECIAL", parser.Peek());
                    break;

                case "REMOVE":
                    string remAttr = parser.ParseId().ToUpperInvariant();
                    if (remAttr == "SPECIAL")
                        context.SpecialLineIds.Remove(lineId);
                    else
                        throw new SyntaxException($"REMOVE 只支持 SPECIAL", parser.Peek());
                    break;

                default:
                    throw new SyntaxException($"未知子命令 '{subCmd}'，允许: SET, ADD, REMOVE", parser.Peek());
            }
        }
    }
}