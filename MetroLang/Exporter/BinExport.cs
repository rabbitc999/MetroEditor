using CsvHelper;
using CsvHelper.Configuration.Attributes;
using MetroEditor.MetroLang.JsonModel;
using Microsoft.International.Converters.PinYinConverter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MetroEditor.MetroLang.Exporter
{
    /// <summary>
    /// 将地铁线路数据（JSON + CSV）编码为自定义二进制格式（版本5）
    /// 包含站点、线路、方向、路线记录、线路顺序表（不含字库）
    /// </summary>
    public static class BinExport
    {
        /// <summary>
        /// 编码主入口
        /// </summary>
        /// <param name="root">已解析的 JsonRoot 对象（来自编译后的数据）</param>
        /// <param name="csvPath">乘车指引 CSV 文件路径</param>
        /// <param name="binPath">输出的二进制文件路径</param>
        public static void Encode(JsonRoot root, string csvPath, string binPath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding gbk = Encoding.GetEncoding(936); // GBK

            if (root == null) throw new Exception("JSON 解析失败");

            string keyCity = root.KeyCity ?? "广州";
            var stationsDict = root.Stations;
            var linesDict = root.Lines;

            var stationNames = stationsDict.Keys.ToList();
            var lineIds = linesDict.Keys.ToList();

            // ---- 读取 CSV ----
            var records = new List<CsvRecord>();
            using (var reader = new StreamReader(csvPath, Encoding.UTF8))
            using (var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture))
            {
                records = csv.GetRecords<CsvRecord>().ToList();
            }

            // ---- 收集所有方向名 ----
            var directionNames = new HashSet<string>();
            foreach (var row in records)
            {
                var segs = ExtractSegments(row.Guide, row.StartStation);
                foreach (var seg in segs)
                    directionNames.Add(seg.Direction);
            }
            var sortedDirections = directionNames.OrderBy(x => x).ToList();

            // ---- 建立 ID 映射 ----
            var stationIdMap = new Dictionary<string, int>();
            for (int i = 0; i < stationNames.Count; i++)
                stationIdMap[stationNames[i]] = i + 1;

            var lineNameMap = new Dictionary<string, int>();
            foreach (var id in lineIds)
            {
                var name = linesDict[id].Name;
                lineNameMap[name] = lineIds.IndexOf(id) + 1;
            }

            var dirNameMap = new Dictionary<string, int>();
            for (int i = 0; i < sortedDirections.Count; i++)
                dirNameMap[sortedDirections[i]] = i + 1;

            // ============================================================
            // 1. 构建站点表 (TYPE_STATION)
            // ============================================================
            var stationTable = new MemoryStream();
            var bw = new BinaryWriter(stationTable);
            foreach (var name in stationNames)
            {
                var info = stationsDict.GetValueOrDefault(name);
                double lon = 0, lat = 0;
                var lineIdsForStation = new List<string>();
                if (info != null)
                {
                    lon = info.Lon;
                    lat = info.Lat;
                    lineIdsForStation = info.Lines.Select(l => l.Id).Distinct().ToList();
                }

                var pinyin = PinyinHelper.GetPinyinInitial(name);
                var nameBytes = gbk.GetBytes(name);
                var pinyinBytes = Encoding.ASCII.GetBytes(pinyin);

                bw.Write((ushort)nameBytes.Length);
                bw.Write(nameBytes);
                bw.Write((ushort)pinyinBytes.Length);
                bw.Write(pinyinBytes);
                bw.Write((float)lon);
                bw.Write((float)lat);
                bw.Write((ushort)lineIdsForStation.Count);
                foreach (var lid in lineIdsForStation)
                {
                    if (linesDict.TryGetValue(lid, out var linfo))
                    {
                        var newId = lineNameMap.GetValueOrDefault(linfo.Name, 0);
                        bw.Write((ushort)newId);
                    }
                    else
                        bw.Write((ushort)0);
                }
            }
            var stationBytes = stationTable.ToArray();

            // ============================================================
            // 2. 构建线路表 (TYPE_LINE)
            // ============================================================
            var lineTable = new MemoryStream();
            bw = new BinaryWriter(lineTable);
            foreach (var lid in lineIds)
            {
                var linfo = linesDict[lid];
                var name = linfo.Name;
                var city = linfo.City ?? "广州";
                var color = linfo.Color ?? "#000000";
                var rgb = Convert.ToInt32(color.TrimStart('#'), 16);
                var nameBytes = gbk.GetBytes(name);
                var cityBytes = gbk.GetBytes(city);
                bw.Write((ushort)nameBytes.Length);
                bw.Write(nameBytes);
                bw.Write((uint)rgb);
                bw.Write((ushort)cityBytes.Length);
                bw.Write(cityBytes);
            }
            var lineBytes = lineTable.ToArray();

            // ============================================================
            // 3. 构建方向表 (TYPE_DIR)
            // ============================================================
            var dirTable = new MemoryStream();
            bw = new BinaryWriter(dirTable);
            foreach (var dname in sortedDirections)
            {
                var bytes = gbk.GetBytes(dname);
                bw.Write((ushort)bytes.Length);
                bw.Write(bytes);
            }
            var dirBytes = dirTable.ToArray();

            // ============================================================
            // 4. 构建路线记录表 (TYPE_RECORD)
            // ============================================================
            var recordTable = new MemoryStream();
            bw = new BinaryWriter(recordTable);
            foreach (var row in records)
            {
                var startId = stationIdMap.GetValueOrDefault(row.StartStation, 0);
                var endId = stationIdMap.GetValueOrDefault(row.EndStation, 0);
                var dist = (int)(double.Parse(row.DistanceKm) * 1000);
                var time = (int)(double.Parse(row.TimeMinutes) * 100);
                var fare = (int)(double.Parse(row.FareYuan) * 100);
                var transfers = int.Parse(row.TransferCount);
                var totalStops = int.Parse(row.StopCount);

                var segs = ExtractSegments(row.Guide, row.StartStation);
                var segCount = segs.Count;
                var segsNew = new List<(int startId, int lineId, int dirId, int stops, int endId)>();
                foreach (var seg in segs)
                {
                    var segStartId = stationIdMap.GetValueOrDefault(seg.FromStation, 0);
                    var lineId = lineNameMap.GetValueOrDefault(seg.LineName, 0);
                    var dirId = dirNameMap.GetValueOrDefault(seg.Direction, 0);
                    var segEndId = stationIdMap.GetValueOrDefault(seg.ToStation, 0);
                    segsNew.Add((segStartId, lineId, dirId, seg.Stops, segEndId));
                }

                // 记录头（18字节）
                bw.Write((ushort)startId);
                bw.Write((ushort)endId);
                bw.Write(dist);
                bw.Write((ushort)time);
                bw.Write((ushort)fare);
                bw.Write((ushort)transfers);
                bw.Write((ushort)totalStops);
                bw.Write((ushort)segCount);

                // 每个分段（10字节）
                foreach (var seg in segsNew)
                {
                    bw.Write((ushort)seg.startId);
                    bw.Write((ushort)seg.lineId);
                    bw.Write((ushort)seg.dirId);
                    bw.Write((ushort)seg.stops);
                    bw.Write((ushort)seg.endId);
                }
            }
            var recordBytes = recordTable.ToArray();

            // ============================================================
            // 5. 构建线路顺序表 (TYPE_ORDER) - 版本5
            // ============================================================
            var orderTable = new MemoryStream();
            bw = new BinaryWriter(orderTable);
            bw.Write((ushort)lineIds.Count);
            foreach (var lid in lineIds)
            {
                var linfo = linesDict[lid];
                var dirs = linfo.Direction ?? new List<JsonDirection>();
                bw.Write((byte)dirs.Count);
                foreach (var dirObj in dirs)
                {
                    var dirName = $"开往{dirObj.End}方向";
                    var dirNameBytes = gbk.GetBytes(dirName);
                    bw.Write((ushort)dirNameBytes.Length);
                    bw.Write(dirNameBytes);

                    var stationList = dirObj.Station ?? new List<JsonDirectionStation>();
                    bw.Write((ushort)stationList.Count);
                    foreach (var st in stationList)
                    {
                        var sid = stationIdMap.GetValueOrDefault(st.Name, 0);
                        bw.Write((ushort)sid);
                    }
                    foreach (var st in stationList)
                    {
                        byte status = 0;
                        if (stationsDict.TryGetValue(st.Name, out var stInfo))
                        {
                            var lineEntry = stInfo.Lines.FirstOrDefault(l => l.Id == lid);
                            if (lineEntry != null && lineEntry.Status == "pending")
                                status = 1;
                        }
                        bw.Write(status);
                    }
                }
            }
            var orderBytes = orderTable.ToArray();

            // ============================================================
            // 6. 计算偏移并写入最终文件（版本5）
            // ============================================================
            var keyCityBytes = gbk.GetBytes(keyCity);
            int headerSize = 4 + 2 + 2 + keyCityBytes.Length;
            int sectionCount = 5;
            int offsetTableSize = 2 + sectionCount * (2 + 8 + 8);
            int baseOffset = headerSize + offsetTableSize;

            int offset = baseOffset;
            var sections = new[]
            {
                (Type: 1, Offset: offset, Length: stationBytes.Length),
                (Type: 2, Offset: offset += stationBytes.Length, Length: lineBytes.Length),
                (Type: 3, Offset: offset += lineBytes.Length, Length: dirBytes.Length),
                (Type: 4, Offset: offset += dirBytes.Length, Length: recordBytes.Length),
                (Type: 5, Offset: offset += recordBytes.Length, Length: orderBytes.Length),
            };

            using (var fs = new FileStream(binPath, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(Encoding.ASCII.GetBytes("ROUT"));
                writer.Write((ushort)5);      // 版本5
                writer.Write((ushort)keyCityBytes.Length);
                writer.Write(keyCityBytes);
                writer.Write((ushort)sectionCount);

                foreach (var sec in sections)
                {
                    writer.Write((ushort)sec.Type);
                    writer.Write((ulong)sec.Offset);
                    writer.Write((ulong)sec.Length);
                }

                writer.Write(stationBytes);
                writer.Write(lineBytes);
                writer.Write(dirBytes);
                writer.Write(recordBytes);
                writer.Write(orderBytes);
            }
        }

        // ------------------ 辅助方法 ------------------
        private static List<SegmentInfo> ExtractSegments(string guide, string startStation)
        {
            var pattern = new Regex(@"开往(.*?)方向的(.*?)，坐(\d+)站.*?【(.*?)】");
            var matches = pattern.Matches(guide);
            var result = new List<SegmentInfo>();
            string current = startStation;
            foreach (Match m in matches)
            {
                var dir = m.Groups[1].Value.Trim();
                var line = m.Groups[2].Value.Trim();
                var stops = int.Parse(m.Groups[3].Value);
                var end = m.Groups[4].Value.Trim();
                result.Add(new SegmentInfo
                {
                    FromStation = current,
                    Direction = dir,
                    LineName = line,
                    Stops = stops,
                    ToStation = end
                });
                current = end;
            }
            return result;
        }

        private class SegmentInfo
        {
            public string FromStation { get; set; }
            public string Direction { get; set; }
            public string LineName { get; set; }
            public int Stops { get; set; }
            public string ToStation { get; set; }
        }
    }

    /// <summary>
    /// CSV 记录映射（必须与 CSV 文件头部完全一致）
    /// </summary>
    public class CsvRecord
    {
        [Name("起点站")]
        public string StartStation { get; set; }

        [Name("终点站")]
        public string EndStation { get; set; }

        [Name("距离(km)")]
        public string DistanceKm { get; set; }

        [Name("时间(分钟)")]
        public string TimeMinutes { get; set; }

        [Name("票价(元)")]
        public string FareYuan { get; set; }

        [Name("换乘次数")]
        public string TransferCount { get; set; }

        [Name("站数")]
        public string StopCount { get; set; }

        [Name("乘车指引")]
        public string Guide { get; set; }
    }

    /// <summary>
    /// 拼音帮助类（使用 Microsoft.International.Converters.PinYinConverter）
    /// </summary>
    public static class PinyinHelper
    {
        public static string GetPinyinInitial(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                {
                    sb.Append(char.ToUpperInvariant(c));
                    continue;
                }
                if (char.IsDigit(c) || char.IsPunctuation(c) || char.IsSymbol(c))
                {
                    sb.Append(c);
                    continue;
                }

                try
                {
                    var chineseChar = new ChineseChar(c);
                    if (chineseChar.Pinyins.Count > 0)
                    {
                        string py = chineseChar.Pinyins[0];
                        if (py.Length > 0)
                        {
                            sb.Append(char.ToUpperInvariant(py[0]));
                            continue;
                        }
                    }
                }
                catch
                {
                    // 非汉字或转换失败，保留原字符
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}