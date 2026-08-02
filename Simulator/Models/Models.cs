namespace MetroEditor.Simulator.Models
{
    /// <summary>票价分段信息（方向、线路、站点列表和距离）</summary>
    public class SimFareSegmentInfo
    {
        public string DirKey { get; set; } = string.Empty;
        public string LineId { get; set; } = string.Empty;
        public List<string> Stations { get; set; } = new();
        public double Distance { get; set; }
    }

    /// <summary>收藏路线信息</summary>
    public class SimFavoriteRoute
    {
        public string KeyCity { get; set; } = string.Empty;
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;

        public SimFavoriteRoute() { }
        public SimFavoriteRoute(string keyCity, string start, string end, string label)
        {
            KeyCity = keyCity;
            Start = start;
            End = end;
            Label = label;
        }
    }

    /// <summary>线路方向信息（起讫站、站点序列、别名颜色等）</summary>
    public class SimLineDirection
    {
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public List<string> Stations { get; set; } = new();
        public string NickColor { get; set; } = string.Empty;
        public bool IsFuture { get; set; }
        public bool IsBranch { get; set; }
        public string LineID { get; set; } = string.Empty;

        public SimLineDirection() { }
        public SimLineDirection(string lineId, string start, string end, List<string> stations,
                                string nickColor, bool isBranch, bool isFuture)
        {
            LineID = lineId;
            Start = start;
            End = end;
            Stations = stations;
            NickColor = nickColor;
            IsBranch = isBranch;
            IsFuture = isFuture;
        }
    }

    /// <summary>线路图例项（线路名、颜色列表、标签、里程）</summary>
    public class SimLineLegendItem
    {
        public string LineId { get; set; } = string.Empty;
        public string LineName { get; set; } = string.Empty;
        public List<int> Colors { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public string LengthStr { get; set; } = string.Empty;

        public SimLineLegendItem() { }
        public SimLineLegendItem(string lineId, string lineName, List<int> colors,
                                 List<string> tags, string lengthStr)
        {
            LineId = lineId;
            LineName = lineName;
            Colors = colors;
            Tags = tags;
            LengthStr = lengthStr;
        }
    }

    /// <summary>地图文件信息（文件名、是否内置、文件大小）</summary>
    public class SimMapFileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public long FileSize { get; set; }

        public SimMapFileInfo() { }
        public SimMapFileInfo(string fileName, bool isInternal, long fileSize)
        {
            FileName = fileName;
            IsInternal = isInternal;
            FileSize = fileSize;
        }
    }

    /// <summary>
    /// 路径搜索结果。
    /// <para>包含换乘次数、总代价、路径节点列表和票价信息。
    /// <c>Transfers</c> 为 -1 表示未找到路径。</para>
    /// </summary>
    public class SimPathResult
    {
        public int Transfers { get; set; } = -1;
        public List<string> Path { get; set; } = new();
        public double Cost { get; set; }
        public double TotalMoney { get; set; }
        public double SpecialMoney { get; set; }

        public SimPathResult() { }
        public SimPathResult(int transfers, double cost, List<string> path)
        {
            Transfers = transfers;
            Cost = cost;
            Path = path;
        }
    }

    /// <summary>行程分段信息（起讫站、线路、方向、站数、是否支线等）</summary>
    public class SimRouteSegment
    {
        public string StartStation { get; set; } = string.Empty;
        public string LineId { get; set; } = string.Empty;
        public string LineName { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string EndStation { get; set; } = string.Empty;
        public int StationCount { get; set; }
        public bool IsBranch { get; set; }
        public string NickColor { get; set; } = string.Empty;
        public string DirKey { get; set; } = string.Empty;
        public bool IsOutStationTransfer { get; set; }

        public SimRouteSegment() { }
        public SimRouteSegment(string startStation, string lineId, string lineName,
                               string direction, string endStation, int stationCount,
                               bool isBranch, string nickColor, bool isOutStationTransfer,
                               string dirKey)
        {
            StartStation = startStation;
            LineId = lineId;
            LineName = lineName;
            Direction = direction;
            EndStation = endStation;
            StationCount = stationCount;
            IsBranch = isBranch;
            NickColor = nickColor;
            IsOutStationTransfer = isOutStationTransfer;
            DirKey = dirKey;
        }
    }

    /// <summary>
    /// 路径搜索权重配置。
    /// <para>各权重维度：距离、换乘、票价、时间、站点数、拥挤度。
    /// 通过不同权重组合实现「最快」「最少换乘」「最便宜」等多种搜索策略。</para>
    /// </summary>
    public class SimSearchWeight
    {
        public double DistanceWeight { get; }
        public double TransferWeight { get; }
        public double MoneyWeight { get; }
        public double TimeWeight { get; }
        public double StationWeight { get; }
        public double CrowdWeight { get; set; }

        public SimSearchWeight(double distanceWeight, double transferWeight, double moneyWeight,
                               double timeWeight, double stationWeight, double crowdWeight = 0)
        {
            DistanceWeight = distanceWeight;
            TransferWeight = transferWeight;
            MoneyWeight = moneyWeight;
            TimeWeight = timeWeight;
            StationWeight = stationWeight;
            CrowdWeight = crowdWeight;
        }

        public static SimSearchWeight GetShortestWeight()
            => new SimSearchWeight(1, 0, 0, 0, 0);
    }

    /// <summary>通勤分析站点指标（距离、换乘次数、综合得分、票价、时间等）</summary>
    public class SimStationValue
    {
        public string Station { get; set; } = string.Empty;
        public double Distance { get; set; }
        public int Transfers { get; set; }
        public double Value { get; set; }
        public string RouteDesc { get; set; } = string.Empty;
        public double Fare { get; set; }
        public double SpecialFare { get; set; }
        public double Time { get; set; }
        public int StationCount { get; set; }

        public SimStationValue() { }
        public SimStationValue(string station, double distance, int transfers, double value,
                               string routeDesc, double fare, double specialFare,
                               double time, int stationCount)
        {
            Station = station;
            Distance = distance;
            Transfers = transfers;
            Value = value;
            RouteDesc = routeDesc;
            Fare = fare;
            SpecialFare = specialFare;
            Time = time;
            StationCount = stationCount;
        }
    }

    /// <summary>换乘线路信息（线路ID、显示名、颜色、状态）</summary>
    public class SimTransferLineInfo
    {
        public string LineId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Color { get; set; }
        public string Status { get; set; } = "operating";

        public SimTransferLineInfo() { }
        public SimTransferLineInfo(string lineId, string displayName, int color, string status)
        {
            LineId = lineId;
            DisplayName = displayName;
            Color = color;
            Status = status;
        }
    }

    /// <summary>UI 显示项（标题或站点，用于线路详情等界面渲染）</summary>
    public class SimDisplayItem
    {
        public string NickName { get; set; } = string.Empty;
        public int Type { get; set; } // 0 = 标题, 1 = 站点
        public string HeaderTitle { get; set; } = string.Empty;
        public int HeaderColor { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string LineTag { get; set; } = string.Empty;
        public string StationType { get; set; } = string.Empty;
        public int Index { get; set; } = -1;
        public List<SimTransferLineInfo> Transfers { get; set; } = new();

        public SimDisplayItem(string stationName, string stationType, int index, string nickName, string lineTag)
        {
            Type = 1;
            StationName = stationName;
            StationType = stationType;
            Index = index;
            NickName = nickName;
            LineTag = lineTag;
        }

        public SimDisplayItem(string headerTitle, int headerColor)
        {
            Type = 0;
            HeaderTitle = headerTitle;
            HeaderColor = headerColor;
        }
    }

    /// <summary>票价分段明细（线路、起讫站、距离、基础票价、附加费、总票价）</summary>
    public class SimFareSegmentDetail
    {
        public string LineId { get; set; } = string.Empty;
        public string LineDisplayName { get; set; } = string.Empty;
        public string StartStation { get; set; } = string.Empty;
        public string EndStation { get; set; } = string.Empty;
        public List<string> Stations { get; set; } = new();
        public double Distance { get; set; }
        public bool IsSpecial { get; set; }
        public double BaseFare { get; set; }
        public double Surcharge { get; set; }
        public double TotalFare { get; set; }

        public override string ToString()
        {
            return $"【{LineDisplayName}】{StartStation} → {EndStation}，{Distance:F1} km，" +
                   $"{(IsSpecial ? "特殊" : "普通")}票价：基础 {BaseFare:F2} + 附加 {Surcharge:F2} = {TotalFare:F2} 元";
        }
    }

    /// <summary>阶梯票价区间（起止里程和对应票价）</summary>
    public class SimFareSegment
    {
        public double RangeStart { get; set; }
        public double RangeEnd { get; set; }
        public double Fare { get; set; }
    }

    /// <summary>线性扩展票价规则（超出门槛后的每公里加收）</summary>
    public class SimLinearExtension
    {
        public double Threshold { get; set; }
        public double BaseFare { get; set; }
        public double RatePerKm { get; set; }
    }

    /// <summary>区间附加费项（起讫站和附加金额）</summary>
    public class SimSurcharge
    {
        public string Node_a { get; set; } = string.Empty;
        public string Node_b { get; set; } = string.Empty;
        public double Price { get; set; }
    }

    /// <summary>特殊线路票价规则（统一票价/区间附加费/独立分段计价）</summary>
    public class SimSpecialLineRule
    {
        public string LineId { get; set; } = string.Empty;
        public List<string>? UseLines { get; set; }
        public double? All { get; set; }
        public List<SimSurcharge>? Surcharge { get; set; }
        public double? DefaultFare { get; set; }
        public List<SimFareSegment>? Segments { get; set; }
        public SimLinearExtension? LinearExtension { get; set; }
    }

    public class SimHeadwayRule
    {
        public string LineId { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int IntervalSeconds { get; set; }
        public List<string>? Days { get; set; }
    }

    public class SimCrowdRule
    {
        public string DirectionKey { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public double Factor { get; set; }
        public List<string>? Days { get; set; }
    }

    public class SimOpenTimeRule
    {
        public string? LineId { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public List<string>? Days { get; set; }
    }

    public class SimVirtualTransfer
    {
        public string StationA { get; set; } = string.Empty;
        public string StationB { get; set; } = string.Empty;
        public double TimeMinutes { get; set; }
        public bool SecurityCheck { get; set; }
        public List<string>? ViaDirections { get; set; }
    }

    public class SimSpeedLimit
    {
        public string DirectionKey { get; set; } = string.Empty;
        public string FromStation { get; set; } = string.Empty;
        public string ToStation { get; set; } = string.Empty;
        public int SpeedKmh { get; set; }
    }
}