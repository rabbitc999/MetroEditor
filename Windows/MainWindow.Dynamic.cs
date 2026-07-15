using MetroEditor.MetroLang.Compiler;
using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 主窗口 — 整合编辑、编译、预览与数据管理
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 编译与数据更新
        private void TextEditor_TextChanged(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        /// <summary>
        /// 输入防抖回调：触发增量编译
        /// 设计要点：
        /// 1. 500ms防抖：避免输入过程中频繁编译阻塞UI
        /// 2. 双检锁+_isCompiling标志：防止并发编译冲突
        /// 3. 后台线程执行：编译逻辑完全脱离UI线程，保证输入流畅
        /// </summary>
        private async void DynamicCompile()
        {
            if (_isCompiling) return;
            lock (_compileLock)
            {
                if (_isCompiling) return;
                _isCompiling = true;
            }

            string text = textEditor.Text;
            var dispatcher = Dispatcher;

            try
            {
                var result = await Task.Run(() => CompileInBackground(text));
                dispatcher.Invoke(() => ApplyCompileResult(result));
            }
            finally
            {
                lock (_compileLock) _isCompiling = false;
            }
        }

        /// <summary>
        /// 后台编译逻辑：纯CPU计算，无UI依赖
        /// 设计要点：
        /// 1. 返回元组结构：同时返回编译消息、状态文本、解析数据、JSON根，避免多次解析
        /// 2. 异常捕获：编译错误转换为用户友好的消息列表，不直接崩溃
        /// </summary>
        private (List<CompileMessage> Messages, string StatusText, ParsedData? Data, JsonRoot? Root) CompileInBackground(string text)
        {
            var messages = new List<CompileMessage>();
            string statusText = DefaultStatus;
            ParsedData? data = null;
            JsonRoot? root = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                messages.Add(new CompileMessage { ColorBrush = Brushes.Gray, Text = "请输入 MetroLang 代码" });
                statusText = "等待输入";
                return (messages, statusText, data, root);
            }

            var parser = new MetorCompiler();
            try
            {
                parser.Parse(new StringBuilder().Append(" ").Append(text).ToString());
                data = parser.GetParsedData();
                root = parser.ExportJson();
                string successText = $"编译成功 | 线路: {data.Lines.Count}，站台: {data.Stations.Count}，方向: {data.Directions.Count}";
                messages.Add(new CompileMessage { ColorBrush = Brushes.Green, Text = successText });
                statusText = successText;
            }
            catch (Exception ex)
            {
                var errorLines = ex.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in errorLines)
                    messages.Add(new CompileMessage { ColorBrush = Brushes.Red, Text = line.Trim() });
                int errorCount = errorLines.Length - 1;
                statusText = $"编译失败，{errorCount}{(errorCount >= 48 ? "+" : "")} 个错误";
            }

            // 添加警告信息
            var warnings = parser.Warnings;
            foreach (var w in warnings)
            {
                messages.Add(new CompileMessage { Text = w, ColorBrush = Brushes.Orange });
            }

            return (messages, statusText, data, root);
        }

        /// <summary>
        /// 编译结果应用入口：统一更新UI和地图
        /// 设计要点：
        /// 1. 作为UI更新的唯一入口，避免散弹式更新
        /// 2. 仅在ParsedData有效时才刷新DataGrid，减少无效重绘
        /// 3. 编译成功后触发地图重绘，保持数据强一致性
        /// </summary>
        private void ApplyCompileResult((List<CompileMessage> Messages, string StatusText, ParsedData? Data, JsonRoot? Root) result)
        {
            errorListBox.ItemsSource = result.Messages;
            statusInfo.Text = result.StatusText;

            if (result.Data != null)
            {
                UpdateUI(result.Data);
                _currentRoot = result.Root;
                mapControl.LoadData(result.Root);
            }
        }

        /// <summary>
        /// 增量更新UI：仅当数据实际变化时才刷新DataGrid
        /// 设计要点：
        /// 1. 深度Diff：比较列表的每个元素，而非仅引用比较
        /// 2. 避免闪烁：减少DataGrid的ItemsSource重置次数，保留滚动位置和选中态
        /// 3. 联动更新：数据变化后同步刷新详情面板和右侧Tab页
        /// </summary>
        private bool UpdateUI(ParsedData data)
        {
            _lineNameMap = data.Lines.ToDictionary(
                kv => kv.Key,
                kv =>
                {
                    string city = kv.Value.City ?? data.MainCity;
                    return (city == data.MainCity) ? kv.Value.Name : $"{city}{kv.Value.Name}";
                }
            );

            var newDirList = BuildDirectionViewModels(data);
            var newStationList = BuildStationViewModels(data);
            var newLineList = BuildLineViewModels(data);

            bool changed = false;

            if (!AreListsEqual(_lastDirList, newDirList, (a, b) => a.DirId == b.DirId && a.DisplayLine == b.DisplayLine && a.Start == b.Start && a.End == b.End && a.Attrs == b.Attrs))
            {
                _lastDirList = newDirList;
                _directionView = CollectionViewSource.GetDefaultView(newDirList);
                directionGrid.ItemsSource = _directionView;
                changed = true;
            }

            if (!AreListsEqual(_lastStationList, newStationList, (a, b) => a.Name == b.Name && Math.Abs(a.Lon - b.Lon) < 1e-9 && Math.Abs(a.Lat - b.Lat) < 1e-9 && a.Lines == b.Lines))
            {
                _lastStationList = newStationList;
                _allStations.Clear();
                _allStations.AddRange(newStationList);
                _stationView = CollectionViewSource.GetDefaultView(newStationList);
                stationGrid.ItemsSource = _stationView;
                changed = true;
            }

            if (!AreListsEqual(_lastLineList, newLineList, (a, b) => a.LineId == b.LineId && a.Name == b.Name && a.Color == b.Color && a.City == b.City && a.Formation == b.Formation && a.Maxspeed == b.Maxspeed && a.OpenYear == b.OpenYear && a.PowerKw == b.PowerKw && a.Brake == b.Brake && a.Opened == b.Opened))
            {
                _lastLineList = newLineList;
                _lineView = CollectionViewSource.GetDefaultView(newLineList);
                lineGrid.ItemsSource = _lineView;
                changed = true;
            }

            _currentParsedData = data;

            // 刷新详情面板
            DirectionGrid_SelectionChanged(directionGrid, null);
            LineGrid_SelectionChanged(lineGrid, null);
            StationGrid_SelectionChanged(stationGrid, null);

            // 更新右侧Tab数据
            UpdateTransferData(data);
            UpdateDwellTimeData(data);
            UpdateFareData(data);
            UpdateSpecialFareData(data);

            return changed;
        }


        private void UpdateTransferData(ParsedData data)
        {
            TransferViewModels.Clear();
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
        }

        private void UpdateDwellTimeData(ParsedData data)
        {
            DwellTimeViewModels.Clear();
            foreach (var st in data.DwellTimes)
            {
                var vm = new DwellTimeDisplayItem
                {
                    Station = st.Key.Station,
                    DisplayLine = _lineNameMap.TryGetValue(st.Key.LineId, out var n) ? n : st.Key.LineId,
                    TimeMin = st.Value
                };
                DwellTimeViewModels.Add(vm);
            }
            DwellTimeDataGrid.ItemsSource = DwellTimeViewModels;
        }

        private void UpdateFareData(ParsedData data)
        {
            FareViewModels.Clear();
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
        }

        private void UpdateSpecialFareData(ParsedData data)
        {
            SpecialFareViewModels.Clear();
            foreach (var sl in data.SpecialLines) // 需要确保ParsedData有此属性
            {
                var vm = new SpecialFareDisplayItem();
                // 如果是线路组（UseLines非空）
                if (sl.UseLines != null && sl.UseLines.Count > 0)
                {
                    vm.UseLines = string.Join(", ", sl.UseLines.Select(id => _lineNameMap.TryGetValue(id, out var n) ? n : id));
                }
                else
                {
                    vm.DisplayLine = _lineNameMap.TryGetValue(sl.LineId, out var n) ? n : sl.LineId;
                }
                // 判断类型：有ALL表示统一票价，有DefaultFare或Segments表示完整票价
                if (sl.All.HasValue)
                {
                    vm.TypeDisplay = "统一票价";
                    vm.Price = sl.All.Value;
                }
                else if (sl.DefaultFare.HasValue || (sl.Segments?.Any() == true))
                {
                    vm.TypeDisplay = "完整票价";
                    vm.DefaultFare = sl.DefaultFare;
                    // 显示分段摘要
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
                    // 显示区间信息
                    vm.StartStation = sl.Surcharge.First().NodeA;
                    vm.EndStation = sl.Surcharge.First().NodeB;
                    vm.Price = sl.Surcharge.First().Price;
                }
                SpecialFareViewModels.Add(vm);
            }
            SpecialFareDataGrid.ItemsSource = SpecialFareViewModels;
        }

        private static List<DirectionViewModel> BuildDirectionViewModels(ParsedData data)
        {
            // 属性显示映射（仅用于界面展示）
            var attrDisplayMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LOOP"] = "环线",
                ["BRANCH"] = "支线",
                ["FAST"] = "快车",
                ["INNER"] = "内环",
                ["OUTER"] = "外环"
            };

            var list = new List<DirectionViewModel>();
            foreach (var kv in data.Directions)
            {
                var dir = kv.Value;
                var lineInfo = data.Lines.TryGetValue(dir.LineId, out var li) ? li : null;
                string displayLine = lineInfo != null ? $"{lineInfo.City}{lineInfo.Name} ({dir.LineId})" : dir.LineId;

                // 构建显示属性列表
                var displayAttrs = new List<string>();

                // 处理环线
                if (dir.Attrs.Contains("LOOP"))
                {
                    // 如果有方向类型（INNER/OUTER），显示为"内环"或"外环"
                    if (!string.IsNullOrEmpty(dir.DirectionType))
                    {
                        string dirType = dir.DirectionType.ToLowerInvariant() == "inner" ? "内环" : "外环";
                        displayAttrs.Add(dirType);
                    }
                    else
                    {
                        displayAttrs.Add("环线");
                    }
                }

                // 处理支线
                if (dir.Attrs.Contains("BRANCH"))
                    displayAttrs.Add("支线");

                // 处理快车
                if (dir.Attrs.Contains("FAST"))
                    displayAttrs.Add("快车");

                // 注意：INNER/OUTER 已在 LOOP 中合并处理，不再单独追加

                string attrsDisplay = string.Join(", ", displayAttrs);

                list.Add(new DirectionViewModel
                {
                    DirId = kv.Key,
                    DisplayLine = displayLine,
                    Start = dir.Stations.FirstOrDefault(),
                    End = dir.Stations.LastOrDefault(),
                    Attrs = attrsDisplay   // 现在显示的是中文友好名称
                });
            }
            return list;
        }

        private static List<StationViewModel> BuildStationViewModels(ParsedData data)
        {
            var list = new List<StationViewModel>();
            var lineNameMap = new Dictionary<string, string>();
            foreach (var kv in data.Lines)
            {
                string lid = kv.Key;
                var lineInfo = kv.Value;
                string city = lineInfo.City ?? data.MainCity;
                string displayName = (city == data.MainCity) ? lineInfo.Name : $"{city}{lineInfo.Name}";
                lineNameMap[lid] = displayName;
            }

            foreach (var kv in data.Stations)
            {
                var st = kv.Value;
                var lineIds = data.Directions.Values
                    .Where(d => d.Stations.Contains(kv.Key))
                    .Select(d => d.LineId)
                    .Distinct()
                    .ToList();

                var lineDisplayNames = lineIds
                    .OrderBy(id => id)
                    .Select(id => lineNameMap.GetValueOrDefault(id, id))
                    .ToList();

                // 获取城市
                string city = data.StationCities.TryGetValue(kv.Key, out var c) ? c : data.MainCity;

                list.Add(new StationViewModel
                {
                    Name = kv.Key,
                    Lon = st.Lon,
                    Lat = st.Lat,
                    Lines = string.Join(", ", lineDisplayNames),
                    City = city   // 新增
                });
            }
            return list;
        }

        private static List<LineViewModel> BuildLineViewModels(ParsedData data)
        {
            var list = new List<LineViewModel>();
            foreach (var kv in data.Lines)
            {
                var line = kv.Value;
                list.Add(new LineViewModel
                {
                    LineId = kv.Key,
                    Name = line.Name,
                    Color = line.Color,
                    City = line.City,
                    Formation = line.Formation ?? string.Empty,
                    Maxspeed = line.Maxspeed,
                    OpenYear = line.OpenYear,
                    PowerKw = line.PowerKw,
                    Brake = line.Brake,
                    Opened = !line.Disabled
                });
            }
            return list;
        }

        /// <summary>
        /// 通用列表比较方法：支持任意类型的相等性判断
        /// 设计要点：
        /// 1. 泛型委托：将相等性判断逻辑交给调用方，灵活适配不同ViewModel
        /// 2. 空值安全：处理null列表的边界情况
        /// </summary>
        private static bool AreListsEqual<T>(List<T>? a, List<T>? b, Func<T, T, bool> comparer)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!comparer(a[i], b[i])) return false;
            return true;
        }
        #endregion

        #region 生成窗口辅助（调用现有对话框）
        private void GenerateLine_Click(object sender, EventArgs e)
        {
            var existingIds = new HashSet<string>(_currentParsedData?.Lines.Keys ?? Enumerable.Empty<string>());
            var dialog = new EditLineWindow(existingIds) { };
            dialog.Show();
        }

        private void GenerateDirection_Click(object sender, EventArgs e)
        {
            if (_currentParsedData == null || _currentParsedData.Stations.Count == 0)
            {
                MessageBox.Show("请先编译文件以获得站台列表", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var stationNames = _currentParsedData.Stations.Keys.OrderBy(x => x).ToList();
            var existingIds = new HashSet<string>(_currentParsedData.Lines.Keys);
            var dialog = new EditDirectionWindow(stationNames, existingIds);
            dialog.Show();
        }

        /// <summary>
        /// 获取站点所属城市：跨窗口复用的工具方法
        /// 设计要点：
        /// 1. 优先主城市：若站点同时属于主城和其他城市，默认返回主城市
        /// 2. 兜底逻辑：无归属城市时返回主城市，避免空值
        /// </summary>
        public static string GetStationCity(string stationName, ParsedData data)
        {
            var lineIds = data.Directions.Values
                .Where(d => d.Stations.Contains(stationName))
                .Select(d => d.LineId)
                .Distinct();

            var cities = lineIds
                .Select(id => data.Lines.TryGetValue(id, out var li) ? li.City : null)
                .Where(c => c != null)
                .Cast<string>()
                .ToHashSet();

            return cities.Contains(data.MainCity) ? data.MainCity : (cities.FirstOrDefault() ?? data.MainCity);
        }
        #endregion

        #region 筛选与搜索
        private void StationFilterTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter(_stationView, StationFilterTextBox.Text, StationFilterColumn);
        private void StationClearFilter_Click(object sender, RoutedEventArgs e) { StationFilterTextBox.Text = string.Empty; ApplyFilter(_stationView, string.Empty, StationFilterColumn); }
        private void DirectionFilterTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter(_directionView, DirectionFilterTextBox.Text, DirectionFilterColumn);
        private void DirectionClearFilter_Click(object sender, RoutedEventArgs e) { DirectionFilterTextBox.Text = string.Empty; ApplyFilter(_directionView, string.Empty, DirectionFilterColumn); }
        private void LineFilterTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter(_lineView, LineFilterTextBox.Text, LineFilterColumn);
        private void LineClearFilter_Click(object sender, RoutedEventArgs e) { LineFilterTextBox.Text = string.Empty; ApplyFilter(_lineView, string.Empty, LineFilterColumn); }

        // 站台筛选列变更
        private void StationFilterColumn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_stationView == null || StationFilterTextBox == null || StationFilterColumn == null)
                return;
            ApplyFilter(_stationView, StationFilterTextBox.Text, StationFilterColumn);
        }

        // 方向筛选列变更
        private void DirectionFilterColumn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_directionView == null || DirectionFilterTextBox == null || DirectionFilterColumn == null)
                return;
            ApplyFilter(_directionView, DirectionFilterTextBox.Text, DirectionFilterColumn);
        }

        // 线路筛选列变更
        private void LineFilterColumn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_lineView == null || LineFilterTextBox == null || LineFilterColumn == null)
                return;
            ApplyFilter(_lineView, LineFilterTextBox.Text, LineFilterColumn);
        }

        private static void ApplyFilter(ICollectionView? view, string keyword, ComboBox columnCombo)
        {
            if (view == null) return;
            keyword = keyword.Trim();
            string column = (columnCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(column))
            {
                view.Filter = null;
                return;
            }

            view.Filter = item =>
            {
                if (item is StationViewModel st)
                {
                    return column switch
                    {
                        "Name" => st.Name.Contains(keyword),
                        "Lines" => st.Lines.Contains(keyword),
                        "City" => st.City.Contains(keyword),
                        _ => true
                    };
                }
                if (item is DirectionViewModel d)
                {
                    return column switch
                    {
                        "DirId" => d.DirId.Contains(keyword),
                        "DisplayLine" => d.DisplayLine.Contains(keyword),
                        "Start" => d.Start.Contains(keyword),
                        "End" => d.End.Contains(keyword),
                        "Attrs" => d.Attrs.Contains(keyword),
                        _ => true
                    };
                }
                if (item is LineViewModel l)
                {
                    return column switch
                    {
                        "LineId" => l.LineId.Contains(keyword),
                        "Name" => l.Name.Contains(keyword),
                        "City" => l.City.Contains(keyword),
                        _ => true
                    };
                }
                return true;
            };
            view.Refresh();
        }
        #endregion
    }
}