using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.IO;
using MetroEditor.MetroLang.JsonModel;

namespace MetroEditor.MetroLang.Tool
{
    // ========================================================================
    // OSM 配置类
    // ========================================================================
    public class OsmConfig
    {
        public Dictionary<string, string> NetworkCityMap { get; set; } = new();
        public Dictionary<string, string> NetworkPrefixMap { get; set; } = new();
        public Dictionary<string, string> SpecialLineMap { get; set; } = new();
        public DefaultTimesConfig DefaultTimes { get; set; } = new();
        public List<FareRule> FareRules { get; set; } = new();

        public class DefaultTimesConfig
        {
            public double InStation { get; set; } = 8.0;
            public double OutStation { get; set; } = 10.0;
            public double Stop { get; set; } = 0.75;
        }

        public class FareRule
        {
            public string Type { get; set; }
            public double Price { get; set; }
            public double Start { get; set; }
            public double End { get; set; }
            public double DistSegment { get; set; }
            public double AddPrice { get; set; }
        }
    }

    // ========================================================================
    // OSM 数据模型
    // ========================================================================
    public class OsmRoot
    {
        [JsonPropertyName("elements")]
        public List<OsmElement> Elements { get; set; }
    }

    public class OsmElement
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("id")]
        public JsonElement Id { get; set; }

        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; set; }

        [JsonPropertyName("members")]
        public List<OsmMember> Members { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; }
    }

    public class OsmMember
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("ref")]
        public JsonElement Ref { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }
    }

    // ========================================================================
    // 线路元数据
    // ========================================================================

    /// <summary>
    /// 线路的核心元数据载体（POCO）。
    /// 剥离自复杂的 Line/LineInfo 对象，仅保留展示与基础计算所需的字段。
    /// </summary>
    public class LineMetadata
    {
        /// <summary>
        /// 线路显示颜色（十六进制 RGB，如 #FF0000）。
        /// </summary>
        public string Color { get; set; } = "#000000";

        /// <summary>
        /// 列车编组形式（如 "6A", "8B"）。
        /// </summary>
        public string Formation { get; set; } = string.Empty;

        /// <summary>
        /// 设计最高运行速度（单位：km/h）。
        /// </summary>
        public double Maxspeed { get; set; }

        /// <summary>
        /// 线路开通年份。
        /// </summary>
        public int OpenYear { get; set; }

        /// <summary>
        /// 相关外部链接（如维基百科 URL）。
        /// </summary>
        public string? Link { get; set; }
    }

    // ========================================================================
    // OSM 地铁数据提取器（完整重构版）
    // 职责：OSM JSON → MetroJsonRoot（标准中间数据）
    // ========================================================================
    public class OsmMetroExtractor
    {
        private const string RailwayKey = "railway";
        private const string StationKey = "station";
        private const string SubwayKey = "subway";
        private const string NameKey = "name";
        private const string NameZhKey = "name:zh";
        private const string NameZhHansKey = "name:zh-Hans";
        private const string NetworkKey = "network";
        private const string RouteKey = "route";
        private const string FromKey = "from";
        private const string ToKey = "to";

        private const string RailwayStationValue = "station";
        private const string SubwayRouteValue = "subway";
        private const string SubwayYesValue = "yes";

        private readonly OsmConfig _config;
        private readonly Dictionary<string, LineMetadata> _lineMetadata;
        private readonly Dictionary<string, (string Name, double Lon, double Lat)> _nodeCache = new();
        private readonly Dictionary<string, List<LineDirection>> _linesDict = new();
        private readonly Dictionary<string, StationInfo> _stationsDict = new();
        private readonly Dictionary<string, string> _lineCityMap = new();
        private string _keyCity = "广州";

        public OsmMetroExtractor(OsmConfig config, Dictionary<string, LineMetadata> metadata)
        {
            _config = config ?? new OsmConfig();
            _lineMetadata = metadata ?? new Dictionary<string, LineMetadata>();
        }

        // ====================================================================
        // 公开入口：OSM → MetroJsonRoot
        // ====================================================================
        public JsonRoot ExtractToRoot(OsmRoot osmData, string keyCity = "广州")
        {
            _keyCity = keyCity;
            _linesDict.Clear();
            _stationsDict.Clear();
            _lineCityMap.Clear();
            _nodeCache.Clear();

            // 1. 缓存所有车站节点
            CacheStationNodes(osmData);

            // 2. 收集并处理地铁线路关系
            var relations = CollectRelations(osmData);
            foreach (var kv in relations)
                ProcessLine(kv.Key, kv.Value);

            // 3. 构建 MetroJsonRoot
            return BuildMetroJsonRoot();
        }

        // ====================================================================
        // 第一步：缓存车站节点
        // ====================================================================
        private void CacheStationNodes(OsmRoot osmData)
        {
            foreach (var elem in osmData.Elements)
            {
                if (elem.Type != "node" || elem.Tags == null) continue;

                bool isStation = elem.Tags.TryGetValue(RailwayKey, out string rw) && rw == RailwayStationValue ||
                                 elem.Tags.TryGetValue(StationKey, out string st) && st == SubwayRouteValue ||
                                 elem.Tags.TryGetValue(SubwayKey, out string sw) && sw == SubwayYesValue;
                if (!isStation) continue;

                string name = elem.Tags.GetValueOrDefault(NameKey) ??
                              elem.Tags.GetValueOrDefault(NameZhKey) ??
                              elem.Tags.GetValueOrDefault(NameZhHansKey);
                if (string.IsNullOrEmpty(name) || elem.Lat == 0 || elem.Lon == 0) continue;

                string id = GetStringFromJsonElement(elem.Id);
                if (!string.IsNullOrEmpty(id))
                    _nodeCache[id] = (name, elem.Lon, elem.Lat);
            }
        }

        // ====================================================================
        // 第二步：收集地铁线路关系
        // ====================================================================
        private Dictionary<string, List<OsmElement>> CollectRelations(OsmRoot osmData)
        {
            var result = new Dictionary<string, List<OsmElement>>();
            foreach (var elem in osmData.Elements)
            {
                if (elem.Type != "relation" || elem.Tags == null) continue;
                if (!elem.Tags.TryGetValue(RouteKey, out string route) || route != SubwayRouteValue) continue;

                string name = elem.Tags.GetValueOrDefault(NameKey, "");
                string network = elem.Tags.GetValueOrDefault(NetworkKey, "");
                string lineId = ExtractLineId(name, network);
                if (string.IsNullOrEmpty(lineId)) continue;

                if (!result.TryGetValue(lineId, out var list))
                {
                    list = new List<OsmElement>();
                    result[lineId] = list;
                }
                list.Add(elem);
            }
            return result;
        }

        // ====================================================================
        // 辅助：提取线路ID
        // ====================================================================
        private string ExtractLineId(string name, string network)
        {
            if (string.IsNullOrEmpty(name)) return null;

            foreach (var kv in _config.SpecialLineMap)
                if (name.Contains(kv.Key)) return kv.Value;

            var match = Regex.Match(name, @"((\d+)号线)");
            if (match.Success)
            {
                string prefix = _config.NetworkPrefixMap.GetValueOrDefault(network, "G");
                return prefix + match.Groups[2].Value;
            }

            return name;
        }

        // ====================================================================
        // 第三步：处理单条线路
        // ====================================================================
        private void ProcessLine(string lineId, List<OsmElement> relations)
        {
            var items = new List<(OsmElement Rel, List<(string Name, double Lon, double Lat)> Ordered, HashSet<string> StationSet)>();

            foreach (var rel in relations)
            {
                if (rel.Members == null) continue;
                var ordered = new List<(string, double, double)>();

                foreach (var member in rel.Members)
                {
                    if (member.Type != "node") continue;
                    string refId = GetStringFromJsonElement(member.Ref);
                    if (string.IsNullOrEmpty(refId)) continue;
                    if (_nodeCache.TryGetValue(refId, out var node))
                        ordered.Add((node.Name, node.Lon, node.Lat));
                }

                if (ordered.Count >= 2)
                {
                    var set = new HashSet<string>(ordered.Select(n => n.Item1));
                    items.Add((rel, ordered, set));
                }
            }

            if (items.Count == 0) return;

            // 确定城市
            string network = items[0].Rel.Tags.GetValueOrDefault(NetworkKey, "");
            string city = _config.NetworkCityMap.GetValueOrDefault(network, "广州");
            if (string.IsNullOrEmpty(city))
            {
                string c = network.Replace("地铁", "");
                city = string.IsNullOrEmpty(c) ? "广州" : c;
            }
            _lineCityMap[lineId] = city;

            // 识别快慢车
            items = items.OrderByDescending(i => i.Ordered.Count).ToList();
            int n = items.Count;
            bool[] isExpress = new bool[n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    if (items[i].Ordered.Count < items[j].Ordered.Count &&
                        items[j].StationSet.IsSupersetOf(items[i].StationSet))
                    {
                        isExpress[i] = true;
                        break;
                    }
                }
            }

            var mainStations = items[0].StationSet;
            int mainCount = mainStations.Count;

            // 处理每个方向
            for (int idx = 0; idx < n; idx++)
            {
                var item = items[idx];
                var relTags = item.Rel.Tags;
                string rawName = relTags.GetValueOrDefault(NameKey, "");
                string displayName = GetLineDisplayName(rawName);

                var (start, end) = ParseStartEnd(relTags, item.Ordered);

                bool isFast = isExpress[idx];
                bool isBranch = !isFast && item.Ordered.Count < mainCount &&
                                item.StationSet.Any(s => mainStations.Contains(s));

                string first = item.Ordered.First().Name;
                string last = item.Ordered.Last().Name;
                if (string.IsNullOrEmpty(start)) start = first;
                if (string.IsNullOrEmpty(end)) end = last;

                bool isLoop = start == end || first == last;

                var stationNext = BuildStationNext(item.Ordered, isLoop);

                foreach (var (name, lon, lat) in item.Ordered)
                    AddStation(name, lon, lat, lineId, start, end);

                var dir = new LineDirection
                {
                    Name = displayName,
                    StationNext = stationNext,
                    Start = start,
                    End = end,
                    IsFast = isFast,
                    IsBranch = isBranch,
                    IsLoop = isLoop
                };

                if (!_linesDict.TryGetValue(lineId, out var dirs))
                {
                    dirs = new List<LineDirection>();
                    _linesDict[lineId] = dirs;
                }
                dirs.Add(dir);
            }
        }

        // ====================================================================
        // 辅助方法
        // ====================================================================
        private static string GetStringFromJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                _ => element.ToString()
            };
        }

        private List<(string Station, double? Next)> BuildStationNext(
            List<(string Name, double Lon, double Lat)> ordered, bool isLoop)
        {
            var result = new List<(string, double?)>();

            if (isLoop)
            {
                var list = ordered;
                if (list.Count > 0 && list.First().Name == list.Last().Name)
                    list = list.Take(list.Count - 1).ToList();
                int count = list.Count;

                for (int i = 0; i < count; i++)
                {
                    int nextIdx = (i + 1) % count;
                    double dist = Haversine(list[i].Lon, list[i].Lat, list[nextIdx].Lon, list[nextIdx].Lat);
                    result.Add((list[i].Name, dist));
                }
            }
            else
            {
                int total = ordered.Count;
                for (int i = 0; i < total; i++)
                {
                    double? dist = i < total - 1
                        ? Haversine(ordered[i].Lon, ordered[i].Lat, ordered[i + 1].Lon, ordered[i + 1].Lat)
                        : null;
                    result.Add((ordered[i].Name, dist));
                }
            }
            return result;
        }

        private void AddStation(string name, double lon, double lat, string lineId, string start, string end)
        {
            if (!_stationsDict.TryGetValue(name, out var info))
            {
                info = new StationInfo { Lon = lon, Lat = lat };
                _stationsDict[name] = info;
            }
            info.Lines.Add((lineId, start, end));
        }

        private string GetLineDisplayName(string rawName)
        {
            var match = Regex.Match(rawName, @"((\d+)号线)");
            if (match.Success)
                return match.Groups[1].Value + "号线";

            foreach (var kv in _config.SpecialLineMap)
                if (rawName.Contains(kv.Key))
                    return kv.Key;

            return rawName;
        }

        private (string start, string end) ParseStartEnd(Dictionary<string, string> tags,
                                                         List<(string Name, double Lon, double Lat)> ordered)
        {
            string from = tags.GetValueOrDefault(FromKey, "");
            string to = tags.GetValueOrDefault(ToKey, "");
            if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                return (from, to);

            string name = tags.GetValueOrDefault(NameKey, "");
            int colon = name.IndexOf('：');
            if (colon >= 0)
            {
                string route = name.Substring(colon + 1);
                int arrow = route.IndexOf('→');
                if (arrow >= 0)
                {
                    string startStation = route.Substring(0, arrow).Trim();
                    string endStation = route.Substring(arrow + 1).Trim();
                    if (!string.IsNullOrEmpty(startStation) && !string.IsNullOrEmpty(endStation))
                        return (startStation, endStation);
                }
            }
            return (null, null);
        }

        private static double Haversine(double lon1, double lat1, double lon2, double lat2)
        {
            const double R = 6371.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        // ====================================================================
        // 构建 MetroJsonRoot
        // ====================================================================
        private JsonRoot BuildMetroJsonRoot()
        {
            var root = new JsonRoot
            {
                KeyCity = _keyCity,
                Version = "2.0",
                UpdateDate = DateTime.Now.ToString("yyyy-MM-dd"),
                CreateDate = DateTime.Now.ToString("yyyy-MM-dd"),
                Author = "OSM Import",
                OperationParams = new JsonOperationParams
                {
                    InnerTransferTime = (int)(_config.DefaultTimes.InStation * 60),
                    OutStationTransferTime = (int)(_config.DefaultTimes.OutStation * 60),
                    DwellTime = (int)(_config.DefaultTimes.Stop * 60),
                    EntryTime = 120
                },
                Stations = BuildStationDictionary(),
                Lines = BuildLineDictionary(),
                Price = BuildPriceConfig()
            };

            return root;
        }

        private Dictionary<string, JsonStation> BuildStationDictionary()
        {
            var result = new Dictionary<string, JsonStation>();
            foreach (var kv in _stationsDict)
            {
                var name = kv.Key;
                var info = kv.Value;
                result[name] = new JsonStation
                {
                    Lon = info.Lon,
                    Lat = info.Lat,
                    City = info.Lines.Select(l => _lineCityMap.GetValueOrDefault(l.LineId, _keyCity)).FirstOrDefault() ?? _keyCity,
                    Lines = info.Lines.Select(line => new JsonStationLine
                    {
                        Id = line.LineId,
                        Start = line.Start,
                        End = line.End
                    }).ToList()
                };
            }
            return result;
        }

        private Dictionary<string, JsonLine> BuildLineDictionary()
        {
            var result = new Dictionary<string, JsonLine>();
            foreach (var kv in _linesDict)
            {
                var lineId = kv.Key;
                var directions = kv.Value;
                if (directions.Count == 0) continue;

                var firstDir = directions.First();
                var meta = _lineMetadata.GetValueOrDefault(lineId, new LineMetadata());
                var city = _lineCityMap.GetValueOrDefault(lineId, _keyCity);

                var line = new JsonLine
                {
                    Name = firstDir.Name,
                    Color = string.IsNullOrEmpty(meta.Color) ? "#CCCCCC" : meta.Color,
                    City = city,
                    Formation = meta.Formation ?? "",
                    Maxspeed = meta.Maxspeed,
                    OpenYear = meta.OpenYear,
                    Link = meta.Link ?? "",
                    Direction = directions.Select(d => BuildDirection(d)).ToList()
                };

                result[lineId] = line;
            }
            return result;
        }

        private JsonDirection BuildDirection(LineDirection dir)
        {
            return new JsonDirection
            {
                Start = dir.StationNext.First().Station,
                End = dir.StationNext.Last().Station,
                IsLoop = dir.IsLoop,
                IsBranch = dir.IsBranch,
                IsFast = dir.IsFast,
                Station = dir.StationNext.Select((s, idx) => new JsonDirectionStation
                {
                    Name = s.Station,
                    Next = s.Next
                }).ToList()
            };
        }

        private JsonPrice BuildPriceConfig()
        {
            var price = new JsonPrice();
            var rules = _config.FareRules;
            if (rules == null || rules.Count == 0)
            {
                price.DefaultFare = 2.0;
                price.Segments = new List<JsonSegment>
                {
                    new() { RangeStart = 0, RangeEnd = 4, Fare = 2 },
                    new() { RangeStart = 4, RangeEnd = 8, Fare = 3 },
                    new() { RangeStart = 8, RangeEnd = 12, Fare = 4 },
                    new() { RangeStart = 12, RangeEnd = 18, Fare = 5 },
                    new() { RangeStart = 18, RangeEnd = 24, Fare = 6 },
                    new() { RangeStart = 24, RangeEnd = 32, Fare = 7 },
                    new() { RangeStart = 32, RangeEnd = 40, Fare = 8 },
                    new() { RangeStart = 40, RangeEnd = 48, Fare = 9 },
                    new() { RangeStart = 48, RangeEnd = 56, Fare = 10 },
                    new() { RangeStart = 56, RangeEnd = 64, Fare = 11 },
                    new() { RangeStart = 64, RangeEnd = 72, Fare = 12 },
                    new() { RangeStart = 72, RangeEnd = 80, Fare = 13 },
                    new() { RangeStart = 80, RangeEnd = 88, Fare = 14 },
                    new() { RangeStart = 88, RangeEnd = 96, Fare = 15 },
                    new() { RangeStart = 96, RangeEnd = 104, Fare = 16 }
                };
                price.LinearExtension = new JsonLinearExtension
                {
                    Threshold = 104,
                    BaseFare = 16,
                    RatePerKm = 0.125
                };
                return price;
            }

            var startRule = rules.FirstOrDefault(r => r.Type == "start");
            if (startRule != null) price.DefaultFare = startRule.Price;

            var segmentRules = rules.Where(r => r.Type == "segment").ToList();
            foreach (var seg in segmentRules)
            {
                price.Segments.Add(new JsonSegment
                {
                    RangeStart = seg.Start,
                    RangeEnd = seg.End,
                    Fare = seg.Price
                });
            }

            var calcRule = rules.FirstOrDefault(r => r.Type == "calc");
            if (calcRule != null)
            {
                double threshold = segmentRules.Any() ? segmentRules.Last().End : 0;
                double baseFare = segmentRules.Any() ? segmentRules.Last().Price : startRule?.Price ?? 2.0;
                price.LinearExtension = new JsonLinearExtension
                {
                    Threshold = threshold,
                    BaseFare = baseFare,
                    RatePerKm = calcRule.AddPrice / calcRule.DistSegment
                };
            }

            return price;
        }

        // ====================================================================
        // 内部数据类
        // ====================================================================
        private class LineDirection
        {
            public string Name { get; set; }
            public List<(string Station, double? Next)> StationNext { get; set; } = new();
            public string Start { get; set; }
            public string End { get; set; }
            public bool IsFast { get; set; }
            public bool IsBranch { get; set; }
            public bool IsLoop { get; set; }
        }

        private class StationInfo
        {
            public double Lon { get; set; }
            public double Lat { get; set; }
            public HashSet<(string LineId, string Start, string End)> Lines { get; set; } = new();
        }
    }

    // ========================================================================
    // 配置加载器
    // ========================================================================
    public static class ConfigLoader
    {
        public static OsmConfig LoadConfig(string configPath)
        {
            var defaultConfig = CreateDefaultConfig();

            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                return defaultConfig;

            string json = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var userConfig = JsonSerializer.Deserialize<OsmConfig>(json, options);
            if (userConfig == null) return defaultConfig;

            if (userConfig.NetworkCityMap?.Any() == true)
                defaultConfig.NetworkCityMap = userConfig.NetworkCityMap;
            if (userConfig.NetworkPrefixMap?.Any() == true)
                defaultConfig.NetworkPrefixMap = userConfig.NetworkPrefixMap;
            if (userConfig.SpecialLineMap?.Any() == true)
                defaultConfig.SpecialLineMap = userConfig.SpecialLineMap;
            if (userConfig.DefaultTimes != null)
                defaultConfig.DefaultTimes = userConfig.DefaultTimes;
            if (userConfig.FareRules?.Any() == true)
                defaultConfig.FareRules = userConfig.FareRules;

            return defaultConfig;
        }

        public static Dictionary<string, LineMetadata> LoadMetadata(string metaPath)
        {
            var result = new Dictionary<string, LineMetadata>();
            if (string.IsNullOrEmpty(metaPath) || !File.Exists(metaPath))
                return result;

            string json = File.ReadAllText(metaPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dict = JsonSerializer.Deserialize<Dictionary<string, LineMetadata>>(json, options);
            if (dict != null) result = dict;
            return result;
        }

        private static OsmConfig CreateDefaultConfig()
        {
            return new OsmConfig
            {
                NetworkCityMap = new Dictionary<string, string>
                {
                    {"佛山地铁", "佛山"}, {"广州地铁", "广州"}, {"北京地铁", "北京"},
                    {"南京地铁", "南京"}, {"上海地铁", "上海"}, {"深圳地铁", "深圳"}
                },
                NetworkPrefixMap = new Dictionary<string, string>
                {
                    {"佛山地铁", "F"}, {"广州地铁", "G"}, {"北京地铁", "B"},
                    {"上海地铁", "S"}, {"深圳地铁", "SZ"}, {"南京地铁", "NJ"}
                },
                SpecialLineMap = new Dictionary<string, string>
                {
                    {"广佛线", "GF"}, {"八通线", "BT"}, {"大兴线", "DX"},
                    {"房山线", "FS"}, {"昌平线", "CP"}, {"亦庄线", "YZ"},
                    {"燕房线", "YF"}, {"S1线", "S1"}, {"西郊线", "XJ"},
                    {"亦庄T1线", "T1"}, {"首都机场线", "SD"}, {"大兴机场线", "DXJ"},
                    {"机场线", "JC"}, {"磁浮", "CF"}, {"浦江线", "PJ"},
                    {"南京地铁S1", "NJ-S1"}, {"南京地铁S2", "NJ-S2"},
                    {"南京地铁S3", "NJ-S3"}, {"南京地铁S7", "NJ-S8"},
                    {"南京地铁S10", "NJ-S10"}
                },
                DefaultTimes = new OsmConfig.DefaultTimesConfig { InStation = 8.0, OutStation = 10.0, Stop = 0.75 },
                FareRules = new List<OsmConfig.FareRule>
                {
                    new() { Type = "start", Price = 2.0 },
                    new() { Type = "segment", Start = 0, End = 4, Price = 2 },
                    new() { Type = "segment", Start = 4, End = 8, Price = 3 },
                    new() { Type = "segment", Start = 8, End = 12, Price = 4 },
                    new() { Type = "segment", Start = 12, End = 18, Price = 5 },
                    new() { Type = "segment", Start = 18, End = 24, Price = 6 },
                    new() { Type = "segment", Start = 24, End = 32, Price = 7 },
                    new() { Type = "segment", Start = 32, End = 40, Price = 8 },
                    new() { Type = "segment", Start = 40, End = 48, Price = 9 },
                    new() { Type = "segment", Start = 48, End = 56, Price = 10 },
                    new() { Type = "segment", Start = 56, End = 64, Price = 11 },
                    new() { Type = "segment", Start = 64, End = 72, Price = 12 },
                    new() { Type = "segment", Start = 72, End = 80, Price = 13 },
                    new() { Type = "segment", Start = 80, End = 88, Price = 14 },
                    new() { Type = "segment", Start = 88, End = 96, Price = 15 },
                    new() { Type = "segment", Start = 96, End = 104, Price = 16 },
                    new() { Type = "calc", DistSegment = 8, AddPrice = 1 }
                }
            };
        }
    }
}