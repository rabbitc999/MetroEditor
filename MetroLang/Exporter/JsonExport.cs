using MetroEditor.MetroLang.Compiler;
using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;

namespace MetroEditor.MetroLang.Exporter
{
    /// <summary>
    /// JSON 导出器，将编译后的内部数据模型 <see cref="Context"/> 映射为标准 <see cref="JsonRoot"/> 结构。
    /// <para>核心职责：整合坐标、线路归属、换乘规则、城市推断等信息，生成可序列化的 JSON 路网数据。</para>
    /// </summary>
    public class JsonExporter
    {
        private Context context;

        /// <summary>
        /// 初始化导出器实例。
        /// </summary>
        /// <param name="context">编译后的上下文数据容器。</param>
        public JsonExporter(Context context)
        {
            this.context = context;
        }

        /// <summary>
        /// 将编译器内部数据模型导出为标准 JSON 路网数据根对象
        /// 这是编译流程的最终阶段，将解析后的中间态转换为可序列化的结构化数据
        /// </summary>
        /// <returns>包含所有路网信息的 MetroJsonRoot 对象</returns>
        /// <exception cref="InvalidOperationException">当未定义主城市时抛出</exception>
        public JsonRoot ExportJson()
        {
            // 前置校验：主城市是路网数据的核心标识，必须存在
            if (string.IsNullOrEmpty(context.MainCity))
                throw new InvalidOperationException("未定义主城市");

            Parser parser = new Parser(new List<Token>());
            PostProcessor postProcessor = new PostProcessor(parser, context);
            postProcessor.RemoveUnusedStations();

            // 构建 JSON 根对象，逐项映射编译中间态数据
            var root = new JsonRoot
            {
                // 基础元数据
                KeyCity = context.MainCity,
                Version = context.Version,
                UpdateDate = context.UpdateDate,
                CreateDate = context.CreateDate,
                Author = context.Author,

                // 运营参数：从默认时间字典中提取，提供合理的默认值
                OperationParams = new JsonOperationParams
                {
                    InnerTransferTime = (int)context.DefaultTimes.GetValueOrDefault("in_station", 150),
                    DwellTime = (int)context.DefaultTimes.GetValueOrDefault("dwell", 30),
                    OutStationTransferTime = (int)context.DefaultTimes.GetValueOrDefault("out_station", 300),
                    EntryTime = (int)context.DefaultTimes.GetValueOrDefault("entry", 120)
                },

                // 调用独立构建方法生成复杂结构
                Price = BuildPrice(),
                Stations = BuildStations(),
                Lines = BuildLines(),

                // 发车间隔规则：将内部 TimeSpan 转换为标准时间字符串
                HeadwayRules = context.HeadwayRules.Select(r => new JsonHeadwayRule
                {
                    LineId = r.LineId,
                    StartTime = r.StartTime.ToString(@"hh\:mm"),
                    EndTime = r.EndTime.ToString(@"hh\:mm"),
                    IntervalSeconds = r.IntervalSeconds,
                    // 仅在有数据时才生成 Days 列表，减少 JSON 体积
                    Days = r.Days.Any() ? r.Days.Select(d => d.ToString()).ToList() : null
                }).ToList(),

                // 车站运营模式规则
                StationModes = context.StationModes.Select(r => new JsonStationModeRule
                {
                    Station = r.Station,
                    Mode = r.Mode,
                    StartTime = r.StartTime?.ToString(@"hh\:mm"),
                    EndTime = r.EndTime?.ToString(@"hh\:mm"),
                    Days = r.Days.Any() ? r.Days.Select(d => d.ToString()).ToList() : null
                }).ToList(),

                // 拥挤度规则：需要将内部方向 ID 转换为外部键
                CrowdRules = context.CrowdRules.Select(r => new JsonCrowdRule
                {
                    DirectionKey = r.DirectionKey,
                    StartTime = r.StartTime.ToString(@"hh\:mm"),
                    EndTime = r.EndTime.ToString(@"hh\:mm"),
                    Factor = r.Factor,
                    Days = r.Days.Any() ? r.Days.Select(d => d.ToString()).ToList() : null
                }).ToList(),

                // 运营时间规则
                OpenTimes = context.OpenTimes.Select(r => new JsonOpenTimeRule
                {
                    LineId = r.LineId,
                    StartTime = r.StartTime.ToString(@"hh\:mm"),
                    EndTime = r.EndTime.ToString(@"hh\:mm"),
                    Days = r.Days.Any() ? r.Days.Select(d => d.ToString()).ToList() : null
                }).ToList(),

                // 车辆段信息：方向键需要转换
                Depots = context.Depots.Select(r => new JsonDepotInfo
                {
                    LineId = r.LineId,
                    Station = r.Station,
                    DirectionKey = r.DirectionKey,
                    DepartureTime = r.DepartureTime?.ToString(@"hh\:mm")
                }).ToList(),

                // 区间限速信息：方向键需要转换
                SpeedLimits = context.SpeedLimits.Select(r => new JsonSpeedLimit
                {
                    DirectionKey = r.DirectionKey,
                    FromStation = r.FromStation,
                    ToStation = r.ToStation,
                    SpeedKmh = r.SpeedKmh
                }).ToList(),

                // 虚拟换乘规则
                VirtualTransfers = context.VirtualTransfers.Select(r => new JsonVirtualTransferRule
                {
                    StationA = r.StationA,
                    StationB = r.StationB,
                    TimeMinutes = r.TimeMinutes,
                    SecurityCheck = r.SecurityCheck,
                    // 空列表转为 null，保持 JSON 整洁
                    ViaDirections = r.ViaDirections.Any() ? r.ViaDirections : null
                }).ToList(),

                // 地图标注直接传递
                Labels = context.Labels.Select(l => new JsonLabel
                {
                    Text = l.Text,
                    Lon = l.Lon,
                    Lat = l.Lat
                }).ToList(),

                Facilities = context.Facilities.Any()
                    ? context.Facilities.ToDictionary(
                        kv => kv.Key,
                        kv => MapToJsonStationFacilities(kv.Value)
                      )
                    : null,

                Promotions = context.Promotions.Any()
                    ? context.Promotions.Select(MapToJsonPromotionPolicy).ToList()
                    : null,

                LanguageOrder = context.LanguageOrder.Any() ? context.LanguageOrder : null,

                Zones = context.Zones.Any()
                    ? context.Zones.Select(kv => new JsonZone
                    {
                        Id = kv.Key,
                        Name = kv.Value.Name,
                        Type = kv.Value.Type,
                        City = kv.Value.City,
                        Color = kv.Value.Color,
                        Center = kv.Value.Center,
                        Radius = kv.Value.Radius,
                        Polygon = kv.Value.Polygon,
                        Stations = kv.Value.Stations,
                        Attributes = kv.Value.Attributes != null ? new JsonZoneAttributes
                        {
                            PeakHours = kv.Value.Attributes.PeakHours,
                            PopulationDensity = kv.Value.Attributes.PopulationDensity,
                            AvgRent = kv.Value.Attributes.AvgRent
                        } : null
                    }).ToList()
                    : null,

                OperatorCompanies = context.OperatorCompanies.Any()
                    ? context.OperatorCompanies.Select(kv => new JsonOperatorCompany
                    {
                        Id = kv.Key,
                        Name = kv.Value.Name,
                        ShortName = kv.Value.ShortName,
                        EnName = kv.Value.EnName,
                        Country = kv.Value.Country,
                        City = kv.Value.City,
                        Founded = kv.Value.Founded,
                        Website = kv.Value.Website,
                        Hotline = kv.Value.Hotline,
                        Description = kv.Value.Description,
                        LogoBase64 = kv.Value.LogoBase64,
                        Lines = kv.Value.Lines
                    }).ToList()
                    : null,

                Backgrounds = context.Backgrounds.Any()
                    ? context.Backgrounds.Select(kv => new JsonBackground
                    {
                        Id = kv.Key,
                        Name = kv.Value.Name,
                        Type = kv.Value.Type,
                        Src = kv.Value.SrcBase64,
                        Bounds = kv.Value.Bounds,
                        Opacity = kv.Value.Opacity
                    }).ToList()
                    : null,
            };

            // 补充 ViaDirections 到 Direction 对象
            // 由于 JSON 结构中的 Direction 不直接包含 ViaDirections，
            // 需要通过匹配线路ID和起止站来关联内部方向信息
            foreach (var lineKv in root.Lines)
            {
                var line = lineKv.Value;
                for (int i = 0; i < line.Direction.Count; i++)
                {
                    var dir = line.Direction[i];
                    // 在内部方向集合中查找匹配的项：
                    // 1. 线路ID相同
                    // 2. 起点站相同（使用 FirstOrDefault 安全访问）
                    // 3. 终点站相同（使用 LastOrDefault 安全访问）
                    var dirInfo = context.Directions.Values.FirstOrDefault(d =>
                        d.LineId == lineKv.Key &&
                        d.Stations.FirstOrDefault() == dir.Start &&
                        d.Stations.LastOrDefault() == dir.End);

                    // 如果找到匹配的方向且包含经由信息，则回填到 JSON 对象中
                    if (dirInfo != null && dirInfo.ViaDirections.Count > 0)
                        dir.ViaDirections = dirInfo.ViaDirections;

                    // 补充 SegmentTypes
                    if (dirInfo != null && dirInfo.SegmentTypes.Count > 0)
                        dir.SegmentTypes = dirInfo.SegmentTypes.Select(s => new JsonSegmentType
                        {
                            From = s.FromStation,
                            To = s.ToStation,
                            Type = s.Type.ToLowerInvariant()
                        }).ToList();
                }
            }

            return root;
        }

