using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class StationRename : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            string oldName = parser.ParseString();
            string newName = parser.ParseString();

            if (!context.Stations.ContainsKey(oldName))
                throw new SyntaxException($"车站 '{oldName}' 未定义", parser.Peek());

            if (context.Stations.ContainsKey(newName))
                throw new SyntaxException($"目标车站 '{newName}' 已存在", parser.Peek());

            // 更新坐标
            context.Stations[newName] = context.Stations[oldName];
            context.Stations.Remove(oldName);

            // 更新城市
            if (context.StationCity.TryGetValue(oldName, out var city))
            {
                context.StationCity[newName] = city;
                context.StationCity.Remove(oldName);
            }

            // 更新别名
            if (context.StationOtherNames.TryGetValue(oldName, out var names))
            {
                context.StationOtherNames[newName] = names;
                context.StationOtherNames.Remove(oldName);
            }

            // 更新设施
            if (context.Facilities.TryGetValue(oldName, out var fac))
            {
                context.Facilities[newName] = fac;
                context.Facilities.Remove(oldName);
            }

            // 更新方向中的站点名称
            Utils.ReplaceStationInDirections(context, oldName, newName);

            // 更新停站时间
            var dwellKeys = context.DwellTimes.Keys.Where(k => k.Station == oldName).ToList();
            foreach (var key in dwellKeys)
            {
                var value = context.DwellTimes[key];
                context.DwellTimes.Remove(key);
                context.DwellTimes[(newName, key.LineId)] = value;
            }

            // 更新车站状态
            var statusKeys = context.StationStatus.Keys.Where(k => k.Station == oldName).ToList();
            foreach (var key in statusKeys)
            {
                var value = context.StationStatus[key];
                context.StationStatus.Remove(key);
                context.StationStatus[(newName, key.LineId)] = value;
            }

            // 更新换乘规则（同站换乘）
            foreach (var transfer in context.Transfers.Where(t => t.Station == oldName))
                transfer.Station = newName;

            // 更新虚拟换乘
            foreach (var v in context.VirtualTransfers.Where(v => v.StationA == oldName))
                v.StationA = newName;
            foreach (var v in context.VirtualTransfers.Where(v => v.StationB == oldName))
                v.StationB = newName;

            // 更新车站运营模式
            foreach (var mode in context.StationModes.Where(m => m.Station == oldName))
                mode.Station = newName;
        }
    }
}