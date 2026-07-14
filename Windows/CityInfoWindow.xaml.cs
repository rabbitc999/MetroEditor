using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MetroEditor.MetroLang.DataModel;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 城市路网信息总览窗口
    /// 功能：展示当前加载城市的全局统计数据、票价规则和线路详情
    /// 设计特点：
    /// - 只读展示，不支持编辑
    /// - 自动计算总里程（仅统计主线方向）
    /// - 可视化展示线路属性（支线、环线、快车、特殊线路）
    /// </summary>
    public partial class CityInfoWindow : Window
    {
        private readonly ParsedData _data;

        public CityInfoWindow(ParsedData data)
        {
            InitializeComponent();
            _data = data;
            LoadData();
        }

        /// <summary>
        /// 计算指定线路的实际长度（仅主线方向，取最大距离方向）
        /// </summary>
        /// <param name="data">路网数据</param>
        /// <param name="lineId">线路ID</param>
        /// <returns>线路长度（千米）</returns>
        /// <remarks>
        /// 计算策略：
        /// 1. 排除未来线路（IsFuture）和快车线路（FAST）
        /// 2. 优先计算主线（非BRANCH），若无主线则取任意方向
        /// 3. 对同一线路的多方向取最长距离（避免支线拉低平均值）
        /// </remarks>
        public static double GetLineLength(ParsedData data, string lineId)
        {
            var dirs = data.Directions.Values.Where(d => d.LineId == lineId).ToList();
            var activeDirs = dirs.Where(d => !d.IsFuture && !d.Attrs.Contains("FAST")).ToList();
            if (!activeDirs.Any()) return 0;

            // 优先取主线（非支线）
            var mainDirs = activeDirs.Where(d => !d.Attrs.Contains("BRANCH")).ToList();
            var targetDirs = mainDirs.Any() ? mainDirs : activeDirs.Take(1).ToList();

            double maxLength = 0;
            foreach (var dir in targetDirs)
            {
                // 查找对应的方向键（Dictionary的Key）
                var dirKey = data.Directions.FirstOrDefault(kv => kv.Value == dir).Key;
                if (dirKey == null) continue;

                // 累加该方向所有区间的距离
                if (data.Distances.TryGetValue(dirKey, out var distList))
                {
                    double dirLength = 0;
                    foreach (var d in distList)
                    {
                        if (d < 1e30) dirLength += d; // 过滤无效距离
                    }
                    if (dirLength > maxLength) maxLength = dirLength;
                }
            }
            return maxLength;
        }

        /// <summary>
        /// 加载并显示所有数据
        /// </summary>
        private void LoadData()
        {
            // ===== 1. 基本信息 =====
            var mainCity = _data.MainCity;
            var cityInfo = _data.Cities.TryGetValue(mainCity, out var ci) ? ci : null;
            string version = cityInfo?.GetValueOrDefault("version", "未知") ?? "未知";
            string updateDate = cityInfo?.GetValueOrDefault("update_date", "未知") ?? "未知";

            // 计算总里程（累加所有线路的主线长度）
            double totalLength = 0;
            foreach (var lineId in _data.Lines.Keys)
            {
                totalLength += GetLineLength(_data, lineId);
            }

            // 绑定基本信息到UI
            CityNameText.Text = $"城市：{mainCity}";
            VersionText.Text = $"版本：{version}";
            UpdateDateText.Text = $"更新日期：{updateDate}";
            TotalLengthText.Text = $"总里程：{totalLength:F1} km";
            StationCountText.Text = $"站点总数：{_data.Stations.Count}";
            LineCountText.Text = $"线路总数：{_data.Lines.Count}";

            // ===== 2. 票价规则 =====
            LoadFareRules();

            // ===== 3. 线路列表 =====
            LoadLineList();
        }

        /// <summary>
        /// 加载票价规则到DataGrid
        /// </summary>
        private void LoadFareRules()
        {
            var items = new List<FareDisplayItem>();
            double startPrice = 2.0; // 默认起步价

            foreach (var rule in _data.MoneyRules)
            {
                if (rule is MoneyStartRule sr)
                {
                    startPrice = sr.Price;
                }
                else if (rule is MoneySegmentRule seg)
                {
                    items.Add(new FareDisplayItem
                    {
                        Type = "分段",
                        Range = $"{seg.Start:F1} - {seg.End:F1} km",
                        Price = seg.Price,
                        Note = $"里程 {seg.Start}~{seg.End} km"
                    });
                }
                else if (rule is MoneyCalcRule calc)
                {
                    items.Add(new FareDisplayItem
                    {
                        Type = "线性扩展",
                        Range = $"每 {calc.DistSegment:F1} km",
                        Price = calc.AddPrice,
                        Note = $"超过分段后每 {calc.DistSegment} km 加 {calc.AddPrice} 元"
                    });
                }
            }

            // 插入起步价（放在最前面）
            items.Insert(0, new FareDisplayItem
            {
                Type = "起步价",
                Range = "0 km",
                Price = startPrice,
                Note = $"起步价 {startPrice:F2} 元"
            });

            FareGrid.ItemsSource = items;
        }

        /// <summary>
        /// 加载线路列表到ListView
        /// </summary>
        private void LoadLineList()
        {
            var lines = new List<LineDisplayItem>();
            foreach (var kv in _data.Lines)
            {
                string lineId = kv.Key;
                var lineInfo = kv.Value;
                var dirs = _data.Directions.Values.Where(d => d.LineId == lineId).ToList();

                // 使用辅助方法获取长度
                double length = GetLineLength(_data, lineId);

                // 解析线路颜色
                var color = TryParseColor(lineInfo.Color) ?? Brushes.Gray;

                // 检测线路特性
                bool hasBranch = dirs.Any(d => d.Attrs.Contains("BRANCH")); // 是否有支线
                bool hasLoop = dirs.Any(d => d.Attrs.Contains("LOOP"));     // 是否环线
                bool hasFast = dirs.Any(d => d.Attrs.Contains("FAST"));     // 是否有快车
                bool isSpecial = _data.SpecialLineIds?.Contains(lineId) ?? false; // 是否特殊线路

                lines.Add(new LineDisplayItem
                {
                    Name = lineInfo.Name,
                    Color = color,
                    Length = length,
                    HasBranch = hasBranch,
                    HasLoop = hasLoop,
                    HasFast = hasFast,
                    IsSpecial = isSpecial
                });
            }

            // 按线路名称排序并绑定
            LineListView.ItemsSource = lines.OrderBy(x => x.Name);
        }

        /// <summary>
        /// 尝试解析颜色字符串为画刷
        /// </summary>
        private static SolidColorBrush TryParseColor(string colorStr)
        {
            if (string.IsNullOrEmpty(colorStr)) return null;
            try
            {
                return (SolidColorBrush)new BrushConverter().ConvertFromString(colorStr);
            }
            catch
            {
                return null;
            }
        }

        // ======================== 内部数据模型 ========================
        public class FareDisplayItem
        {
            public string Type { get; set; }
            public string Range { get; set; }
            public double Price { get; set; }
            public string Note { get; set; }
        }

        public class LineDisplayItem
        {
            public string Name { get; set; }
            public SolidColorBrush Color { get; set; }
            public double Length { get; set; }
            public bool HasBranch { get; set; }
            public bool HasLoop { get; set; }
            public bool HasFast { get; set; }
            public bool IsSpecial { get; set; }
        }
    }

    // ======================== UI 转换器 ========================
    /// <summary>
    /// Bool到Visibility转换器（用于根据布尔值控制UI元素的可见性）
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}