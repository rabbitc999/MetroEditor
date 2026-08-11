using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class DirectionSplit : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 1. 解析源方向
            string sourceId = parser.ParseId();
            if (!context.Directions.TryGetValue(sourceId, out var sourceDir))
                throw new SyntaxException($"方向 '{sourceId}' 未定义", parser.Peek());

            if (sourceDir.Attrs.Contains("LOOP"))
                throw new SyntaxException($"方向 '{sourceId}' 是环线，不能拆分", parser.Peek());

            // 2. 解析拆分点站点
            parser.ParseId("AT");
            string station = parser.ParseString();

            var stations = sourceDir.Stations;
            int splitIndex = stations.IndexOf(station);
            if (splitIndex == -1)
                throw new SyntaxException($"站点 '{station}' 不在方向 '{sourceId}' 中", parser.Peek());

            if (splitIndex == 0 || splitIndex == stations.Count - 1)
                throw new SyntaxException($"拆分点 '{station}' 不能是方向的首站或末站", parser.Peek());

            // 3. 解析新方向 ID
            parser.ParseId("INTO");
            string newId1 = parser.ParseId();
            string newId2 = parser.ParseId();

            if (context.Directions.ContainsKey(newId1))
                throw new SyntaxException($"方向 '{newId1}' 已存在", parser.Peek());
            if (context.Directions.ContainsKey(newId2))
                throw new SyntaxException($"方向 '{newId2}' 已存在", parser.Peek());

            // 4. 获取原距离列表
            if (!context.Distances.TryGetValue(sourceId, out var sourceDistances))
                throw new SyntaxException($"方向 '{sourceId}' 缺少 DISTANCE 定义", parser.Peek());

            // 5. 构建新方向的站点和距离
            // 新方向1: 站点 [0..splitIndex], 距离 [0..splitIndex-1] + NIL
            var stations1 = stations.Take(splitIndex + 1).ToList();
            var dist1 = sourceDistances.Take(splitIndex).ToList();
            dist1.Add(1e50); // NIL

            // 新方向2: 站点 [splitIndex..end], 距离 [splitIndex..end-1] + NIL
            var stations2 = stations.Skip(splitIndex).ToList();
            var dist2 = sourceDistances.Skip(splitIndex).Take(stations2.Count - 1).ToList();
            dist2.Add(1e50);

            // 6. 复制属性（除 LOOP 外）
            var attrs = new HashSet<string>(sourceDir.Attrs);
            attrs.Remove("LOOP");

            // 7. 创建方向1
            var dir1 = new DirectionInfo
            {
                LineId = sourceDir.LineId,
                Stations = stations1,
                Attrs = attrs,
                DirectionType = sourceDir.DirectionType,
                IsFuture = sourceDir.IsFuture,
                ConnectStation = sourceDir.ConnectStation,
                ViaDirections = new List<string>(sourceDir.ViaDirections) // 保留 via 关系
            };
            context.Directions[newId1] = dir1;
            context.Distances[newId1] = dist1;

            // 8. 创建方向2
            var dir2 = new DirectionInfo
            {
                LineId = sourceDir.LineId,
                Stations = stations2,
                Attrs = attrs,
                DirectionType = sourceDir.DirectionType,
                IsFuture = sourceDir.IsFuture,
                ConnectStation = sourceDir.ConnectStation,
                ViaDirections = new List<string>(sourceDir.ViaDirections)
            };
            context.Directions[newId2] = dir2;
            context.Distances[newId2] = dist2;

            // 可选：保留原方向不删除，用户可自行决定是否删除
        }
    }
}