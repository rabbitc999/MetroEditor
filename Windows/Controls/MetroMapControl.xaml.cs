using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using MetroEditor.MetroLang.JsonModel;
using System.IO;

namespace MetroEditor.Windows.Controls
{
    /// <summary>
    /// 地铁地图控件 - 封装 WebView2 地图渲染与交互
    /// 支持预览模式（只读）和编辑模式（拖拽站点）
    /// </summary>
    public partial class MetroMapControl : UserControl
    {
        #region 依赖属性

        /// <summary>
        /// 是否为编辑模式（启用拖拽站点功能）
        /// </summary>
        public static readonly DependencyProperty IsEditModeProperty =
            DependencyProperty.Register(nameof(IsEditMode), typeof(bool), typeof(MetroMapControl),
                new PropertyMetadata(false, OnIsEditModeChanged));

        public bool IsEditMode
        {
            get => (bool)GetValue(IsEditModeProperty);
            set => SetValue(IsEditModeProperty, value);
        }

        private static void OnIsEditModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MetroMapControl)d;
            bool isEdit = (bool)e.NewValue;
            control.SetEditMode(isEdit);
        }

        #endregion

        #region 字段

        private JsonRoot _currentRoot;
        private bool _isPageLoaded;
        private bool _isNavigating;
        private DispatcherTimer _sizeChangedTimer;
        private const double SizeChangedDebounceMs = 500;

        #endregion

        #region 事件

        /// <summary>
        /// 站点坐标变化事件（编辑模式下拖拽站点后触发）
        /// </summary>
        public event EventHandler<Dictionary<string, (double Lon, double Lat)>> StationsChanged;

        /// <summary>
        /// 站点长按事件（预览模式下长按站点）
        /// </summary>
        public event EventHandler<string> StationLongClicked;

        /// <summary>
        /// 空白处长按事件
        /// </summary>
        public event EventHandler BlankLongClicked;

        /// <summary>
        /// 地图准备就绪事件
        /// </summary>
        public event EventHandler MapReady;

        private string _html;

        #endregion

        public MetroMapControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            // 订阅 SizeChanged 事件（在控件级别）
            this.SizeChanged += OnControlSizeChanged;
        }

        #region 初始化与生命周期

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                loadingOverlay.Visibility = Visibility.Visible;
                await webView.EnsureCoreWebView2Async(null);
                webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                webView.NavigationCompleted += OnNavigationCompleted;

                // 根据模式加载不同的 HTML
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string htmlPath = IsEditMode ? Path.Combine(baseDirectory, "Page/edit.html") : Path.Combine(baseDirectory, "Page/map.html");
                _html = File.ReadAllText(htmlPath);
                webView.NavigateToString(_html);
            }
            catch (Exception)
            {
            
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            }
            webView.NavigationCompleted -= OnNavigationCompleted;
            _sizeChangedTimer?.Stop();
            this.SizeChanged -= OnControlSizeChanged;
        }

        #endregion

        #region WebView 事件处理

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            loadingOverlay.Visibility = Visibility.Collapsed;
            _isNavigating = false; // 确保重置

            if (!e.IsSuccess)
            {
                return;
            }

            _isPageLoaded = true;
            if (_currentRoot != null)
            {
                InjectData(_currentRoot);
            }
            MapReady?.Invoke(this, EventArgs.Empty);
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message = e.TryGetWebMessageAsString();
            if (message == "stationsChanged")
            {
                // 编辑模式下站点坐标变化
                if (IsEditMode)
                {
                    _ = RefreshStationsAsync();
                }
            }
            else if (message.StartsWith("stationLongClick:"))
            {
                string station = message.Substring("stationLongClick:".Length);
                StationLongClicked?.Invoke(this, station);
            }
            else if (message == "blankLongClick")
            {
                BlankLongClicked?.Invoke(this, EventArgs.Empty);
            }
            else if (message == "mapReady")
            {
                // 地图内部初始化完成，可以安全注入数据
                if (_currentRoot != null)
                    InjectData(_currentRoot);
            }
        }

        #endregion

        #region 尺寸变化处理

        private void OnControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 如果 WebView2 已加载，直接通知或重载
            if (webView.CoreWebView2 == null || !_isPageLoaded || _isNavigating)
                return;

            // 防抖
            _sizeChangedTimer ??= new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SizeChangedDebounceMs)
            };
            _sizeChangedTimer.Stop();
            _sizeChangedTimer.Tick += (_, _) =>
            {
                _sizeChangedTimer.Stop();
                Reload();
            };
            _sizeChangedTimer.Start();
        }

        /// <summary>
        /// 通知 HTML 尺寸已变化（通过 JS）
        /// </summary>
        private async void NotifyResize()
        {
            if (webView.CoreWebView2 == null || !_isPageLoaded) return;
            try
            {
                string script = "if (typeof onResize === 'function') onResize();";
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                // 如果 JS 调用失败，降级为 Reload
                System.Diagnostics.Debug.WriteLine($"NotifyResize 失败: {ex.Message}");
                Reload();
            }
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 加载地铁数据
        /// </summary>
        public void LoadData(JsonRoot root)
        {
            if (root == null) return;
            _currentRoot = root ?? throw new ArgumentNullException(nameof(root));
            if (_isPageLoaded && webView.CoreWebView2 != null)
            {
                InjectData(root);
            }
            // 如果页面未加载，等待 OnNavigationCompleted 中注入
        }

        /// <summary>
        /// 刷新地图（重新加载数据）
        /// </summary>
        public void Reload()
        {
            if (_isNavigating || _currentRoot == null || webView.CoreWebView2 == null)
                return;

            _isNavigating = true;
            webView.NavigationCompleted += OnNavigationCompleted;
            webView.Reload();
        }

        /// <summary>
        /// 高亮指定线路
        /// </summary>
        public async void HighlightLines(HashSet<string> lineIds)
        {
            if (!_isPageLoaded || webView.CoreWebView2 == null) return;
            string json = JsonSerializer.Serialize(lineIds);
            string script = $"highlightLinesFromAndroid({json})";
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        /// <summary>
        /// 清除路径
        /// </summary>
        public async void ClearPath()
        {
            if (!_isPageLoaded || webView.CoreWebView2 == null) return;
            await webView.CoreWebView2.ExecuteScriptAsync("clearRouteFromAndroid();");
        }

        /// <summary>
        /// 绘制路径
        /// </summary>
        public async void DrawPath(List<string> stationNames)
        {
            if (!_isPageLoaded || webView.CoreWebView2 == null) return;
            string json = JsonSerializer.Serialize(stationNames);
            string script = $"drawPathFromAndroid({json})";
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        /// <summary>
        /// 缩放至站点集合
        /// </summary>
        public async void ZoomToStations(HashSet<string> stationNames)
        {
            if (!_isPageLoaded || webView.CoreWebView2 == null || stationNames == null || stationNames.Count == 0) return;
            string json = JsonSerializer.Serialize(stationNames);
            string script = $"zoomToStationsFromAndroid({json})";
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        /// <summary>
        /// 缩放至线路集合
        /// </summary>
        public async void ZoomToLines(HashSet<string> lineIds)
        {
            if (!_isPageLoaded || webView.CoreWebView2 == null || lineIds == null || lineIds.Count == 0) return;
            string json = JsonSerializer.Serialize(lineIds);
            string script = $"zoomToLinesFromAndroid({json})";
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        /// <summary>
        /// 显示错误信息
        /// </summary>
        public async void ShowError(string message)
        {
            if (!_isPageLoaded || webView.CoreWebView2 == null) return;
            string script = $"showErrorFromAndroid('{message.Replace("'", "\\'")}')";
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        #endregion

        #region 内部方法

        private void SetEditMode(bool isEdit)
        {
            // 如果已经加载，切换 HTML（需要重新加载）
            if (_isPageLoaded && webView.CoreWebView2 != null)
            {
                webView.NavigateToString(_html);
                _isPageLoaded = false; // 重置加载状态
            }
        }

        private async void InjectData(JsonRoot root)
        {
            if (webView.CoreWebView2 == null) return;
            string json = JsonSerializer.Serialize(root);
            // 双重序列化，确保 JS 端正确解析
            string script = $"loadMetroData({JsonSerializer.Serialize(json)})";
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private async Task RefreshStationsAsync()
        {
            try
            {
                if (!_isPageLoaded || webView.CoreWebView2 == null) return;
                string script = "getStationsDataAsJSON();";
                string jsonResult = await webView.CoreWebView2.ExecuteScriptAsync(script);
                string actualJson = JsonSerializer.Deserialize<string>(jsonResult);
                if (string.IsNullOrEmpty(actualJson)) return;

                using var doc = JsonDocument.Parse(actualJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("stations", out var stationsObj)) return;

                var dict = new Dictionary<string, (double Lon, double Lat)>();
                foreach (var prop in stationsObj.EnumerateObject())
                {
                    string name = prop.Name;
                    var st = prop.Value;
                    double lon = st.GetProperty("lon").GetDouble();
                    double lat = st.GetProperty("lat").GetDouble();
                    dict[name] = (lon, lat);
                }
                StationsChanged?.Invoke(this, dict);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新站点失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 页面是否加载完成
        /// </summary>
        public bool IsPageLoaded => _isPageLoaded;

        /// <summary>
        /// 获取当前地图上的站点坐标数据
        /// </summary>
        public async Task<Dictionary<string, (double Lon, double Lat)>> GetStationsDataAsync()
        {
            if (!_isPageLoaded || webView.CoreWebView2 == null)
                return new Dictionary<string, (double Lon, double Lat)>();

            string script = "getStationsDataAsJSON();";
            string jsonResult = await webView.CoreWebView2.ExecuteScriptAsync(script);
            string actualJson = JsonSerializer.Deserialize<string>(jsonResult);
            if (string.IsNullOrEmpty(actualJson))
                return new Dictionary<string, (double Lon, double Lat)>();

            using var doc = JsonDocument.Parse(actualJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("stations", out var stationsObj))
                return new Dictionary<string, (double Lon, double Lat)>();

            var dict = new Dictionary<string, (double Lon, double Lat)>();
            foreach (var prop in stationsObj.EnumerateObject())
            {
                string name = prop.Name;
                var st = prop.Value;
                double lon = st.GetProperty("lon").GetDouble();
                double lat = st.GetProperty("lat").GetDouble();
                dict[name] = (lon, lat);
            }
            return dict;
        }
        #endregion
    }
}