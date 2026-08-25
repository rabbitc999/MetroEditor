using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>
    /// 处理 STATION_DELETE 指令，删除未被任何方向引用的车站。
    /// </summary>
    internal class StationDelete : ICommandHandler
    {
        /// <summary>
        /// 校验车站存在且未被线路方向引用后执行删除。
        /// </summary>
        /// <param name="parser">当前源文件解析器</param>
        /// <param name="context">编译中的路网数据上下文</param>
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