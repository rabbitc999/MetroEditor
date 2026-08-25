using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 票价规则编辑器窗口
    /// 功能：可视化编辑 MetroLang 中的 PRICE_DEFINE 代码块
    /// 支持两种票价体系：
    /// 1. 普通票价：基于里程的分段累进计费
    /// 2. 特殊票价：针对机场线、磁悬浮等线路的独立计费
    /// </summary>
    /// <remarks>
    /// 核心设计：
    /// - 双向绑定：UI 修改实时反映到代码预览
    /// - 数据驱动：基于 ParsedData 加载现有规则
    /// - 即时验证：输入时自动生成合法的 MetroLang 代码
    /// </remarks>
    public partial class FareEditorWindow : Window
    {
        private readonly ParsedData _data;                           // 路网数据（只读）
        private readonly Dictionary<string, string> _lineNameMap;    // 线路ID到显示名称的映射
        private readonly List<string> _lineIds;                     // 所有线路ID列表
        private readonly ObservableCollection<SegmentViewModel> _segments = new();   // 普通票价分段（绑定到DataGrid）
        private readonly ObservableCollection<SpecialFareViewModel> _specials = new(); // 特殊票价规则（绑定到DataGrid）

        /// <summary>
        /// 构造函数：初始化票价编辑器
        /// </summary>
        /// <param name="data">解析后的路网数据</param>
        public FareEditorWindow(ParsedData data)
        {
            InitializeComponent();
            _data = data;

            // 获取所有线路并按ID排序
            _lineIds = data.Lines.Keys.OrderBy(x => x).ToList();

            // 构建线路显示名称映射（跨城市线路添加城市前缀）
            _lineNameMap = data.Lines.ToDictionary(
                kv => kv.Key,
                kv =>
                {
                    string city = kv.Value.City ?? data.MainCity;
                    return (city == data.MainCity) ? kv.Value.Name : kv.Value.Name;
                }
            );

            // ---------- 加载现有普通票价规则 ----------
            foreach (var rule in data.MoneyRules)
            {
                if (rule is MoneyStartRule start)
                    StartPriceBox.Text = start.Price.ToString("F2");      // 起步价
                else if (rule is MoneySegmentRule seg)
                    _segments.Add(new SegmentViewModel { Start = seg.Start, End = seg.End, Price = seg.Price }); // 分段规则
                else if (rule is MoneyCalcRule calc)
                {
                    DistSegmentBox.Text = calc.DistSegment.ToString("F2"); // 线性延伸单位距离
                    AddPriceBox.Text = calc.AddPrice.ToString("F2");       // 线性延伸单位加价
                }
            }

            // ---------- 加载现有特殊票价规则 ----------
            foreach (var sf in data.SpecialFareRules)
            {
                var vm = new SpecialFareViewModel();
                vm.SetLineNameMap(_lineNameMap);
                vm.LineId = sf.LineId;
                vm.Type = sf.Type;
                vm.Price = sf.Price;
                vm.StartStation = sf.StartStation ?? "";
                vm.EndStation = sf.EndStation ?? "";
                _specials.Add(vm);
            }

            // 绑定数据源到UI控件
            SegmentGrid.ItemsSource = _segments;
            SpecialGrid.ItemsSource = _specials;

            // 初始化代码预览
            RefreshPreview();
            Loaded += (o, e) => Topmost = false;
        }

        #region 普通票价分段操作
        /// <summary>
        /// 添加普通票价分段按钮点击事件
        /// </summary>
        private void AddSegment_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddSegmentDialog(); // 弹出分段输入对话框
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _segments.Add(new SegmentViewModel
                {
                    Start = dialog.Start,
                    End = dialog.End,
                    Price = dialog.Price
                });
                RefreshPreview(); // 更新代码预览
            }
        }

        /// <summary>
        /// 删除选中的普通票价分段
        /// </summary>
        private void DeleteSegment_Click(object sender, RoutedEventArgs e)
        {
            var selected = SegmentGrid.SelectedItems.Cast<SegmentViewModel>().ToList();
            foreach (var item in selected)
                _segments.Remove(item);
            RefreshPreview();
        }
        #endregion

        #region 特殊票价规则操作
        /// <summary>
        /// 添加特殊票价规则按钮点击事件
        /// </summary>
        private void AddSpecial_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddSpecialFareDialog(_lineIds, _lineNameMap);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                var vm = new SpecialFareViewModel();
                vm.SetLineNameMap(_lineNameMap);
                vm.LineId = dialog.LineId;
                vm.Type = dialog.Type;
                vm.Price = dialog.Price;
                vm.StartStation = dialog.StartStation ?? "";
                vm.EndStation = dialog.EndStation ?? "";
                _specials.Add(vm);
                RefreshPreview();
            }
        }

        /// <summary>
        /// 删除选中的特殊票价规则
        /// </summary>
        private void DeleteSpecial_Click(object sender, RoutedEventArgs e)
        {
            var selected = SpecialGrid.SelectedItems.Cast<SpecialFareViewModel>().ToList();
            foreach (var item in selected)
                _specials.Remove(item);
            RefreshPreview();
        }
        #endregion

        #region 代码预览与生成
        /// <summary>
        /// 手动刷新代码预览按钮点击事件
        /// </summary>
        private void RefreshPreview_Click(object sender, RoutedEventArgs e) => RefreshPreview();

        /// <summary>
        /// 生成 PRICE_DEFINE 代码块
        /// </summary>
        /// <remarks>
        /// 生成逻辑严格按照 MetroLang 语法：
        /// 1. DEFAULT_FARE：起步价
        /// 2. SEGMENT：里程分段（必须按起始里程升序排列）
        /// 3. LINEAR_EXTENSION：线性延伸规则（基于最后一段分段的结束里程）
        /// 4. SPECIAL_LINE：特殊线路规则（按线路分组）
        /// </remarks>
        private void RefreshPreview()
        {
            var sb = new StringBuilder();
            sb.AppendLine("PRICE_DEFINE");

            // ---------- 起步价 ----------
            if (double.TryParse(StartPriceBox.Text, out double startPrice))
                sb.AppendLine($"    DEFAULT_FARE {startPrice:F2}");

            // ---------- 里程分段 ----------
            // 按起始里程排序，确保生成的代码符合语法要求
            foreach (var seg in _segments.OrderBy(s => s.Start))
                sb.AppendLine($"    SEGMENT {seg.Start:F2} {seg.End:F2} {seg.Price:F2}");

            // ---------- 线性延伸规则 ----------
            if (double.TryParse(DistSegmentBox.Text, out double distSeg) &&
                double.TryParse(AddPriceBox.Text, out double addPrice))
            {
                double rate = addPrice / distSeg; // 计算每公里加价率

                // 获取最后一个分段的结束里程作为阈值
                double threshold = 0;
                if (_segments.Any())
                {
                    var last = _segments.OrderBy(s => s.End).Last();
                    threshold = last.End;
                }

                // 获取最后一个分段的票价作为基础票价
                double baseFare = _segments.Any() ? _segments.OrderBy(s => s.End).Last().Price : startPrice;

                sb.AppendLine($"    LINEAR_EXTENSION THRESHOLD {threshold:F2} BASE_FARE {baseFare:F2} RATE_PER_KM {rate:F6}");
            }

            // ---------- 特殊线路规则 ----------
            // 按线路ID分组，同一线路的规则放在一起
            var grouped = _specials.GroupBy(s => s.LineId);
            foreach (var group in grouped)
            {
                foreach (var sp in group)
                {
                    if (sp.Type == "all")
                        sb.AppendLine($"    SPECIAL_LINE {sp.LineId} ALL {sp.Price:F2}");
                    else
                        sb.AppendLine($"    SPECIAL_LINE {sp.LineId} SEGMENT '{sp.StartStation}' '{sp.EndStation}' {sp.Price:F2}");
                }
            }

            sb.AppendLine("END_DEFINE");
            PreviewTextBox.Text = sb.ToString().TrimEnd();
        }
        #endregion

        #region 辅助功能
        /// <summary>
        /// 复制生成的代码到剪贴板
        /// </summary>
        private void CopyPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PreviewTextBox.Text))
            {
                MessageBox.Show("没有可复制的内容", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                MainWindow.InsertText(PreviewTextBox.Text);
                MessageBox.Show("代码已复制到主界面", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        #endregion

        #region ViewModel 定义（用于数据绑定）
        /// <summary>
        /// 普通票价分段视图模型
        /// </summary>
        public class SegmentViewModel : INotifyPropertyChanged
        {
            private double _start;
            private double _end;
            private double _price;

            public double Start { get => _start; set { _start = value; OnPropertyChanged(); } }
            public double End { get => _end; set { _end = value; OnPropertyChanged(); } }
            public double Price { get => _price; set { _price = value; OnPropertyChanged(); } }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = "") =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 特殊票价规则视图模型
        /// </summary>
        public class SpecialFareViewModel : INotifyPropertyChanged
        {
            private string _lineId;
            private string _type; // "all" 或 "segment"
            private double _price;
            private string _startStation;
            private string _endStation;
            private Dictionary<string, string> _lineNameMap;

            /// <summary>
            /// 注入线路名称映射，用于显示友好名称
            /// </summary>
            public void SetLineNameMap(Dictionary<string, string> map) => _lineNameMap = map;

            // ---------- 原始属性 ----------
            public string LineId
            {
                get => _lineId;
                set { _lineId = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayLine)); }
            }

            /// <summary>
            /// 显示用的线路名称（如"北京地铁1号线"）
            /// </summary>
            public string DisplayLine => _lineNameMap != null && _lineNameMap.TryGetValue(LineId, out var name) ? name : LineId;

            public string Type
            {
                get => _type;
                set { _type = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypeDisplay)); }
            }

            /// <summary>
            /// 显示用的类型名称（如"统一票价(ALL)"）
            /// </summary>
            public string TypeDisplay
            {
                get => Type == "all" ? "统一票价(ALL)" : "区间票价";
                set { Type = value == "统一票价(ALL)" ? "all" : "segment"; }
            }

            public double Price { get => _price; set { _price = value; OnPropertyChanged(); } }
            public string StartStation { get => _startStation; set { _startStation = value; OnPropertyChanged(); } }
            public string EndStation { get => _endStation; set { _endStation = value; OnPropertyChanged(); } }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = "") =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}