        /// <summary>
        /// 将 DataModel.ToiletInfo 映射为 JsonToiletInfo
        /// </summary>
        private JsonToiletInfo MapToJsonToiletInfo(ToiletInfo? source)
        {
            if (source == null) return null;
            return new JsonToiletInfo
            {
                Position = source.Position,
                Accessible = source.Accessible,
                Gender = source.Gender
            };
        }

        /// <summary>
        /// 将 DataModel.ElevatorInfo 映射为 JsonElevatorInfo
        /// </summary>
        private JsonElevatorInfo MapToJsonElevatorInfo(ElevatorInfo source)
        {
            return new JsonElevatorInfo
            {
                Location = source.Location,
                Accessible = source.Accessible
            };
        }

        /// <summary>
        /// 将 DataModel.EscalatorInfo 映射为 JsonEscalatorInfo
        /// </summary>
        private JsonEscalatorInfo MapToJsonEscalatorInfo(EscalatorInfo source)
        {
            return new JsonEscalatorInfo
            {
                Quantity = source.Quantity,
                Direction = source.Direction
            };
        }

        /// <summary>
        /// 将 DataModel.StationFacilities 映射为 JsonStationFacilities
        /// </summary>
        private JsonStationFacilities MapToJsonStationFacilities(StationFacilities source)
        {
            return new JsonStationFacilities
            {
                Toilet = MapToJsonToiletInfo(source.Toilet),
                AccessibleToilet = MapToJsonToiletInfo(source.AccessibleToilet),
                Elevators = source.Elevators?.Select(MapToJsonElevatorInfo).ToList(),
                WideGate = source.WideGate != null ? new JsonWideGateInfo { Quantity = source.WideGate.Quantity } : null,
                Escalators = source.Escalators?.Select(MapToJsonEscalatorInfo).ToList(),
                TactilePaving = source.TactilePaving != null ? new JsonTactilePavingInfo { Status = source.TactilePaving.Status } : null
            };
        }

