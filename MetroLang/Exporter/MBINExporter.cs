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
    public static class MBINExporter
    {
        public static byte[] Export(ParsedData data, SimMetroGraph graph)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // ===== 1. 过滤方向 =====
            var activeDirections = data.Directions
                .Where(kv => !kv.Value.IsFuture && !kv.Value.Attrs.Contains("FAST"))
                .ToList();

            // ===== 2. 构建字符串池（并记录每个字符串的偏移） =====
            var stringMap = new Dictionary<string, int>();          // 字符串 -> 索引
            var stringOffsetMap = new Dictionary<string, int>();    // 字符串 -> 偏移（字节）
            var stringList = new List<string>();                    // 索引 -> 字符串

            int AddString(string s)
            {
                if (string.IsNullOrEmpty(s)) return -1;
                if (!stringMap.TryGetValue(s, out int idx))
                {
                    idx = stringList.Count;
                    stringMap[s] = idx;
                    stringList.Add(s);
                    // 偏移在写入时计算，现在只记录索引
                }
                return idx;
            }

            // 收集所有需要放入字符串池的数据
            var allLineNames = activeDirections
                .Select(kv => new { kv.Value, PhysLine = data.Lines[kv.Value.LineId] })
                .Select(x => string.IsNullOrEmpty(x.Value.Alias) ? x.PhysLine.Name : x.Value.Alias)
                .Distinct()
                .ToList();
            foreach (var name in allLineNames) AddString(name);

            var stationNames = data.Stations.Keys.ToList();
            foreach (var st in stationNames) AddString(st);

            // 备用名称
            var altNameMap = new Dictionary<string, List<string>>();
            foreach (var kv in data.StationOtherNames)
            {
                if (kv.Value != null && kv.Value.Any())
                {
                    altNameMap[kv.Key] = kv.Value.ToList();
                    foreach (var alt in kv.Value)
                        AddString(alt);
                }
            }

            // 语言顺序
            var languageOrder = data.LanguageOrder?.Any() == true
                ? data.LanguageOrder.ToList()
                : new List<string> { "zh", "en" };
            foreach (var lang in languageOrder)
                AddString(lang);

            // 城市名（添加到字符串池，但未直接使用）
            AddString(data.MainCity);

            // 现在写入字符串池，并记录每个字符串的偏移
            long strPoolBase = 64; // 文件头后开始
            using var strMs = new MemoryStream();
            using var strWriter = new BinaryWriter(strMs);
            for (int i = 0; i < stringList.Count; i++)
            {
                stringOffsetMap[stringList[i]] = (int)strMs.Position; // 记录偏移
                byte[] bytes = Encoding.UTF8.GetBytes(stringList[i]);
                strWriter.Write(bytes);
                strWriter.Write((byte)0); // 终止符
                while (strMs.Position % 4 != 0) strWriter.Write((byte)0);
            }
            byte[] strBytes = strMs.ToArray();
            long strOffset = 64; // 从文件头后开始
            int strPoolSize = strBytes.Length;

            // ===== 3. 构建显示线路映射 =====
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

            // ===== 4. 构建节点映射 =====
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

            // ===== 5. 站点索引 =====
            var stationIndexMap = new Dictionary<string, ushort>();
            for (ushort i = 0; i < stationNames.Count; i++)
                stationIndexMap[stationNames[i]] = i;

            // ===== 6. 构建备用名称池 =====
            // 每个站点有 (languageOrder.Count - 1) 个备用名，按语言顺序存储
            int altCountPerStation = languageOrder.Count - 1;
            var altPool = new List<ushort>();
            var altOffsets = new Dictionary<string, uint>(); // 站名 -> 在该池中的偏移（字节）
            foreach (var st in stationNames)
            {
                altOffsets[st] = (uint)altPool.Count * 2;
                var alts = altNameMap.TryGetValue(st, out var list) ? list : new List<string>();
                for (int i = 0; i < altCountPerStation; i++)
                {
                    if (i < alts.Count && stringMap.TryGetValue(alts[i], out int idx))
                        altPool.Add((ushort)idx);
                    else
                        altPool.Add((ushort)stringMap[st]); // 回退到主名
                }
            }
            int altPoolCount = altPool.Count;

            // ===== 7. 构建站名->节点列表查找表 =====
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

            // ===== 8. 预计算运行边 =====
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

            // ===== 9. 预计算换乘边 =====
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
                        // 构建外部键用于换乘查询
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

            // ===== 10. 票价分段 =====
            var fareSegments = data.MoneyRules.OfType<MoneySegmentRule>().ToList();

            // ===== 11. 计算各段偏移和大小 =====
            long headerOffset = 0; // 文件头在 0
            long strOffsetPos = 64;
            long langOffsetPos = strOffsetPos + strPoolSize;
            int langTableSize = languageOrder.Count * 4;
            long lineOffsetPos = langOffsetPos + langTableSize;
            int lineTableSize = displayLineCount * 32;
            long stationMetaOffsetPos = lineOffsetPos + lineTableSize;
            int stationMetaSize = stationNames.Count * 24;
            long altPoolOffsetPos = stationMetaOffsetPos + stationMetaSize;
            int altPoolSize = altPool.Count * 2;
            long nodeOffsetPos = altPoolOffsetPos + altPoolSize;
            int nodeTableSize = nodeCount * 4;
            long railEdgeOffsetPos = nodeOffsetPos + nodeTableSize;
            int railEdgeSize = railEdges.Count * 12;
            long transferEdgeOffsetPos = railEdgeOffsetPos + railEdgeSize;
            int transferEdgeSize = transferEdges.Count * 8;
            long lookupOffsetPos = transferEdgeOffsetPos + transferEdgeSize;
            // 查找表：先写入总条目数(2字节)，然后每个条目：station_idx(2) + node_count(2) + node_ids(2*node_count)
            int lookupSize = 2;
            foreach (var item in lookupItems)
                lookupSize += 2 + 2 + item.nodes.Count * 2;
            long fareOffsetPos = lookupOffsetPos + lookupSize;
            int fareSize = fareSegments.Count * 12;
            long totalSize = fareOffsetPos + fareSize;

            // ===== 12. 写入所有数据 =====

            // 写入文件头（占位，稍后回填）
            writer.Write(new byte[64]);

            // 写入字符串池
            writer.Write(strBytes);

            // 写入语言顺序表（每个语言代码在字符串池中的偏移）
            foreach (var lang in languageOrder)
            {
                if (stringOffsetMap.TryGetValue(lang, out int off))
                    WriteBE(writer, (uint)off);
                else
                    WriteBE(writer, 0u);
            }

            // 写入线路表
            foreach (var kv in displayLineList)
            {
                var info = kv.Value;
                int nameIdx = stringMap[info.Name];
                int nameOff = stringOffsetMap[info.Name];
                WriteBE(writer, (uint)nameOff);
                WriteBE(writer, ParseColor(info.Color));
                WriteBE(writer, (ushort)info.Maxspeed);
                WriteBE(writer, (ushort)info.OpenYear);
                WriteBE(writer, 0u); WriteBE(writer, 0u);
                WriteBE(writer, 0u); WriteBE(writer, 0u);
            }

            // 写入站点元数据
            foreach (var st in stationNames)
            {
                var coord = data.Stations[st];
                int nameOff = stringOffsetMap[st];
                ushort altCount = (ushort)altCountPerStation;
                uint altOff = altOffsets[st];
                WriteBE(writer, (ushort)nameOff);
                WriteBE(writer, altCount);
                WriteBE(writer, altOff);
                WriteBE(writer, (int)(coord.Lon * 1e6));
                WriteBE(writer, (int)(coord.Lat * 1e6));
                WriteBE(writer, 0u);
                WriteBE(writer, 0u);
            }

            // 写入备用名称池
            foreach (var idx in altPool)
                WriteBE(writer, idx);

            // 写入节点表
            for (int i = 0; i < nodeCount; i++)
            {
                ushort lineIdx = dirToLineIdx[nodeDirIds[i]];
                ushort stIdx = stationIndexMap[nodeStationNames[i]];
                WriteBE(writer, lineIdx);
                WriteBE(writer, stIdx);
            }

            // 写入运行边
            foreach (var e in railEdges)
            {
                WriteBE(writer, e.from);
                WriteBE(writer, e.to);
                WriteBE(writer, e.distCm);
                WriteBE(writer, e.timeSec);
            }

            // 写入换乘边
            foreach (var e in transferEdges)
            {
                WriteBE(writer, e.from);
                WriteBE(writer, e.to);
                WriteBE(writer, e.timeSec);
            }

            // 写入查找表
            WriteBE(writer, (ushort)lookupItems.Count);
            foreach (var item in lookupItems)
            {
                WriteBE(writer, item.stIdx);
                WriteBE(writer, (ushort)item.nodes.Count);
                foreach (var n in item.nodes)
                    WriteBE(writer, n);
            }

            // 写入票价分段
            foreach (var seg in fareSegments)
            {
                WriteBE(writer, (uint)(seg.Start * 100000));
                WriteBE(writer, (uint)(seg.End * 100000));
                WriteBE(writer, (uint)(seg.Price * 100));
            }

            // ===== 13. 回填文件头 =====
            ms.Seek(0, SeekOrigin.Begin);
            WriteBE(writer, (uint)0x4D42494E); // "MBIN"
            WriteBE(writer, (byte)0x03);       // 版本 v3
            WriteBE(writer, (byte)0x01);       // 大端标志
            WriteBE(writer, (ushort)0);        // reserved0
            WriteBE(writer, (uint)strOffsetPos);
            WriteBE(writer, (uint)langOffsetPos);
            WriteBE(writer, (uint)lineOffsetPos);
            WriteBE(writer, (uint)stationMetaOffsetPos);
            WriteBE(writer, (uint)altPoolOffsetPos);
            WriteBE(writer, (uint)nodeOffsetPos);
            WriteBE(writer, (uint)railEdgeOffsetPos);
            WriteBE(writer, (uint)transferEdgeOffsetPos);
            WriteBE(writer, (uint)lookupOffsetPos);
            WriteBE(writer, (uint)fareOffsetPos);
            WriteBE(writer, (uint)totalSize);
            WriteBE(writer, (ushort)displayLineCount);
            WriteBE(writer, (ushort)stationNames.Count);
            WriteBE(writer, (ushort)nodeCount);
            WriteBE(writer, (ushort)railEdges.Count);
            WriteBE(writer, (ushort)transferEdges.Count);
            WriteBE(writer, (ushort)lookupItems.Count);
            WriteBE(writer, (ushort)fareSegments.Count);
            WriteBE(writer, (ushort)languageOrder.Count);
            WriteBE(writer, (ushort)altPoolCount);
            WriteBE(writer, (ushort)0); // reserved1

            return ms.ToArray();
        }

        // ===== 大端写入辅助 =====
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