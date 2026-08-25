using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.Windows.Controls
{
    public partial class TimeParametersPanel : UserControl
    {
        public static readonly DependencyProperty ParsedDataProperty =
            DependencyProperty.Register(nameof(ParsedData), typeof(ParsedData), typeof(TimeParametersPanel),
                new PropertyMetadata(null, OnParsedDataChanged));

        public ParsedData ParsedData
        {
            get => (ParsedData)GetValue(ParsedDataProperty);
            set => SetValue(ParsedDataProperty, value);
        }

        public event EventHandler? EditTransfersRequested;
        public event EventHandler? EditDwellTimesRequested;

        private Dictionary<string, string> _lineNameMap = new();

        public ObservableCollection<TransferDisplayItem> TransferViewModels { get; } = new();
        public ObservableCollection<DwellTimeDisplayItem> DwellTimeViewModels { get; } = new();

        public TimeParametersPanel() => InitializeComponent();

        private static void OnParsedDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TimeParametersPanel)d;
            control.UpdateData(e.NewValue as ParsedData);
        }

        private void UpdateData(ParsedData? data)
        {
            TransferViewModels.Clear();
            DwellTimeViewModels.Clear();

            if (data == null) return;

            _lineNameMap = data.Lines.ToDictionary(
                kv => kv.Key,
                kv => (kv.Value.City == data.MainCity) ? kv.Value.Name : $"{kv.Value.City}{kv.Value.Name}"
            );

            // 换乘时间
            foreach (var tr in data.Transfers)
            {
                var vm = new TransferDisplayItem
                {
                    Station = tr.Station,
                    DisplayLine1 = _lineNameMap.TryGetValue(tr.L1, out var n1) ? n1 : tr.L1,
                    DisplayLine2 = _lineNameMap.TryGetValue(tr.L2, out var n2) ? n2 : tr.L2,
                    StatusDisplay = tr.Status switch
                    {
                        "operating" => "站内换乘",
                        "out_station" => "出站换乘",
                        "pending" => "暂缓换乘",
                        _ => tr.Status
                    },
                    TimeMin = tr.Time.HasValue ? tr.Time.Value / 60.0 : data.DefaultInStationTime / 60.0
                };
                TransferViewModels.Add(vm);
            }
            TransferDataGrid.ItemsSource = TransferViewModels;

            // 停站时间
            foreach (var st in data.DwellTimes)
            {
                var vm = new DwellTimeDisplayItem
                {
                    Station = st.Key.Station,
                    DisplayLine = _lineNameMap.TryGetValue(st.Key.LineId, out var n) ? n : st.Key.LineId,
                    TimeMin = st.Value / 60.0
                };
                DwellTimeViewModels.Add(vm);
            }
            DwellTimeDataGrid.ItemsSource = DwellTimeViewModels;
        }

        private void EditTransfers_Click(object sender, RoutedEventArgs e) => EditTransfersRequested?.Invoke(this, EventArgs.Empty);
        private void EditDwellTimes_Click(object sender, RoutedEventArgs e) => EditDwellTimesRequested?.Invoke(this, EventArgs.Empty);
    }

    public class TransferDisplayItem
    {
        public string Station { get; set; } = string.Empty;
        public string DisplayLine1 { get; set; } = string.Empty;
        public string DisplayLine2 { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public double TimeMin { get; set; }
    }

    public class DwellTimeDisplayItem
    {
        public string Station { get; set; } = string.Empty;
        public string DisplayLine { get; set; } = string.Empty;
        public double TimeMin { get; set; }
    }
}