        /// <summary>
        /// 将 DataModel.PromotionPolicy 映射为 JsonPromotionPolicy
        /// </summary>
        private JsonPromotionPolicy MapToJsonPromotionPolicy(PromotionPolicy source)
        {
            return new JsonPromotionPolicy
            {
                Id = source.Id,
                Name = source.Name,
                Description = source.Description,
                Type = source.Type,
                Value = source.Value,
                ApplicableLines = source.ApplicableLines.Any() ? source.ApplicableLines : null,
                StartTime = source.StartTime,
                EndTime = source.EndTime,
                Days = source.Days.Any() ? source.Days : null
            };
        }

        /// <summary>
        /// 将 DataModel.Segment 映射为 JsonSegment
        /// </summary>
        private JsonSegment MapToJsonSegment(Segment source)
        {
            return new JsonSegment
            {
                RangeStart = source.RangeStart,
                RangeEnd = source.RangeEnd,
                Fare = source.Fare
            };
        }

        /// <summary>
        /// 将 DataModel.LinearExtension 映射为 JsonLinearExtension
        /// </summary>
        private JsonLinearExtension MapToJsonLinearExtension(LinearExtension? source)
        {
            if (source == null) return new JsonLinearExtension();
            return new JsonLinearExtension
            {
                Threshold = source.Threshold,
                BaseFare = source.BaseFare,
                RatePerKm = source.RatePerKm
            };
        }

