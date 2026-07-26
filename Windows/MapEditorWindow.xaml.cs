using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Windows;
using MetroEditor.MetroLang.JsonModel;
using MessageBox = System.Windows.MessageBox;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 地图编辑器窗口 - 可视化拖拽编辑站点坐标
    /// 基于 MetroMapControl 控件实现
    /// </summary>
    public partial class MapEditorWindow : Window
    {
        private JsonRoot? _root;
        private Dictionary<string, (double Lon, double Lat)>? _currentStations;
        private const string Indent = "  ";

        public MapEditorWindow()
        {
            InitializeComponent();
            Loaded += MapEditorWindow_Loaded;
        }

        /// <summary>
        /// 加载地铁数据到地图编辑器
        /// </summary>
        public void LoadData(JsonRoot root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        private void MapEditorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_root != null)
            {
                mapControl.LoadData(_root);
                // 订阅站点变化事件
                mapControl.StationsChanged += OnStationsChanged;
            }
            else
            {
                MessageBox.Show("未加载任何数据，请先调用 LoadData", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 站点坐标变化事件处理
        /// </summary>
        private void OnStationsChanged(object? sender, Dictionary<string, (double Lon, double Lat)> stations)
        {
            _currentStations = stations;
            // 自动刷新预览
            _ = RefreshPreviewAsync();
        }

        /// <summary>
        /// 刷新代码预览区域
        /// </summary>
        private async Task RefreshPreviewAsync()
        {
            try
            {
                // 如果当前没有数据，尝试从控件获取
                if (_currentStations == null && mapControl.IsPageLoaded)
                {
                    _currentStations = await mapControl.GetStationsDataAsync();
                }

                if (_currentStations == null || _currentStations.Count == 0)
                {
                    PreviewTextBox.Text = "未获取到站点数据";
                    return;
                }

                string code = GenerateStationDefineCode(_root, _currentStations);
                PreviewTextBox.Text = code;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                PreviewTextBox.Text = "刷新失败";
            }
        }

        /// <summary>
        /// 生成 STATION_DEFINE 代码
        /// </summary>
        private static string GenerateStationDefineCode(JsonRoot? root, Dictionary<string, (double Lon, double Lat)> stations)
        {
            if (stations == null || stations.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("STATION_DEFINE");
            foreach (var kv in stations)
            {
                string name = kv.Key;
                double lon = kv.Value.Lon;
                double lat = kv.Value.Lat;
                string line = $"{Indent}NODE '{name}' COORD {lon:F6}, {lat:F6}";

                // 保留原有的城市信息
                if (root != null && root.Stations.TryGetValue(name, out var st) && !string.IsNullOrEmpty(st.City))
                {
                    line += $" CITY '{st.City}'";
                }
                sb.AppendLine(line);
            }
            sb.AppendLine("END_DEFINE");
            return sb.ToString();
        }

        #region 事件处理
        private async void RefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            await RefreshPreviewAsync();
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentStations == null || _currentStations.Count == 0)
                {
                    // 尝试从控件获取最新数据
                    _currentStations = await mapControl.GetStationsDataAsync();
                }

                if (_currentStations == null || _currentStations.Count == 0)
                {
                    MessageBox.Show("无站点数据可复制", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string code = GenerateStationDefineCode(_root, _currentStations);
                MainWindow.InsertText(code);
                MessageBox.Show("代码已复制到主界面", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // 移除事件订阅，防止内存泄漏
            mapControl.StationsChanged -= OnStationsChanged;
            base.OnClosed(e);
        }
        #endregion
    }
}