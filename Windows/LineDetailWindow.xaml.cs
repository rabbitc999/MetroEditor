using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 线路详情查看窗口
    /// 功能：展示单条线路的完整信息，包括基本信息、属性标签和站点序列
    /// 核心特性：
    /// 1. 智能分组显示：主线、支线、未来线路分开展示
    /// 2. 属性标签化：环线、支线、快车、特殊计费等直观标识
    /// 3. 精确里程计算：仅统计主线方向，排除支线和快车干扰
    /// </summary>
    /// <remarks>
    /// 设计模式：
    /// - 使用 DataTemplateSelector 实现混合列表（标题+站点）
    /// - 使用转换器控制未来线路的显示状态
    /// - 静态方法 GetLineLength 可被其他模块复用
    /// </remarks>
    public partial class LineDetailWindow : Window
    {
        public LineDetailWindow(ParsedData data, string lineId)
        {
            InitializeComponent();

            // 验证线路是否存在
            if (!data.Lines.TryGetValue(lineId, out var line))
            {
                MessageBox.Show($"未找到线路 {lineId}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // ---------- 基本信息面板 ----------
            LineNameText.Text = line.Name;
            CityText.Text = line.City ?? data.MainCity;
            FormationText.Text = line.Formation ?? "未知";
            MaxSpeedText.Text = line.Maxspeed.HasValue ? $"{line.Maxspeed} km/h" : "未知";
            OpenYearText.Text = line.OpenYear > 0 ? line.OpenYear.ToString() : "未知";

            // 计算并显示线路长度（仅主线）
            double length = GetLineLength(data, lineId);
            LengthText.Text = length > 0 ? $"{length:F1} km" : "未知";

            // ---------- 属性标签 ----------
            var tags = new List<TagItem>();
            var dirs = data.Directions.Values.Where(d => d.LineId == lineId).ToList();

            if (dirs.Any(d => d.Attrs.Contains("LOOP")))
                tags.Add(new TagItem { Text = "环线", Color = "#4CAF50" });
            if (dirs.Any(d => d.Attrs.Contains("BRANCH")))
                tags.Add(new TagItem { Text = "带支线", Color = "#9C27B0" });
            if (dirs.Any(d => d.Attrs.Contains("FAST")))
                tags.Add(new TagItem { Text = "有快车", Color = "#2196F3" });
            if (data.SpecialLineIds?.Contains(lineId) == true)
                tags.Add(new TagItem { Text = "单独计费", Color = "#D32F2F" });
            if (dirs.Any(d => d.IsFuture))
                tags.Add(new TagItem { Text = "未来线路", Color = "#FF9800" });

            TagsItemsControl.ItemsSource = tags;

            // ---------- 站点序列（分组显示） ----------
            var displayItems = BuildDisplayItems(data, lineId);
            StationListView.ItemsSource = displayItems;
            Loaded += (o, e) => Topmost = false;
        }

        #region 数据构建逻辑
        /// <summary>
        /// 构建用于 ListView 的混合数据源
        /// 结构：HeaderItem (标题) -> StationDisplayItem (站点) -> HeaderItem -> ...
        /// </summary>
        private List<object> BuildDisplayItems(ParsedData data, string lineId)
        {
            var items = new List<object>();
            var dirs = data.Directions.Values.Where(d => d.LineId == lineId && !d.Attrs.Contains("FAST")).ToList();

            // 分类方向定义
            var mainDirs = dirs.Where(d => !d.Attrs.Contains("BRANCH") && !d.IsFuture).ToList();
            var branchDirs = dirs.Where(d => d.Attrs.Contains("BRANCH") && !d.IsFuture).ToList();
            var futureDirs = dirs.Where(d => d.IsFuture).ToList();

            // 1. 主线方向
            int mainIndex = 1;
            foreach (var dir in mainDirs)
            {
                string title = dir.Alias ?? $"{data.Lines[lineId].Name} 主线{mainIndex++}";
                if (dir.Attrs.Contains("LOOP")) title += " (环线)";
                items.Add(new HeaderItem { Title = title });
                items.AddRange(dir.Stations.Select(s => new StationDisplayItem { Name = s, IsMain = true }));
            }

            // 2. 支线方向
            int branchIndex = 1;
            foreach (var dir in branchDirs)
            {
                string title = dir.Alias ?? $"{data.Lines[lineId].Name} 支线{branchIndex++}";
                if (dir.Attrs.Contains("LOOP")) title += " (环线)";
                items.Add(new HeaderItem { Title = title });
                items.AddRange(dir.Stations.Select(s => new StationDisplayItem { Name = s, IsMain = false }));
            }

            // 3. 未来线路方向
            int futureIndex = 1;
            foreach (var dir in futureDirs)
            {
                string title = dir.Alias ?? $"{data.Lines[lineId].Name} 规划线路{futureIndex++}";
                if (dir.Attrs.Contains("LOOP")) title += " (环线)";
                items.Add(new HeaderItem { Title = title });
                items.AddRange(dir.Stations.Select(s => new StationDisplayItem { Name = s, IsFuture = true }));
            }

            return items;
        }

        /// <summary>
        /// 计算线路实际长度（静态方法，可供外部调用）
        /// 计算策略：
        /// 1. 排除未来线路和快车线路
        /// 2. 优先计算主线（非支线），若无主线则取任意方向
        /// 3. 对同一线路的多方向取最长距离（避免支线拉低平均值）
        /// </summary>
        public static double GetLineLength(ParsedData data, string lineId)
        {
            var dirs = data.Directions.Values.Where(d => d.LineId == lineId).ToList();
            var activeDirs = dirs.Where(d => !d.IsFuture && !d.Attrs.Contains("FAST")).ToList();
            if (!activeDirs.Any()) return 0;

            var mainDirs = activeDirs.Where(d => !d.Attrs.Contains("BRANCH")).ToList();
            var targetDirs = mainDirs.Any() ? mainDirs : activeDirs.Take(1).ToList();

            double maxLength = 0;
            foreach (var dir in targetDirs)
            {
                // 查找对应的方向键
                var dirKey = data.Directions.FirstOrDefault(kv => kv.Value == dir).Key;
                if (string.IsNullOrEmpty(dirKey)) continue;

                // 累加该方向所有区间的距离
                if (data.Distances.TryGetValue(dirKey, out var distList))
                {
                    double dirLength = distList.Where(d => d < 1e30).Sum(); // 过滤无效距离
                    if (dirLength > maxLength) maxLength = dirLength;
                }
            }
            return maxLength;
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 将颜色字符串转换为画刷
        /// </summary>
        private Brush GetColorBrush(string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex)) return Brushes.Gray;
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)); }
            catch { return Brushes.Gray; }
        }
        #endregion
    }

    #region 数据模型与辅助类
    /// <summary>
    /// 列表中的标题项（用于分组）
    /// </summary>
    public class HeaderItem
    {
        public string Title { get; set; }
    }

    /// <summary>
    /// 列表中的站点显示项
    /// </summary>
    public class StationDisplayItem
    {
        public string Name { get; set; }
        public bool IsMain { get; set; } = false;      // 是否为主线站点
        public bool IsFuture { get; set; } = false;    // 是否为未来线路站点
    }

    /// <summary>
    /// 属性标签项（用于TagsItemsControl）
    /// </summary>
    public class TagItem
    {
        public string Text { get; set; }
        public string Color { get; set; }
    }

    /// <summary>
    /// 换乘线路项（预留，当前未使用）
    /// </summary>
    public class TransferLineItem
    {
        public string Name { get; set; }
        public Brush Color { get; set; }
    }

    /// <summary>
    /// DataTemplate选择器：根据数据类型选择不同的模板
    /// </summary>
    public class ItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate HeaderTemplate { get; set; }
        public DataTemplate StationTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is HeaderItem)
                return HeaderTemplate;
            else if (item is StationDisplayItem)
                return StationTemplate;
            return base.SelectTemplate(item, container);
        }
    }

    /// <summary>
    /// 布尔值取反转换器（用于隐藏/显示未来线路相关元素）
    /// </summary>
    public class InverseBoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    #endregion
}