        /// <summary>
        /// 将 DataModel.SurchargeItem 映射为 JsonSurchargeItem
        /// </summary>
        private JsonSurchargeItem MapToJsonSurchargeItem(SurchargeItem source)
        {
            return new JsonSurchargeItem
            {
                NodeA = source.NodeA,
                NodeB = source.NodeB,
                Price = source.Price
            };
        }

        /// <summary>
        /// 将 DataModel.SpecialLine 映射为 JsonSpecialLine
        /// </summary>
        private JsonSpecialLine MapToJsonSpecialLine(SpecialLine source)
        {
            return new JsonSpecialLine
            {
                LineId = source.LineId,
                All = source.All,
                Surcharge = source.Surcharge.Any() ? source.Surcharge.Select(MapToJsonSurchargeItem).ToList() : null,
                UseLines = source.UseLines.Any() ? source.UseLines : null,
                DefaultFare = source.DefaultFare,
                Segments = source.Segments.Any() ? source.Segments.Select(MapToJsonSegment).ToList() : null,
                LinearExtension = source.LinearExtension != null ? MapToJsonLinearExtension(source.LinearExtension) : null
            };
        }

        /// <summary>
        /// 构建票价配置对象
        /// 将内部 PriceConfig 转换为 JSON 序列化所需的 Price 对象
        /// </summary>
        /// <returns>包含完整票价规则的 Price 对象</returns>
        private JsonPrice BuildPrice()
        {
            return BuildPriceFromConfig(context.PriceConfig);
        }

        /// <summary>
        /// 从指定的配置对象构建票价配置
        /// 处理默认值、空值合并和数据结构转换
        /// </summary>
        /// <param name="config">内部票价配置对象</param>
        /// <returns>JSON 序列化用的 Price 对象</returns>
        private JsonPrice BuildPriceFromConfig(PriceConfig config)
        {
            return new JsonPrice
            {
                Currency = string.IsNullOrEmpty(config.Currency) ? "CNY" : config.Currency,
                DefaultFare = config.DefaultFare > 0 ? config.DefaultFare : 2.0,
                Segments = config.Segments.Select(MapToJsonSegment).ToList(),
                LinearExtension = MapToJsonLinearExtension(config.LinearExtension),
                TransferDiscount = config.TransferDiscount != null
                    ? new JsonTransferDiscount
                    {
                        Enabled = config.TransferDiscount.Enabled,
                        DiscountAmount = config.TransferDiscount.DiscountAmount,
                        TimeLimitMinutes = config.TransferDiscount.TimeLimitMinutes
                    }
                    : new JsonTransferDiscount(),
                FareCap = config.FareCap,
                SpecialLines = config.SpecialLines.Select(MapToJsonSpecialLine).ToList()
            };
        }

