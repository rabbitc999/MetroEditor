using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.Windows.Controls
{
    public partial class FareParametersPanel : UserControl
    {
        public static readonly DependencyProperty ParsedDataProperty =
            DependencyProperty.Register(nameof(ParsedData), typeof(ParsedData), typeof(FareParametersPanel),
                new PropertyMetadata(null, OnParsedDataChanged));

        public ParsedData ParsedData
        {
            get => (ParsedData)GetValue(ParsedDataProperty);
            set => SetValue(ParsedDataProperty, value);
        }

        public event EventHandler? EditFareRequested;
        public event EventHandler? EditSpecialFareRequested;

        private Dictionary<string, string> _lineNameMap = new();

        public ObservableCollection<FareDisplayItem> FareViewModels { get; } = new();
        public ObservableCollection<SpecialFareDisplayItem> SpecialFareViewModels { get; } = new();

        public FareParametersPanel() => InitializeComponent();

        private static void OnParsedDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (FareParametersPanel)d;
            control.UpdateData(e.NewValue as ParsedData);
        }

        private void UpdateData(ParsedData? data)
        {
            FareViewModels.Clear();
            SpecialFareViewModels.Clear();

            if (data == null) return;

            // 构建线路名称映射
            _lineNameMap = data.Lines.ToDictionary(
                kv => kv.Key,
                kv => (kv.Value.City == data.MainCity) ? kv.Value.Name : $"{kv.Value.City}{kv.Value.Name}"
            );

            // 普通票价
            foreach (var rule in data.MoneyRules)
            {
                var vm = new FareDisplayItem();
                if (rule is MoneyStartRule start)
                {
                    vm.Type = "起步价";
                    vm.Price = start.Price;
                    vm.Note = $"起步价 {start.Price:F2} 元";
                }
                else if (rule is MoneySegmentRule seg)
                {
                    vm.Type = "分段";
                    vm.Start = seg.Start;
                    vm.End = seg.End;
                    vm.Price = seg.Price;
                    vm.Note = $"{seg.Start:F2}~{seg.End:F2} km 票价 {seg.Price:F2} 元";
                }
                else if (rule is MoneyCalcRule calc)
                {
                    vm.Type = "线性扩展";
                    vm.Start = calc.DistSegment;
                    vm.Price = calc.AddPrice;
                    vm.Note = $"每 {calc.DistSegment:F2} km 加收 {calc.AddPrice:F2} 元";
                }
                FareViewModels.Add(vm);
            }
            FareDataGrid.ItemsSource = FareViewModels;

            // 特殊票价（需 ParsedData 有 SpecialLines 属性，若无则跳过）
            // 假设 ParsedData.SpecialLines 存在
            if (data.SpecialLines != null)
            {
                foreach (var sl in data.SpecialLines)
                {
                    var vm = new SpecialFareDisplayItem();
                    if (sl.UseLines != null && sl.UseLines.Count > 0)
                    {
                        vm.UseLines = string.Join(", ", sl.UseLines.Select(id => _lineNameMap.TryGetValue(id, out var n) ? n : id));
                    }
                    else
                    {
                        vm.DisplayLine = _lineNameMap.TryGetValue(sl.LineId, out var n) ? n : sl.LineId;
                    }
                    if (sl.All.HasValue)
                    {
                        vm.TypeDisplay = "统一票价";
                        vm.Price = sl.All.Value;
                    }
                    else if (sl.DefaultFare.HasValue || (sl.Segments?.Any() == true))
                    {
                        vm.TypeDisplay = "完整票价";
                        vm.DefaultFare = sl.DefaultFare;
                        if (sl.Segments != null && sl.Segments.Any())
                        {
                            vm.SegmentsSummary = string.Join("; ", sl.Segments.Select(s => $"{s.RangeStart}-{s.RangeEnd}:{s.Fare}"));
                        }
                        if (sl.LinearExtension != null)
                        {
                            vm.LinearExtensionRate = sl.LinearExtension.RatePerKm;
                        }
                    }
                    else if (sl.Surcharge != null && sl.Surcharge.Any())
                    {
                        vm.TypeDisplay = "区间附加费";
                        vm.StartStation = sl.Surcharge.First().NodeA;
                        vm.EndStation = sl.Surcharge.First().NodeB;
                        vm.Price = sl.Surcharge.First().Price;
                    }
                    SpecialFareViewModels.Add(vm);
                }
            }
            SpecialFareDataGrid.ItemsSource = SpecialFareViewModels;
        }

        private void EditFare_Click(object sender, RoutedEventArgs e) => EditFareRequested?.Invoke(this, EventArgs.Empty);
        private void EditSpecialFare_Click(object sender, RoutedEventArgs e) => EditSpecialFareRequested?.Invoke(this, EventArgs.Empty);
    }

    // ViewModel 类（可复用原 MainWindow 中的定义）
    public class FareDisplayItem
    {
        public string Type { get; set; } = string.Empty;
        public double Start { get; set; }
        public double End { get; set; }
        public double Price { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class SpecialFareDisplayItem
    {
        public string DisplayLine { get; set; } = string.Empty;
        public string UseLines { get; set; } = string.Empty;
        public string TypeDisplay { get; set; } = string.Empty;
        public double Price { get; set; }
        public string StartStation { get; set; } = string.Empty;
        public string EndStation { get; set; } = string.Empty;
        public double? DefaultFare { get; set; }
        public string SegmentsSummary { get; set; } = string.Empty;
        public double? LinearExtensionRate { get; set; }
    }
}