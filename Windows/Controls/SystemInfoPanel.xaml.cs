using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace MetroEditor.Windows.Controls
{
    public partial class SystemInfoPanel : UserControl
    {
        // ========== 依赖属性 ==========
        public static readonly DependencyProperty ParsedDataProperty =
            DependencyProperty.Register(nameof(ParsedData), typeof(ParsedData), typeof(SystemInfoPanel),
                new PropertyMetadata(null, OnParsedDataChanged));

        public ParsedData ParsedData
        {
            get => (ParsedData)GetValue(ParsedDataProperty);
            set => SetValue(ParsedDataProperty, value);
        }

        public static readonly DependencyProperty DatasetInfoProperty =
            DependencyProperty.Register(nameof(DatasetInfo), typeof(string), typeof(SystemInfoPanel),
                new PropertyMetadata(""));

        public string DatasetInfo
        {
            get => (string)GetValue(DatasetInfoProperty);
            set => SetValue(DatasetInfoProperty, value);
        }

        public static readonly DependencyProperty DefaultSettingsProperty =
            DependencyProperty.Register(nameof(DefaultSettings), typeof(string), typeof(SystemInfoPanel),
                new PropertyMetadata(""));

        public string DefaultSettings
        {
            get => (string)GetValue(DefaultSettingsProperty);
            set => SetValue(DefaultSettingsProperty, value);
        }

        public static readonly DependencyProperty PromotionsDisplayProperty =
            DependencyProperty.Register(nameof(PromotionsDisplay), typeof(string), typeof(SystemInfoPanel),
                new PropertyMetadata(""));

        public string PromotionsDisplay
        {
            get => (string)GetValue(PromotionsDisplayProperty);
            set => SetValue(PromotionsDisplayProperty, value);
        }

        // ========== 构造函数 ==========
        public SystemInfoPanel()
        {
            InitializeComponent();
            DataContext = this;
            UpdateDisplay(null);
        }

        // ========== 数据更新 ==========
        private static void OnParsedDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (SystemInfoPanel)d;
            control.UpdateDisplay(e.NewValue as ParsedData);
        }

        private void UpdateDisplay(ParsedData data)
        {
            if (data == null)
            {
                DatasetInfo = "无";
                DefaultSettings = "无";
                PromotionsDisplay = "无";
            }
            else
            {
                BuildDatasetInfo(data);
                BuildDefaultSettings(data);
                BuildPromotionsDisplay(data);
            }
        }

        // ========== 数据集信息 ==========
        private void BuildDatasetInfo(ParsedData data)
        {
            var sb = new StringBuilder();
            bool hasInfo = false;

            if (!string.IsNullOrEmpty(data.MainCity))
            {
                sb.AppendLine($"主城市: {data.MainCity}");
                hasInfo = true;
            }
            if (!string.IsNullOrEmpty(data.Version))
            {
                sb.AppendLine($"版本: {data.Version}");
                hasInfo = true;
            }
            if (!string.IsNullOrEmpty(data.UpdateDate))
            {
                sb.AppendLine($"更新日期: {data.UpdateDate}");
                hasInfo = true;
            }
            if (!string.IsNullOrEmpty(data.CreateDate))
            {
                sb.AppendLine($"创建日期: {data.CreateDate}");
                hasInfo = true;
            }
            if (!string.IsNullOrEmpty(data.Author))
            {
                sb.AppendLine($"作者: {data.Author}");
                hasInfo = true;
            }
            if (data.OtherCities != null && data.OtherCities.Any())
            {
                sb.AppendLine($"其他城市: {string.Join(", ", data.OtherCities)}");
                hasInfo = true;
            }

            DatasetInfo = hasInfo ? sb.ToString() : "无";
        }

        // ========== 默认设置 ==========
        private void BuildDefaultSettings(ParsedData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"默认站内换乘时间: {data.DefaultInStationTime:F1} 秒");
            sb.AppendLine($"默认出站换乘时间: {data.DefaultOutStationTime:F1} 秒");
            sb.AppendLine($"默认停站时间: {data.DefaultDwellTime:F1} 秒");
            sb.AppendLine($"默认进站时间: {data.DefaultEntryTime:F1} 秒");
            sb.AppendLine($"默认上车时间: {data.DefaultBoardTime:F1} 秒");

            var startRule = data.MoneyRules?.OfType<MoneyStartRule>().FirstOrDefault();
            sb.AppendLine($"默认起步票价: {(startRule != null ? $"{startRule.Price:F2} 元" : "未定义")}");

            DefaultSettings = sb.ToString();
        }

        // ========== 优惠策略 ==========
        private void BuildPromotionsDisplay(ParsedData data)
        {
            if (data.Promotions == null || !data.Promotions.Any())
            {
                PromotionsDisplay = "无";
                return;
            }

            var sb = new StringBuilder();
            foreach (var policy in data.Promotions)
            {
                sb.AppendLine($"ID: {policy.Id}");
                sb.AppendLine($"  名称: {policy.Name}");
                sb.AppendLine($"  类型: {policy.Type} (值: {policy.Value})");
                sb.AppendLine($"  描述: {policy.Description}");
                var lines = policy.ApplicableLines;
                sb.AppendLine($"  适用线路: {(lines != null && lines.Any() ? string.Join(", ", lines) : "全网")}");
                if (!string.IsNullOrEmpty(policy.StartTime) && !string.IsNullOrEmpty(policy.EndTime))
                    sb.AppendLine($"  时间: {policy.StartTime} - {policy.EndTime}");
                if (policy.Days != null && policy.Days.Any())
                    sb.AppendLine($"  星期: {string.Join(", ", policy.Days)}");
                sb.AppendLine();
            }
            PromotionsDisplay = sb.ToString();
        }
    }
}