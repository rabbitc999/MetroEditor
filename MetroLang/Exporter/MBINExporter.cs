using MetroEditor.MetroLang.DataModel;
using MetroEditor.Simulator;
using MetroEditor.Simulator.Algorithms;
using MetroEditor.Simulator.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MetroEditor.MetroLang.Exporter
{
    /// <summary>
    /// MBIN 二进制导出器（小端模式，字符串池为 GBK 编码）
    /// </summary>
    public static class MBINExporter
    {
        public static byte[] Export(ParsedData data, SimMetroGraph graph)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding StringEncoding = Encoding.GetEncoding(936); // GBK
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // 1. 过滤方向
            var activeDirections = data.Directions
                .Where(kv => !kv.Value.IsFuture && !kv.Value.Attrs.Contains("FAST"))
                .ToList();

            // 2. 构建字符串池
            var stringMap = new Dictionary<string, int>();
            var stringOffsetMap = new Dictionary<string, int>();
            var stringList = new List<string>();

            int AddString(string s)
            {
                if (string.IsNullOrEmpty(s)) return -1;
                if (!stringMap.TryGetValue(s, out int idx))
                {
                    idx = stringList.Count;
                    stringMap[s] = idx;
                    stringList.Add(s);
                }
                return idx;
            }

            var allLineNames = activeDirections
                .Select(kv => new { kv.Value, PhysLine = data.Lines[kv.Value.LineId] })
                .Select(x => string.IsNullOrEmpty(x.Value.Alias) ? x.PhysLine.Name : x.Value.Alias)
                .Distinct()
                .ToList();
            foreach (var name in allLineNames) AddString(name);

            var stationNames = data.Stations.Keys.ToList();
            foreach (var st in stationNames) AddString(st);

            var languageOrder = data.LanguageOrder?.Any() == true
                ? data.LanguageOrder.ToList()
                : new List<string> { "zh", "en" };
            foreach (var lang in languageOrder) AddString(lang);
            AddString(data.MainCity);

            // 写入字符串池（GBK）
            using var strMs = new MemoryStream();
            using var strWriter = new BinaryWriter(strMs);
            for (int i = 0; i < stringList.Count; i++)
            {
                stringOffsetMap[stringList[i]] = (int)strMs.Position;
                byte[] bytes = StringEncoding.GetBytes(stringList[i]);
                strWriter.Write(bytes);
                strWriter.Write((byte)0);
                while (strMs.Position % 4 != 0) strWriter.Write((byte)0);
            }
            byte[] strBytes = strMs.ToArray();
            long strOffset = 64;
            int strPoolSize = strBytes.Length;

            // 3. 显示线路映射
            var displayLineMap = new Dictionary<string, (string Name, string Color, int Maxspeed, int OpenYear)>();
            var dirToLineIdx = new Dictionary<string, ushort>();

            foreach (var dirKv in activeDirections)
            {
                string dirId = dirKv.Key;
                var dir = dirKv.Value;
                var physLine = data.Lines[dir.LineId];
                string name = string.IsNullOrEmpty(dir.Alias) ? physLine.Name : dir.Alias;
                string color = string.IsNullOrEmpty(dir.AliasColor) ? physLine.Color : dir.AliasColor;
                string key = $"{dir.LineId}|{name}|{color}";
                if (!displayLineMap.ContainsKey(key))
                {
                    displayLineMap[key] = (
                        Name: name,
                        Color: color,
                        Maxspeed: (int)(physLine.Maxspeed ?? 0),
                        OpenYear: physLine.OpenYear
                    );
                }
            }

            var displayLineList = displayLineMap.ToList();
            for (int i = 0; i < displayLineList.Count; i++)
            {
                var kv = displayLineList[i];
                foreach (var dirKv in activeDirections)
                {
                    var dir = dirKv.Value;
                    var physLine = data.Lines[dir.LineId];
                    string name = string.IsNullOrEmpty(dir.Alias) ? physLine.Name : dir.Alias;
                    string color = string.IsNullOrEmpty(dir.AliasColor) ? physLine.Color : dir.AliasColor;
                    string key = $"{dir.LineId}|{name}|{color}";
                    if (key == kv.Key)
                        dirToLineIdx[dirKv.Key] = (ushort)i;
                }
            }
            int displayLineCount = displayLineList.Count;

            // 4. 构建节点映射
            var nodeMap = new Dictionary<string, ushort>();
            var nodeDirIds = new List<string>();
            var nodeStationNames = new List<string>();
            ushort nextNode = 0;
            foreach (var dirKv in activeDirections)
            {
                string dirId = dirKv.Key;
                var dir = dirKv.Value;
                foreach (var st in dir.Stations)
                {
                    string key = $"{dirId}|{st}";
                    if (!nodeMap.ContainsKey(key))
                    {
                        nodeMap[key] = nextNode++;
                        nodeDirIds.Add(dirId);
                        nodeStationNames.Add(st);
                    }
                }
            }
            ushort nodeCount = nextNode;

            // 5. 站点索引
            var stationIndexMap = new Dictionary<string, ushort>();
            for (ushort i = 0; i < stationNames.Count; i++)
                stationIndexMap[stationNames[i]] = i;

            // 6. 站名->节点列表查找表
            var stationNodes = new Dictionary<string, List<ushort>>();
            for (ushort i = 0; i < nodeCount; i++)
            {
                string st = nodeStationNames[i];
                if (!stationNodes.TryGetValue(st, out var list))
                    stationNodes[st] = list = new List<ushort>();
                list.Add(i);
            }

            var lookupItems = new List<(ushort stIdx, List<ushort> nodes)>();
            foreach (var kv in stationNodes)
            {
                if (stationIndexMap.TryGetValue(kv.Key, out var idx))
                    lookupItems.Add((idx, kv.Value));
            }

            // 7. 预计算运行边
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
                    data.Lines.TryGetValue(dir.LineId, out var line);
                    int maxSpeed = line?.Maxspeed != null ? (int)line.Maxspeed.Value : 80;
                    int powerKw = line?.PowerKw != null ? (int)line.PowerKw.Value : 2640;
                    double brake = line?.Brake ?? 1.0;
                    string formation = line?.Formation ?? "6B";

                    double timeSec = SimTrainTimeUtils.ComputeSegmentTime(distKm, formation, maxSpeed, powerKw, brake);
                    ushort timeUShort = (ushort)Math.Round(timeSec);
                    if (timeUShort == 0) timeUShort = 1;
                    uint distCm = (uint)(distKm * 100000);
                    railEdges.Add((fromNode, toNode, distCm, timeUShort));
                }
            }

            // 8. 预计算换乘边
            var transferEdges = new List<(ushort from, ushort to, ushort timeSec)>();
            foreach (var kv in stationNodes)
            {
                var nodes = kv.Value;
                if (nodes.Count < 2) continue;
                string st = kv.Key;
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        string dirId1 = nodeDirIds[nodes[i]];
                        string dirId2 = nodeDirIds[nodes[j]];
                        var dir1 = data.Directions[dirId1];
                        var dir2 = data.Directions[dirId2];
                        string dirKey1 = $"{dir1.LineId}|{dir1.Stations.First()}|{dir1.Stations.Last()}";
                        string dirKey2 = $"{dir2.LineId}|{dir2.Stations.First()}|{dir2.Stations.Last()}";
                        double transferSec = SimStationUtils.GetTransferTime(graph.Cache, st, dirKey1, dirKey2);
                        ushort transferUShort = (ushort)Math.Round(transferSec);
                        if (transferUShort == 0) transferUShort = 10;
                        transferEdges.Add((nodes[i], nodes[j], transferUShort));
                        transferEdges.Add((nodes[j], nodes[i], transferUShort));
                    }
                }
            }

            // 9. 票价分段（保证至少一个分段）
            var fareSegments = data.MoneyRules.OfType<MoneySegmentRule>().ToList();
            if (fareSegments.Count == 0)
            {
                fareSegments.Add(new MoneySegmentRule { Start = 0, End = 1000, Price = 2.0 });
            }

            // 10. 计算偏移
            long strOffsetPos = 64;
            long lineOffsetPos = strOffsetPos + strPoolSize;
            int lineTableSize = displayLineCount * 32;
            long stationMetaOffsetPos = lineOffsetPos + lineTableSize;
            int stationMetaSize = stationNames.Count * 20;     // 20 字节
            long nodeOffsetPos = stationMetaOffsetPos + stationMetaSize;
            int nodeTableSize = nodeCount * 4;
            long railEdgeOffsetPos = nodeOffsetPos + nodeTableSize;
            int railEdgeSize = railEdges.Count * 10;
            long transferEdgeOffsetPos = railEdgeOffsetPos + railEdgeSize;
            int transferEdgeSize = transferEdges.Count * 6;
            long lookupOffsetPos = transferEdgeOffsetPos + transferEdgeSize;
            int lookupSize = 2;
            foreach (var item in lookupItems)
                lookupSize += 2 + 2 + item.nodes.Count * 2;
            long fareOffsetPos = lookupOffsetPos + lookupSize;
            int fareSize = fareSegments.Count * 12;
            long standardEnd = fareOffsetPos + fareSize;

            // 语言表（追加在标准部分之后）
            var langOffsets = new List<uint>();
            foreach (var lang in languageOrder)
            {
                if (stringOffsetMap.TryGetValue(lang, out int off))
                    langOffsets.Add((uint)off);
                else
                    langOffsets.Add(0);
            }
            int langCount = langOffsets.Count;
            long langTableOffset = standardEnd;
            int langTableSize = langCount * 4;
            long totalSize = langTableOffset + langTableSize;

            // 11. 写入数据
            writer.Write(new byte[64]); // 头部占位

            // 字符串池
            writer.Write(strBytes);

            // 线路表
            foreach (var kv in displayLineList)
            {
                var info = kv.Value;
                int nameOff = stringOffsetMap[info.Name];
                writer.Write((uint)nameOff);
                writer.Write(ParseColor(info.Color));
                writer.Write((ushort)info.Maxspeed);
                writer.Write((ushort)info.OpenYear);
                writer.Write(0u); writer.Write(0u);
                writer.Write(0u); writer.Write(0u);
            }

            // 站点元数据 (20字节, name_idx 为 uint32)
            foreach (var st in stationNames)
            {
                var coord = data.Stations[st];
                int nameOff = stringOffsetMap[st];
                writer.Write((uint)nameOff);        // 改为 4 字节
                writer.Write((int)(coord.Lon * 1e6));
                writer.Write((int)(coord.Lat * 1e6));
                writer.Write(0u);
                writer.Write(0u);
            }

            // 节点表
            for (int i = 0; i < nodeCount; i++)
            {
                writer.Write(dirToLineIdx[nodeDirIds[i]]);
                writer.Write(stationIndexMap[nodeStationNames[i]]);
            }

            // 运行边
            foreach (var e in railEdges)
            {
                writer.Write(e.from);
                writer.Write(e.to);
                writer.Write(e.distCm);
                writer.Write(e.timeSec);
            }

            // 换乘边
            foreach (var e in transferEdges)
            {
                writer.Write(e.from);
                writer.Write(e.to);
                writer.Write(e.timeSec);
            }

            // 查找表
            writer.Write((ushort)lookupItems.Count);
            foreach (var item in lookupItems)
            {
                writer.Write(item.stIdx);
                writer.Write((ushort)item.nodes.Count);
                foreach (var n in item.nodes)
                    writer.Write(n);
            }

            // 票价分段
            foreach (var seg in fareSegments)
            {
                writer.Write((uint)(seg.Start * 100000));
                writer.Write((uint)(seg.End * 100000));
                writer.Write((uint)(seg.Price * 100));
            }

            // 语言表
            foreach (var off in langOffsets)
                writer.Write(off);

            // 12. 回填文件头
            ms.Seek(0, SeekOrigin.Begin);
            writer.Write((uint)0x4D42494E);          // "MBIN"
            writer.Write((byte)0x03);                // version
            writer.Write((byte)0x00);                // endian = 0x00 (小端)
            writer.Write((ushort)0);

            writer.Write((uint)strOffsetPos);
            writer.Write((uint)lineOffsetPos);
            writer.Write((uint)stationMetaOffsetPos);
            writer.Write((uint)nodeOffsetPos);
            writer.Write((uint)railEdgeOffsetPos);
            writer.Write((uint)transferEdgeOffsetPos);
            writer.Write((uint)lookupOffsetPos);
            writer.Write((uint)fareOffsetPos);
            writer.Write((uint)totalSize);

            writer.Write((ushort)displayLineCount);
            writer.Write((ushort)stationNames.Count);
            writer.Write((ushort)nodeCount);
            writer.Write((ushort)railEdges.Count);
            writer.Write((ushort)transferEdges.Count);
            writer.Write((ushort)lookupItems.Count);
            writer.Write((ushort)fareSegments.Count);

            // padding 存储语言表信息
            writer.Write((uint)langTableOffset);
            writer.Write((uint)langTableSize);
            writer.Write((ushort)langCount);

            return ms.ToArray();
        }

        private static uint ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return 0xCCCCCC;
            return uint.Parse(hex.TrimStart('#'), System.Globalization.NumberStyles.HexNumber);
        }
    }
}