        /// <summary>
        /// 构建车站字典：将内部车站数据转换为 JSON 结构
        /// 核心方法，负责整合坐标、线路归属、换乘规则、城市推断等信息
        /// </summary>
        /// <returns>车站名称到 Station 对象的映射字典</returns>
        private Dictionary<string, JsonStation> BuildStations()
        {
            // ===== 第一阶段：构建车站-方向映射 =====
            // 记录每个车站被哪些线路和方向经过
            var stationLineDirs = new Dictionary<string, HashSet<(string LineId, string Start, string End)>>();
            foreach (var direction in context.Directions.Values)
            {
                var stations = direction.Stations;
                if (stations.Count == 0) continue;

                // 获取方向的起止站
                string start = stations.First(), end = stations.Last();
                foreach (var station in stations)
                {
                    if (!stationLineDirs.TryGetValue(station, out var directionSet))
                        stationLineDirs[station] = directionSet = new HashSet<(string, string, string)>();
                    // 记录线路ID和起止站信息
                    directionSet.Add((direction.LineId, start, end));
                }
            }

            // ===== 第二阶段：构建换乘规则映射 =====
            var transferMap = new Dictionary<string, Dictionary<string, object>>();
            foreach (var transferRule in context.Transfers)
            {
                // 生成双向换乘键（线路A-线路B 和 线路B-线路A）
                string key1 = $"{transferRule.L1}|{transferRule.L2}";
                string key2 = $"{transferRule.L2}|{transferRule.L1}";

                foreach (var key in new[] { key1, key2 })
                {
                    if (!transferMap.TryGetValue(transferRule.Station, out var transferDict))
                        transferMap[transferRule.Station] = transferDict = new Dictionary<string, object>();

                    // 构建换乘详情对象
                    var transferDetails = new Dictionary<string, object> { ["status"] = transferRule.Status };
                    if (transferRule.Time.HasValue) transferDetails["time"] = transferRule.Time.Value;
                    if (transferRule.SecurityCheck) transferDetails["security_check"] = true;
                    if (!string.IsNullOrEmpty(transferRule.TransferType))
                        transferDetails["transfer_type"] = transferRule.TransferType;

                    transferDict[key] = transferDetails;
                }
            }

            // ===== 第三阶段：构建最终车站对象 =====
            var result = new Dictionary<string, JsonStation>();
            foreach (var (stationName, (longitude, latitude)) in context.Stations)
            {
                // ---- 城市智能识别逻辑 ----
                string city;
                if (context.StationCity.TryGetValue(stationName, out var explicitlyDefinedCity))
                {
                    // 情况1：已在 STATION_DEFINE 中显式定义城市
                    city = explicitlyDefinedCity;
                }
                else
                {
                    // 情况2：从途经线路的所属城市推断
                    var candidateCities = new HashSet<string>();
                    if (stationLineDirs.TryGetValue(stationName, out var lineDirectionSet))
                    {
                        foreach (var (lineId, _, _) in lineDirectionSet)
                        {
                            if (context.Lines.TryGetValue(lineId, out var lineInfo) &&
                                !string.IsNullOrEmpty(lineInfo.City))
                                candidateCities.Add(lineInfo.City);
                        }
                    }

                    // 根据候选城市数量决定最终城市
                    if (candidateCities.Count == 1)
                    {
                        // 所有线路属于同一城市
                        city = candidateCities.First();
                    }
                    else if (candidateCities.Count > 1)
                    {
                        // 线路跨城市：使用主城市并发出警告
                        city = context.MainCity;
                        context.Warnings.Add($"站点 '{stationName}' 途经线路城市不一致 ({string.Join(", ", candidateCities)})，将使用主城市 '{context.MainCity}'");
                    }
                    else
                    {
                        // 无线路信息：回退到主城市
                        city = context.MainCity;
                    }
                }

                // 创建车站对象
                var stationEntry = new JsonStation
                {
                    Lon = longitude,
                    Lat = latitude,
                    Lines = new List<JsonStationLine>(),
                    City = city  // 使用推断后的城市
                };

                // 添加线路归属信息
                if (stationLineDirs.TryGetValue(stationName, out var lineDirectionSet2))
                {
                    foreach (var (lineId, start, end) in lineDirectionSet2)
                    {
                        var stationLine = new JsonStationLine { Id = lineId, Start = start, End = end };
                        // 添加车站状态（如 pending/operating）
                        if (context.StationStatus.TryGetValue((stationName, lineId), out var status))
                            stationLine.Status = status;

                        // 添加平台坐标
                        if (context.PlatformCoords.TryGetValue((stationName, lineId), out var platformCoord))
                        {
                            stationLine.PlatformCoord = new JsonCoord
                            {
                                Lon = platformCoord.Lon,
                                Lat = platformCoord.Lat
                            };
                        }

                        stationEntry.Lines.Add(stationLine);
                    }
                }

                // 添加换乘规则
                if (transferMap.TryGetValue(stationName, out var transfers))
                    stationEntry.Transfers = transfers;

                stationEntry.OtherNames = context.StationOtherNames.TryGetValue(stationName, out var names) ? names : null;

                result[stationName] = stationEntry;
            }
            return result;
        }

