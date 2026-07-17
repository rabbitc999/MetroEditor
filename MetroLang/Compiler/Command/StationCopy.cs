using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class StationCopy : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string sourceStation = parser.ParseString();
            string targetStation = parser.ParseString();

            if (!context.Stations.ContainsKey(sourceStation))
                throw new SyntaxException($"车站 '{sourceStation}' 未定义", parser.Peek());

            if (context.Stations.ContainsKey(targetStation))
                throw new SyntaxException($"目标车站 '{targetStation}' 已存在", parser.Peek());

            Utils.CopyStation(context, sourceStation, targetStation);
        }
    }
}