using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class StationDelete : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string stationName = parser.ParseString();

            if (!context.Stations.ContainsKey(stationName))
            {
                parser.AddWarning($"车站 '{stationName}' 不存在，忽略删除");
                return;
            }

            if (Utils.IsStationUsed(context, stationName))
            {
                var dirs = Utils.GetDirectionsUsingStation(context, stationName);
                throw new SyntaxException($"车站 '{stationName}' 被以下方向引用，无法删除: {string.Join(", ", dirs)}", parser.Peek());
            }

            Utils.DeleteStation(context, stationName);
        }
    }
}