        /// <summary>
        /// 构建线路字典：将内部线路数据转换为 JSON 结构
        /// 核心方法，负责整合线路属性和方向定义
        /// </summary>
        /// <returns>线路ID到 Line 对象的映射字典</returns>
        private Dictionary<string, JsonLine> BuildLines()
        {
            var result = new Dictionary<string, JsonLine>();
            foreach (var (lineId, lineInfo) in context.Lines)
            {
                // 创建线路基础对象
                var line = new JsonLine
                {
                    Color = lineInfo.Color,
                    Formation = lineInfo.Formation ?? "",
                    OpenYear = lineInfo.OpenYear,
                    City = lineInfo.City,
                    Maxspeed = lineInfo.Maxspeed ?? 0,
                    Link = context.LineLinks.GetValueOrDefault(lineId, ""),
                    Name = lineInfo.Name,
                    PowerKw = lineInfo.PowerKw,
                    Brake = lineInfo.Brake,
                    Disabled = lineInfo.Disabled,
                    Direction = new List<JsonDirection>(),
                    IsSpecial = context.SpecialLineIds.Contains(lineId),
                    ImageBase64 = lineInfo.ImageBase64,
                };

                // 添加该线路下的所有方向定义
                foreach (var (directionId, directionInfo) in context.Directions)
                {
                    // 只处理属于当前线路的方向
                    if (directionInfo.LineId != lineId) continue;

                    // 创建方向对象
                    var direction = new JsonDirection
                    {
                        IsLoop = directionInfo.Attrs.Contains("LOOP"),
                        // 环线需要指定内外环类型
                        DirectionType = directionInfo.Attrs.Contains("LOOP") ? directionInfo.DirectionType : null,
                        Station = new List<JsonDirectionStation>(),
                        IsFuture = directionInfo.IsFuture,
                        ConnectStation = directionInfo.ConnectStation,
                        ExpressStops = directionInfo.ExpressStops.Count > 0
                            ? new List<string>(directionInfo.ExpressStops)
                            : null,
                        Routings = directionInfo.Routings.Count > 0
                            ? directionInfo.Routings.Select(r => new JsonRouting
                            {
                                Name = r.Name,
                                Stations = new List<string>(r.Stations)
                            }).ToList()
                            : null
                    };

                    var stations = directionInfo.Stations;
                    if (stations.Count > 0)
                    {
                        direction.Start = stations.First();
                        direction.End = stations.Last();
                    }

                    // 获取该方向的距离数据
                    var distances = context.Distances.GetValueOrDefault(directionId, new List<double>());

                    // 构建每个车站的详细信息
                    for (int i = 0; i < stations.Count; i++)
                    {
                        var stationEntry = new JsonDirectionStation { Name = stations[i] };

                        // 设置到站距离（小于1e30视为有效距离）
                        if (i < distances.Count && distances[i] < 1e30)
                            stationEntry.Next = distances[i];

                        // 设置停站时间（若存在自定义配置）
                        if (context.DwellTimes.TryGetValue((stations[i], lineId), out double dwellSeconds))
                            stationEntry.DwellTime = (int)dwellSeconds;

                        direction.Station.Add(stationEntry);
                    }

                    line.Direction.Add(direction);
                }
                result[lineId] = line;
            }
            return result;
        }
    }
}
