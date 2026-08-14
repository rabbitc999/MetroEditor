using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 路网统计窗口：汇总线路、车站、换乘、里程等指标，并提供各线路车站数柱状图。
    /// </summary>
    public partial class NetworkStatisticsWindow : Window
    {
        public NetworkStatisticsWindow(ParsedData data)
        {
            InitializeComponent();

            // 构建 车站 → 所属线路集合（由方向信息反推）
            var stationToLines = new Dictionary<string, HashSet<string>>();
            var lineStations = new Dictionary<string, HashSet<string>>(); // 仅主线
            foreach (var dir in data.Directions.Values)
            {
                if (!lineStations.ContainsKey(dir.LineId))
                    lineStations[dir.LineId] = new HashSet<string>();
                foreach (var st in dir.Stations)
                {
                    if (!dir.IsFuture) lineStations[dir.LineId].Add(st);
                    if (!stationToLines.ContainsKey(st)) stationToLines[st] = new HashSet<string>();
                    stationToLines[st].Add(dir.LineId);
                }
            }

            // 汇总卡片
            int lineCount = data.Lines.Count;
            int stationCount = data.Stations.Count;
            int transferCount = stationToLines.Count(kv => kv.Value.Count >= 2);
            int futureStations = data.StationStatus
                .Count(kv => kv.Value.Equals("FUTURE", StringComparison.OrdinalIgnoreCase));
            int loopCount = data.Directions.Values
                .Where(d => d.Attrs.Contains("LOOP"))
                .Select(d => d.LineId).Distinct().Count();
            int expressCount = data.Directions.Values
                .Where(d => d.ExpressStops.Count > 0)
                .Select(d => d.LineId).Distinct().Count();
            int operatorCount = data.OperatorCompanies?.Count ?? 0;
            int zoneCount = data.Zones?.Count ?? 0;

            // 总里程：复用 LineDetailWindow 的计算逻辑
            var lineLengths = new Dictionary<string, double>();
            foreach (var lineId in data.Lines.Keys)
                lineLengths[lineId] = LineDetailWindow.GetLineLength(data, lineId);
            double totalLength = lineLengths.Values.Sum();

            LineCountText.Text = lineCount.ToString();
            StationCountText.Text = stationCount.ToString();
            TransferCountText.Text = transferCount.ToString();
            TotalLengthText.Text = totalLength.ToString("F1");
            FutureCountText.Text = futureStations.ToString();
            LoopCountText.Text = loopCount.ToString();
            ExpressCountText.Text = expressCount.ToString();
            OperatorCountText.Text = operatorCount.ToString();
            ZoneCountText.Text = zoneCount.ToString();

            // 明细表
            var rows = new List<LineStatRow>();
            foreach (var lineId in data.Lines.Keys.OrderBy(id => id))
            {
                var line = data.Lines[lineId];
                var dirs = data.Directions.Values.Where(d => d.LineId == lineId).ToList();
                double length = lineLengths.GetValueOrDefault(lineId, 0);

                var tags = new List<string>();
                if (dirs.Any(d => d.Attrs.Contains("LOOP"))) tags.Add("环线");
                if (dirs.Any(d => d.ExpressStops.Count > 0)) tags.Add("快车");
                if (dirs.Any(d => d.IsFuture)) tags.Add("未来");
                if (data.SpecialLineIds.Contains(lineId)) tags.Add("单独计费");

                rows.Add(new LineStatRow
                {
                    LineId = lineId,
                    Name = line.Name,
                    City = !string.IsNullOrEmpty(line.City) ? line.City : data.MainCity,
                    Color = line.Color,
                    StationCount = lineStations.GetValueOrDefault(lineId)?.Count ?? 0,
                    Length = $"{length:F1}",
                    LengthRatio = totalLength > 0 ? $"{length / totalLength * 100:F1}%" : "—",
                    Tags = string.Join("、", tags)
                });
            }
            LineGrid.ItemsSource = rows;

            // 柱状图：各线路主线车站数（WPF 原生横向条形图）
            StationChart.ItemsSource = BuildStationCountChart(rows);

            Loaded += (o, e) => Topmost = false;
        }

        private List<StationChartItem> BuildStationCountChart(List<LineStatRow> rows)
        {
            const double MaxBarPx = 360.0;
            int maxCount = rows.Count > 0 ? rows.Max(r => r.StationCount) : 1;
            if (maxCount <= 0) maxCount = 1;

            return rows.Select(r => new StationChartItem
            {
                Label = r.LineId,
                Count = r.StationCount,
                BarWidth = Math.Max(4, r.StationCount * 1.0 / maxCount * MaxBarPx),
                Brush = ParseBrush(r.Color)
            }).ToList();
        }

        private static SolidColorBrush ParseBrush(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2));
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { return new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2)); }
        }
    }

    /// <summary>线路明细表行模型。</summary>
    public class LineStatRow
    {
        public string LineId { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public string Color { get; set; }
        public int StationCount { get; set; }
        public string Length { get; set; }
        public string LengthRatio { get; set; }
        public string Tags { get; set; }
    }

    /// <summary>柱状图条目。</summary>
    public class StationChartItem
    {
        public string Label { get; set; }
        public int Count { get; set; }
        public double BarWidth { get; set; }
        public SolidColorBrush Brush { get; set; }
    }
}
