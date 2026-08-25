using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler.Command
{
    public class GenTransfer : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            double walkSpeedKmh = 5.0;
            bool updateExisting = false;

            while (true)
            {
                if (parser.IsPeekId("WALK_SPEED"))
                {
                    parser.ParseId();
                    walkSpeedKmh = parser.ParseDouble();
                }
                else if (parser.IsPeekId("UPDATE_EXISTING"))
                {
                    parser.ParseId();
                    updateExisting = true;
                }
                else
                {
                    break;
                }
            }

            double walkSpeedMps = walkSpeedKmh * 1000 / 3600.0;

            foreach (var transfer in context.Transfers)
            {
                if (!updateExisting && transfer.Time.HasValue)
                    continue;

                string station = transfer.Station;
                string line1 = transfer.L1;
                string line2 = transfer.L2;

                // 判断该车站是否有任何平台坐标定义（至少一条线路）
                bool hasAnyPlatform = context.PlatformCoords.Keys.Any(k => k.Station == station);
                if (!hasAnyPlatform)
                    continue; // 该车站没有任何线路定义平台坐标，跳过

                // 获取有效坐标
                var coord1 = GetEffectiveCoordinate(context, station, line1);
                var coord2 = GetEffectiveCoordinate(context, station, line2);

                if (coord1 == null || coord2 == null)
                    continue;

                bool isPlatform1 = context.PlatformCoords.ContainsKey((station, line1));
                bool isPlatform2 = context.PlatformCoords.ContainsKey((station, line2));

                // 两条线路都未定义平台坐标 → 跳过
                if (!isPlatform1 && !isPlatform2)
                    continue;

                double distanceKm = Utils.Haversine(coord1.Value.Lat, coord1.Value.Lon, coord2.Value.Lat, coord2.Value.Lon);
                double timeSec = (distanceKm * 1000) / walkSpeedMps;
                transfer.Time = timeSec;
            }
        }

        private (double Lon, double Lat)? GetEffectiveCoordinate(Context context, string station, string lineId)
        {
            if (context.PlatformCoords.TryGetValue((station, lineId), out var platform))
                return platform;
            if (context.Stations.TryGetValue(station, out var global))
                return global;
            return null;
        }
    }
}
