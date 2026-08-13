using MetroEditor.MetroLang.DataModel;
using System.IO;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // BACKGROUND_DEFINE 解析：背景图片定义
    // 支持 SVG 或固定像素图片（PNG/JPG），通过地理范围定位到地图上
    // ====================================================================
    internal class BackgroundDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 1. 解析背景ID
            string bgId = parser.ParseId();
            if (context.Backgrounds.ContainsKey(bgId))
                parser.AddWarning($"背景 '{bgId}' 已存在，将覆盖");

            var bg = new BackgroundInfo();
            context.Backgrounds[bgId] = bg;

            // 2. 解析主体
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) break;

                string cmd = parser.ParseId().ToUpperInvariant();
                switch (cmd)
                {
                    case "NAME":
                        bg.Name = parser.ParseString();
                        break;

                    case "TYPE":
                        string typeVal = parser.ParseId().ToLowerInvariant();
                        if (typeVal != "svg" && typeVal != "image")
                            throw new SyntaxException($"TYPE 必须为 'svg' 或 'image'，但遇到了 '{typeVal}'", parser.Peek());
                        bg.Type = typeVal;
                        break;

                    case "SRC":
                        string imagePath = parser.ParseString();
                        string sourceDir = parser.GetCurrentTokenSourceDirectory();
                        string fullPath = Path.GetFullPath(Path.Combine(sourceDir, imagePath));
                        if (!File.Exists(fullPath))
                            throw new SyntaxException($"背景图片文件不存在: {fullPath}", parser.Peek());
                        byte[] bytes = File.ReadAllBytes(fullPath);
                        bg.SrcBase64 = Convert.ToBase64String(bytes);
                        break;

                    case "BOUNDS":
                        bg.Bounds = ParseBounds(parser);
                        break;

                    case "OPACITY":
                        bg.Opacity = parser.ParseDouble();
                        break;

                    default:
                        throw new SyntaxException($"BACKGROUND_DEFINE 内未知子命令 '{cmd}'", parser.Peek());
                }
            }

            // 3. 默认值
            if (string.IsNullOrEmpty(bg.Type))
                bg.Type = "image";
            if (string.IsNullOrEmpty(bg.Name))
                bg.Name = bgId;
        }

        /// <summary>解析地理范围 [minLon, minLat, maxLon, maxLat]</summary>
        private double[] ParseBounds(Parser parser)
        {
            parser.ParseSymbol("[");
            double minLon = parser.ParseDouble();
            parser.ParseSymbol(",");
            double minLat = parser.ParseDouble();
            parser.ParseSymbol(",");
            double maxLon = parser.ParseDouble();
            parser.ParseSymbol(",");
            double maxLat = parser.ParseDouble();
            parser.ParseSymbol("]");
            return new double[] { minLon, minLat, maxLon, maxLat };
        }
    }
}
