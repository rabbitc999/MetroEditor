using MetroEditor.MetroLang.DataModel;
using MetroEditor.PathFinding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MetroEditor.MetroLang.Exporter
{
    public static class MBINExporter
    {
        public static byte[] Export(ParsedData data, MetroGraph graph)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // 预先过滤：只保留非未来、非快车的方向
            var activeDirections = data.Directions
                .Where(kv => !kv.Value.IsFuture && !kv.Value.Attrs.Contains("FAST"))
                .ToList();

            // -------- 构建外部键映射（内部方向ID -> 外部方向键） --------
            var internalToExternalKey = new Dictionary<string, string>();
            foreach (var dirKv in activeDirections)
            {
                var dir = dirKv.Value;
                string lineId = dir.LineId;
                string start = dir.Stations.FirstOrDefault() ?? "";
                string end = dir.Stations.LastOrDefault() ?? "";
                if (!string.IsNullOrEmpty(start) && !string.IsNullOrEmpty(end))
                {
                    internalToExternalKey[dirKv.Key] = $"{lineId}|{start}|{end}";
                }
            }

            // -------- 构建“显示线路”映射 --------
            var displayLineMap = new Dictionary<string, (string Name, string Color, int Maxspeed, int OpenYear)>();
            var dirToDisplayLineIdx = new Dictionary<string, ushort>();

            foreach (var dirKv in activeDirections)
            {
                string dirId = dirKv.Key;
                var dir = dirKv.Value;
                var physLine = data.Lines[dir.LineId];

                string displayName = string.IsNullOrEmpty(dir.Alias) ? physLine.Name : dir.Alias;
                string displayColor = string.IsNullOrEmpty(dir.AliasColor) ? physLine.Color : dir.AliasColor;

                string key = $"{dir.LineId}|{displayName}|{displayColor}";

                if (!displayLineMap.ContainsKey(key))
                {
                    displayLineMap[key] = (
                        Name: displayName,
                        Color: displayColor,
                        Maxspeed: (int)(physLine.Maxspeed ?? 0),
                        OpenYear: physLine.OpenYear
                    );
                }
            }

            var displayLineList = displayLineMap.ToList();
            for (int i = 0; i < displayLineList.Count; i++)
            {
                var kv = displayLineList[i];
                string key = kv.Key;
                foreach (var dirKv in activeDirections)
                {
                    string dirId = dirKv.Key;
                    var dir = dirKv.Value;
                    var physLine = data.Lines[dir.LineId];
                    string displayName = string.IsNullOrEmpty(dir.Alias) ? physLine.Name : dir.Alias;
                    string displayColor = string.IsNullOrEmpty(dir.AliasColor) ? physLine.Color : dir.AliasColor;
                    string testKey = $"{dir.LineId}|{displayName}|{displayColor}";
                    if (testKey == key)
                    {
                        dirToDisplayLineIdx[dirId] = (ushort)i;
                    }
                }
            }

            int displayLineCount = displayLineList.Count;

            // -------- 构建字符串池 --------
            var stringMap = new Dictionary<string, int>();
            var stringList = new List<string>();
            int AddString(string s)
            {
                if (string.IsNullOrEmpty(s)) return 0;
                if (!stringMap.TryGetValue(s, out int idx))
                {
                    idx = stringList.Count;
                    stringMap[s] = idx;
                    stringList.Add(s);
                }
                return idx;
            }

            foreach (var kv in displayLineList)
                AddString(kv.Value.Name);
            foreach (var st in data.Stations.Keys) AddString(st);
            AddString(data.MainCity);
            foreach (var city in data.Cities.Keys) AddString(city);

            int strPoolSize = 0;
            for (int i = 0; i < stringList.Count; i++)
            {
                strPoolSize += Encoding.UTF8.GetByteCount(stringList[i]) + 1;
                while (strPoolSize % 4 != 0) strPoolSize++;
            }

            // -------- 构建图节点映射 --------
            var nodeMap = new Dictionary<string, ushort>();
            var nodeDirIds = new List<string>();
            var nodeStationNames = new List<string>();

            ushort nextNodeId = 0;
            foreach (var dirKv in activeDirections)
            {
                string dirId = dirKv.Key;
                var dir = dirKv.Value;

                foreach (var station in dir.Stations)
                {
                    string nodeKey = $"{dirId}|{station}";
                    if (!nodeMap.ContainsKey(nodeKey))
                    {
                        nodeMap[nodeKey] = nextNodeId++;
                        nodeDirIds.Add(dirId);
                        nodeStationNames.Add(station);
                    }
                }
            }
            ushort nodeCount = nextNodeId;

            // -------- 构建站名到节点列表的映射（并收集所有出现的站名） --------
            var stationNodes = new Dictionary<string, List<ushort>>();
            for (ushort i = 0; i < nodeCount; i++)
            {
                string stName = nodeStationNames[i];
                if (!stationNodes.TryGetValue(stName, out var list))
                    stationNodes[stName] = list = new List<ushort>();
                list.Add(i);
            }

            // -------- 构建站点元数据表（确保包含所有站名） --------
            // 合并 data.Stations 和 stationNodes 的键
            var allStationNames = new HashSet<string>(data.Stations.Keys);
            foreach (var name in stationNodes.Keys)
                allStationNames.Add(name);

            var stationList = allStationNames
                .Select(name => new
                {
                    Name = name,
                    Coord = data.Stations.TryGetValue(name, out var c) ? c : (0.0, 0.0)
                })
                .ToList();

            var stationIndexMap = new Dictionary<string, ushort>();
            for (ushort i = 0; i < stationList.Count; i++)
                stationIndexMap[stationList[i].Name] = i;

            // -------- 预计算运行边 --------
            var railEdges = new List<(ushort from, ushort to, uint distCm, ushort timeSec)>();

            foreach (var dirKv in activeDirections)
            {
                string dirId = dirKv.Key;
                var dir = dirKv.Value;

                if (!data.Distances.TryGetValue(dirId, out var distList)) continue;

                for (int i = 0; i < dir.Stations.Count - 1; i++)
                {
                    string fromSt = dir.Stations[i];
                    string toSt = dir.Stations[i + 1];
                    string fromKey = $"{dirId}|{fromSt}";
                    string toKey = $"{dirId}|{toSt}";

                    if (!nodeMap.TryGetValue(fromKey, out ushort fromNode)) continue;
                    if (!nodeMap.TryGetValue(toKey, out ushort toNode)) continue;

                    double distKm = (i < distList.Count && distList[i] < 1e30) ? distList[i] : 0.5;

                    int maxSpeed = data.Lines.TryGetValue(dir.LineId, out var li) ? (int)(li.Maxspeed ?? 80) : 80;
                    double timeSec = TimeCalculator.ComputeSegmentTime(
                        distKm, maxSpeed, dir.LineId, dirId, fromSt, toSt, data);

                    ushort timeUShort = (ushort)Math.Round(timeSec);
                    if (timeUShort == 0) timeUShort = 1;

                    uint distCm = (uint)(distKm * 100000);

                    railEdges.Add((fromNode, toNode, distCm, timeUShort));
                }
            }

            // -------- 预计算换乘边 --------
            var transferEdges = new List<(ushort from, ushort to, ushort timeSec)>();

            foreach (var kv in stationNodes)
            {
                var nodes = kv.Value;
                if (nodes.Count < 2) continue;

                string stationName = kv.Key;
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        string dirId1 = nodeDirIds[nodes[i]];
                        string dirId2 = nodeDirIds[nodes[j]];
                        string dirKey1 = internalToExternalKey.TryGetValue(dirId1, out var ek1) ? ek1 : dirId1;
                        string dirKey2 = internalToExternalKey.TryGetValue(dirId2, out var ek2) ? ek2 : dirId2;

                        double transferSec = StationUtils.GetInnerTransferTime(stationName, dirKey1, dirKey2, data);
                        ushort transferUShort = (ushort)Math.Round(transferSec);
                        if (transferUShort == 0) transferUShort = 10;

                        transferEdges.Add((nodes[i], nodes[j], transferUShort));
                        transferEdges.Add((nodes[j], nodes[i], transferUShort));
                    }
                }
            }

            // -------- 站名 -> 节点列表查找表 --------
            var lookupItems = new List<(ushort stationIdx, List<ushort> nodeIds)>();
            foreach (var kv in stationNodes)
            {
                if (stationIndexMap.TryGetValue(kv.Key, out ushort stIdx))
                    lookupItems.Add((stIdx, kv.Value));
                else
                {
                    // 理论上不会发生，因为我们已经合并了所有站名
                    throw new InvalidOperationException($"站点 '{kv.Key}' 未在索引表中找到");
                }
            }

            // -------- 票价分段表 --------
            var fareSegments = data.MoneyRules.OfType<MoneySegmentRule>().ToList();

            // -------- 写入文件 (强制大端) --------
            long headerPos = ms.Position;
            writer.Write(new byte[64]);

            // 字符串池
            long strPoolPos = ms.Position;
            for (int i = 0; i < stringList.Count; i++)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(stringList[i]);
                writer.Write(bytes);
                writer.Write((byte)0);
                while (ms.Position % 4 != 0) writer.Write((byte)0);
            }

            // 线路表 (32字节/条)
            long lineTablePos = ms.Position;
            foreach (var kv in displayLineList)
            {
                var info = kv.Value;
                WriteBE(writer, (uint)stringMap[info.Name]);
                WriteBE(writer, ParseColor(info.Color));
                WriteBE(writer, (ushort)info.Maxspeed);
                WriteBE(writer, (ushort)info.OpenYear);
                WriteBE(writer, (uint)0);
                WriteBE(writer, (uint)0);
                WriteBE(writer, (uint)0);
                WriteBE(writer, (uint)0);
            }

            // 站点元数据表 (24字节/条)
            long stationMetaPos = ms.Position;
            foreach (var item in stationList)
            {
                WriteBE(writer, stationIndexMap[item.Name]);
                WriteBE(writer, (int)(item.Coord.Item1 * 1e6));
                WriteBE(writer, (int)(item.Coord.Item2 * 1e6));
                WriteBE(writer, (ushort)0);
                WriteBE(writer, (uint)0);
            }

            // 节点表 (4字节/条)
            long nodeTablePos = ms.Position;
            for (int i = 0; i < nodeCount; i++)
            {
                string dirId = nodeDirIds[i];
                ushort displayLineIdx = dirToDisplayLineIdx[dirId];
                WriteBE(writer, displayLineIdx);
                WriteBE(writer, stationIndexMap[nodeStationNames[i]]);
            }

            // 运行边表
            long railEdgePos = ms.Position;
            foreach (var e in railEdges)
            {
                WriteBE(writer, e.from);
                WriteBE(writer, e.to);
                WriteBE(writer, e.distCm);
                WriteBE(writer, e.timeSec);
            }

            // 换乘边表
            long transferEdgePos = ms.Position;
            foreach (var e in transferEdges)
            {
                WriteBE(writer, e.from);
                WriteBE(writer, e.to);
                WriteBE(writer, e.timeSec);
            }

            // 查找表
            long lookupTablePos = ms.Position;
            WriteBE(writer, (ushort)lookupItems.Count);
            foreach (var item in lookupItems)
            {
                WriteBE(writer, item.stationIdx);
                WriteBE(writer, (ushort)item.nodeIds.Count);
                foreach (var nid in item.nodeIds)
                    WriteBE(writer, nid);
            }

            // 票价分段表
            long fareTablePos = ms.Position;
            foreach (var seg in fareSegments)
            {
                WriteBE(writer, (uint)(seg.Start * 100000));
                WriteBE(writer, (uint)(seg.End * 100000));
                WriteBE(writer, (uint)(seg.Price * 100));
            }

            // -------- 回填文件头 --------
            long totalSize = ms.Position;
            ms.Seek(headerPos, SeekOrigin.Begin);

            WriteBE(writer, (uint)0x4D42494E); // "MBIN"
            WriteBE(writer, (byte)0x02);
            WriteBE(writer, (byte)0x01);
            WriteBE(writer, (byte)0);
            WriteBE(writer, (byte)0);
            WriteBE(writer, (uint)strPoolPos);
            WriteBE(writer, (uint)lineTablePos);
            WriteBE(writer, (uint)stationMetaPos);
            WriteBE(writer, (uint)nodeTablePos);
            WriteBE(writer, (uint)railEdgePos);
            WriteBE(writer, (uint)transferEdgePos);
            WriteBE(writer, (uint)lookupTablePos);
            WriteBE(writer, (uint)fareTablePos);
            WriteBE(writer, (uint)totalSize);
            WriteBE(writer, (ushort)displayLineCount);
            WriteBE(writer, (ushort)stationList.Count);
            WriteBE(writer, (ushort)nodeCount);
            WriteBE(writer, (ushort)railEdges.Count);
            WriteBE(writer, (ushort)transferEdges.Count);
            WriteBE(writer, (ushort)lookupItems.Count);
            WriteBE(writer, (ushort)fareSegments.Count);

            while (ms.Position < 64) writer.Write((byte)0);

            return ms.ToArray();
        }

        // ======================== 大端写入辅助 ========================
        private static void WriteBE(BinaryWriter w, byte v) => w.Write(v);
        private static void WriteBE(BinaryWriter w, ushort v)
        {
            var b = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            w.Write(b);
        }
        private static void WriteBE(BinaryWriter w, uint v)
        {
            var b = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            w.Write(b);
        }
        private static void WriteBE(BinaryWriter w, int v)
        {
            var b = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            w.Write(b);
        }

        private static uint ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return 0xCCCCCC;
            return uint.Parse(hex.TrimStart('#'), System.Globalization.NumberStyles.HexNumber);
        }
    }
}