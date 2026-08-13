using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 区域管理窗口（ZONE_DEFINE）
    /// 功能：列表 + 表单方式管理多个区域（名称/类型/城市/颜色/中心点/半径/多边形/站点/属性），
    /// 支持从当前编译数据（ParsedData）导入、增删改、生成 ZONE_DEFINE 代码块插入编辑器
    /// </summary>
    public partial class ZoneManagerWindow : Window
    {
        private const string Indent = "  ";

        /// <summary>
        /// 区域编辑项（表单与列表的数据载体）
        /// </summary>
        public class ZoneItem
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public string City { get; set; } = "";
            public string Color { get; set; } = "";
            public string CenterText { get; set; } = "";
            public string RadiusText { get; set; } = "";
            public string PolygonText { get; set; } = "";
            public string StationsText { get; set; } = "";
            public string PeakHoursText { get; set; } = "";
            public string DensityText { get; set; } = "";
            public string RentText { get; set; } = "";

            public int StationCount => SplitList(StationsText).Count;
        }

        private readonly List<ZoneItem> _zones = new();
        private bool _loading; // 表单加载标志（防止加载时触发写回）

        public ZoneManagerWindow(ParsedData? data = null)
        {
            InitializeComponent();
            if (data != null)
                LoadFromData(data);
            else
                AddNewZone();
            RefreshGrid();
            UpdatePreview();
            Loaded += (o, e) => Topmost = false;
        }

        /// <summary>
        /// 从编译结果导入现有区域
        /// </summary>
        private void LoadFromData(ParsedData data)
        {
            foreach (var kv in data.Zones)
            {
                var z = kv.Value;
                _zones.Add(new ZoneItem
                {
                    Id = kv.Key,
                    Name = z.Name ?? "",
                    Type = z.Type ?? "",
                    City = z.City ?? "",
                    Color = z.Color ?? "",
                    CenterText = z.Center is { Length: 2 } c ? $"{c[0]:R}, {c[1]:R}" : "",
                    RadiusText = z.Radius?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                    PolygonText = z.Polygon is { Count: > 0 }
                        ? string.Join(Environment.NewLine, z.Polygon.Select(p => $"[{p[0]:R}, {p[1]:R}]"))
                        : "",
                    StationsText = z.Stations != null ? string.Join(", ", z.Stations) : "",
                    PeakHoursText = z.Attributes.PeakHours != null ? string.Join(", ", z.Attributes.PeakHours) : "",
                    DensityText = z.Attributes.PopulationDensity?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                    RentText = z.Attributes.AvgRent?.ToString("R", CultureInfo.InvariantCulture) ?? ""
                });
            }
        }

        /// <summary>
        /// 刷新列表与预览
        /// </summary>
        private void RefreshGrid()
        {
            ZoneGrid.ItemsSource = null;
            ZoneGrid.ItemsSource = _zones;
        }

        /// <summary>
        /// 新增一个空白区域并选中
        /// </summary>
        private ZoneItem AddNewZone()
        {
            var item = new ZoneItem();
            _zones.Add(item);
            return item;
        }

        #region 列表操作
        private void NewZone_Click(object sender, RoutedEventArgs e)
        {
            var item = AddNewZone();
            RefreshGrid();
            ZoneGrid.SelectedItem = item;
            ZoneGrid.ScrollIntoView(item);
            LoadForm(item);
            ZoneIdTextBox.Focus();
        }

        private void DeleteZone_Click(object sender, RoutedEventArgs e)
        {
            if (ZoneGrid.SelectedItem is not ZoneItem item) return;
            _zones.Remove(item);
            RefreshGrid();
            if (_zones.Count == 0)
                AddNewZone();
            RefreshGrid();
            ZoneGrid.SelectedIndex = Math.Min(0, _zones.Count - 1);
            LoadForm(ZoneGrid.SelectedItem as ZoneItem);
            UpdatePreview();
        }

        private void ZoneGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ZoneGrid.SelectedItem is ZoneItem item)
                LoadForm(item);
        }
        #endregion

        #region 表单同步
        /// <summary>
        /// 将选中项加载到表单（不触发写回）
        /// </summary>
        private void LoadForm(ZoneItem item)
        {
            if (item == null) return;
            _loading = true;
            try
            {
                ZoneIdTextBox.Text = item.Id;
                ZoneNameTextBox.Text = item.Name;
                ZoneTypeTextBox.Text = item.Type;
                ZoneCityTextBox.Text = item.City;
                ZoneColorTextBox.Text = item.Color;
                ZoneCenterTextBox.Text = item.CenterText;
                ZoneRadiusTextBox.Text = item.RadiusText;
                ZonePolygonTextBox.Text = item.PolygonText;
                ZoneStationsTextBox.Text = item.StationsText;
                ZonePeakHoursTextBox.Text = item.PeakHoursText;
                ZoneDensityTextBox.Text = item.DensityText;
                ZoneRentTextBox.Text = item.RentText;
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>
        /// 表单变更时写回选中项并刷新预览
        /// </summary>
        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || ZoneGrid.SelectedItem is not ZoneItem item) return;
            item.Id = ZoneIdTextBox.Text.Trim();
            item.Name = ZoneNameTextBox.Text;
            item.Type = ZoneTypeTextBox.Text.Trim();
            item.City = ZoneCityTextBox.Text;
            item.Color = ZoneColorTextBox.Text.Trim();
            item.CenterText = ZoneCenterTextBox.Text.Trim();
            item.RadiusText = ZoneRadiusTextBox.Text.Trim();
            item.PolygonText = ZonePolygonTextBox.Text;
            item.StationsText = ZoneStationsTextBox.Text;
            item.PeakHoursText = ZonePeakHoursTextBox.Text;
            item.DensityText = ZoneDensityTextBox.Text.Trim();
            item.RentText = ZoneRentTextBox.Text.Trim();
            UpdatePreview();
        }
        #endregion

        #region 代码生成
        /// <summary>
        /// 生成单个区域的 ZONE_DEFINE 代码
        /// </summary>
        private static string GenerateZoneCode(ZoneItem z)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ZONE_DEFINE {z.Id}");

            if (!string.IsNullOrEmpty(z.Name))
                sb.AppendLine($"{Indent}NAME '{z.Name}'");
            if (!string.IsNullOrEmpty(z.Type))
                sb.AppendLine($"{Indent}TYPE {z.Type}");
            if (!string.IsNullOrEmpty(z.City))
                sb.AppendLine($"{Indent}CITY '{z.City}'");
            if (!string.IsNullOrEmpty(z.Color))
                sb.AppendLine($"{Indent}COLOR '{z.Color}'");

            if (TryParseCoord(z.CenterText, out double clon, out double clat))
                sb.AppendLine($"{Indent}CENTER {clon:R}, {clat:R}");

            if (double.TryParse(z.RadiusText, NumberStyles.Float, CultureInfo.InvariantCulture, out double radius))
                sb.AppendLine($"{Indent}RADIUS {radius:R}");

            var polygon = ParsePolygon(z.PolygonText);
            if (polygon.Count > 0)
                sb.AppendLine($"{Indent}POLYGON [{string.Join(", ", polygon.Select(p => $"[{p[0]:R}, {p[1]:R}]"))}]");

            var stations = SplitList(z.StationsText);
            if (stations.Count > 0)
                sb.AppendLine($"{Indent}STATIONS [{string.Join(", ", stations.Select(s => $"'{s}'"))}]");

            var peakHours = SplitList(z.PeakHoursText);
            bool hasDensity = double.TryParse(z.DensityText, NumberStyles.Float, CultureInfo.InvariantCulture, out double density);
            bool hasRent = double.TryParse(z.RentText, NumberStyles.Float, CultureInfo.InvariantCulture, out double rent);
            if (peakHours.Count > 0 || hasDensity || hasRent)
            {
                sb.AppendLine($"{Indent}ATTRIBUTES");
                if (peakHours.Count > 0)
                    sb.AppendLine($"{Indent}{Indent}PEAK_HOURS [{string.Join(", ", peakHours.Select(p => $"'{p}'"))}]");
                if (hasDensity)
                    sb.AppendLine($"{Indent}{Indent}POPULATION_DENSITY {density:R}");
                if (hasRent)
                    sb.AppendLine($"{Indent}{Indent}AVG_RENT {rent:R}");
                sb.AppendLine($"{Indent}END_ATTRIBUTES");
            }

            sb.AppendLine("END_DEFINE");
            return sb.ToString();
        }

        /// <summary>
        /// 生成全部区域的 ZONE_DEFINE 代码
        /// </summary>
        private string GenerateAllCode()
        {
            var sb = new StringBuilder();
            var valid = _zones.Where(z => !string.IsNullOrEmpty(z.Id)).ToList();
            for (int i = 0; i < valid.Count; i++)
            {
                sb.AppendLine($"# 区域：{valid[i].Id}");
                sb.AppendLine(GenerateZoneCode(valid[i]));
                if (i < valid.Count - 1) sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// 更新预览（选中项若无则显示全部）
        /// </summary>
        private void UpdatePreview()
        {
            PreviewTextBox.Text = GenerateAllCode();
        }

        /// <summary>
        /// 解析逗号/空格分隔的列表
        /// </summary>
        public static List<string> SplitList(string input) =>
            (input ?? "").Split(new[] { ',', ' ', '\n', '\r', '\t', '[', ']', '\'' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim())
                         .Where(s => s.Length > 0)
                         .Distinct()
                         .ToList();

        /// <summary>
        /// 解析 "经度, 纬度" 坐标文本
        /// </summary>
        private static bool TryParseCoord(string text, out double lon, out double lat)
        {
            lon = lat = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out lon)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lat);
        }

        /// <summary>
        /// 解析多边形文本（每行 [经度, 纬度] 或 经度, 纬度）
        /// </summary>
        private static List<double[]> ParsePolygon(string text)
        {
            var result = new List<double[]>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (var line in text.Split('\n', '\r'))
            {
                var cleaned = line.Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "").Trim();
                if (TryParseCoord(cleaned, out double lon, out double lat))
                    result.Add(new[] { lon, lat });
            }
            return result;
        }
        #endregion

        #region 操作
        /// <summary>
        /// 插入到编辑器（生成全部区域的代码）
        /// </summary>
        private void InsertCode_Click(object sender, RoutedEventArgs e)
        {
            string code = GenerateAllCode();
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show("没有可插入的区域代码（请先填写区域 ID）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                MainWindow.InsertText(code);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"插入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        #endregion
    }
}
