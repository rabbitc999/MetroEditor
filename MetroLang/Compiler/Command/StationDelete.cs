using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>
    /// 删除车站命令；删除前会阻止仍被线路方向引用的车站。
    /// </summary>
    internal class StationDelete : ICommandHandler
    {
        /// <summary>
        /// 解析车站名称，处理不存在警告和引用完整性检查。
        /// </summary>
        public void Handle(Parser parser, Context context)
        {
            string stationName = parser.ParseString();

            // 删除不存在的目标属于可恢复输入错误，因此只记录警告并继续编译。
            if (!context.Stations.ContainsKey(stationName))
            {
                parser.AddWarning($"车站 '{stationName}' 不存在，忽略删除");
                return;
            }

            // 线路方向仍引用该站时直接报错，避免产生无法遍历的断裂方向。
            if (Utils.IsStationUsed(context, stationName))
            {
                var dirs = Utils.GetDirectionsUsingStation(context, stationName);
                throw new SyntaxException($"车站 '{stationName}' 被以下方向引用，无法删除: {string.Join(", ", dirs)}", parser.Peek());
            }

            Utils.DeleteStation(context, stationName);
        }
    }
}
