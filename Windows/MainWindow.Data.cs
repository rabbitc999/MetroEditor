using ICSharpCode.AvalonEdit.Folding;
using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.Windows
{
    /// <summary>
    /// 主窗口 — 整合编辑、编译、预览与数据管理
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 公开静态属性（用于 XAML 绑定）
        public static List<KeyValuePair<string, string>> Keywords { get; } = new()
        {
            // ----- 全局与城市 -----
            new("PROGRAM_DEFINE", "程序定义块"),
            new("OTHER_CITY", "其他城市列表"),
            new("VERSION", "版本号"),
            new("UPDATE_DATE", "更新日期"),

            // ----- 站点与状态 -----
            new("STATION_STATUS", "车站状态"),
            new("STATION_DEFINE", "站点定义块"),
            new("STATION_MODE", "站点进出模式"),

            // ----- 线路定义 -----
            new("LINE_DEFINE", "线路定义块"),

            // ----- 方向定义（含子命令） -----
            new("DIRECTION_DEFINE", "方向定义块"),
            new("STATIONS", "方向站点列表"),
            new("NICK_NAME", "方向别名"),
            new("NICK_COLOR", "方向别名颜色"),
            new("LOOP", "环线"),
            new("BRANCH", "支线"),
            new("FAST", "快车"),
            new("INNER", "内环"),
            new("OUTER", "外环"),
            new("FUTURE", "建设中/规划中"),
            new("REV", "反向"),            // 方向内子命令

            // ----- 换乘 -----
            new("TRANSFER_TYPE", "换乘类型定义"),
            new("TRANSFER", "换乘"),
            new("TRANSFER_DEFINE", "换乘定义块"),
            new("VIRTUAL_TRANSFER_DEFINE", "虚拟换乘定义块"),

            // ----- 票价 -----
            new("PRICE_DEFINE", "票价定义块"),
            new("SPECIAL_DEFINE", "特殊票价块（线路内）"),
            new("CURRENCY", "货币"),
            new("DEFAULT_FARE", "默认票价"),
            new("SEGMENT", "分段票价"),
            new("LINEAR_EXTENSION", "线性延伸"),
            new("THRESHOLD", "线性延伸阈值"),
            new("BASE_FARE", "线性延伸基础票价"),
            new("RATE_PER_KM", "线性延伸费率"),
            new("TRANSFER_DISCOUNT", "换乘优惠"),
            new("ENABLED", "启用"),
            new("DISCOUNT_AMOUNT", "优惠金额"),
            new("TIME_LIMIT", "时间限制"),
            new("FARE_CAP", "票价上限"),
            new("SPECIAL_LINE", "特殊线路（票价块内）"),

            // ----- 运营参数 -----
            new("OPERATION_DEFINE", "运营参数定义块"),
            new("TRANSFER_TIME", "平均换乘时间"),
            new("OUT_STATION_TRANSFER_TIME", "出站换乘时间"),
            new("DWELL_TIME", "平均停站时间"),
            new("ENTRY_TIME", "平均进站时间"),

            // ----- 发车间隔与拥挤度 -----
            new("HEADWAY_PROFILE", "发车间隔时段块"),
            new("CROWD_PROFILE", "拥挤度时段块"),
            new("PERIOD", "时段定义"),
            new("TIME_RANGE", "时段关键字"),
            new("VALUE", "值关键字"),

            // ----- 停站与限速 -----
            new("DWELL_TIME", "停站时间（单站）"),
            new("DWELL_TIME_DEFINE", "停站时间批量块"),
            new("SPEED_LIMIT_DEFINE", "限速批量块"),
            new("SPEED_LIMIT", "区间限速（方向内）"),

            // ----- 其他 -----
            new("DEPOT", "车辆段"),
            new("OPEN_TIME", "运营时间"),
            new("VIA", "经由/共线"),
            new("LABEL_DEFINE", "文字标签定义块"),

            // ----- 条件编译 -----
            new("DEF", "定义编译常量"),
            new("IF", "条件编译开始"),
            new("END_IF", "条件编译结束"),

            // ----- 块结束标记 -----
            new("END_DEFINE", "块结束标记（通用）"),

            // ----- 站点/方向属性 -----
            new("NAME", "线路名称"),
            new("COLOR", "线路颜色"),
            new("FORMATION", "编组"),
            new("MAXSPEED", "最高速度"),
            new("OPEN_YEAR", "开通年份"),
            new("POWER", "功率"),
            new("BRAKE", "刹车"),
            new("DISABLE", "禁用"),
            new("SPECIAL", "特殊标记（线路）"),
            new("LINK", "线路链接"),

            // ----- 状态与模式 -----
            new("STATUS", "状态"),
            new("MODE", "进出模式"),
            new("ENTRY_ONLY", "仅进站"),
            new("EXIT_ONLY", "仅出站"),
            new("BOTH", "可进可出"),
            new("SECURITY_CHECK", "需重新安检"),
            new("NORMAL", "正常"),
            new("CLOSED", "关闭"),

            // ----- 坐标与计算 -----
            new("COORD", "站点坐标"),
            new("AUTO", "自动计算"),
            new("NIL", "空值"),
            new("ALL", "全部"),
            new("SURCHARGE", "附加费"),
            new("TRANSFER_RESTRICT", "换乘限制"),

            // ----- 星期与时段 -----
            new("MONDAY", "星期一"),
            new("TUESDAY", "星期二"),
            new("WEDNESDAY", "星期三"),
            new("THURSDAY", "星期四"),
            new("FRIDAY", "星期五"),
            new("SATURDAY", "星期六"),
            new("SUNDAY", "星期日"),
            new("WEEKEND", "周末"),
            new("WORKDAY", "工作日"),
            new("WEEKDAY", "工作日"),
            new("GLOBAL", "全局（运营时间）"),

            // ----- 其他值 -----
            new("TIME_MINUTES", "时间(分钟)"),
            new("VIA_DIRECTIONS", "经由方向列表"),
            new("TIME", "时间(秒)"),
            new("NODE", "块内节点"),
            
            // ----- 设施与卫生间 -----
            new("FACILITY_DEFINE", "设施与卫生间定义块"),
            new("TOILET", "普通卫生间"),
            new("ACCESSIBLE_TOILET", "无障碍卫生间"),
            new("ELEVATOR", "无障碍电梯"),
            new("WIDE_GATE", "宽闸机"),
            new("ESCALATOR", "自动扶梯"),
            new("TACTILE_PAVING", "盲道"),
            new("POSITION", "卫生间位置（付费区/非付费区）"),
            new("LOCATION", "电梯位置描述"),
            new("QUANTITY", "数量"),
            new("GENDER", "卫生间性别（male/female/mixed）"),

            // ----- 票价优惠策略 -----
            new("PROMOTION_DEFINE", "票价优惠策略定义块"),
            new("POLICY", "优惠策略定义"),
            new("END_POLICY", "优惠策略结束"),
            new("DESCRIPTION", "优惠描述"),
            new("APPLICABLE_LINES", "适用线路列表"),
            new("TYPE", "优惠类型（rate/fixed/fixed_discount）"),
        };

        public List<SyntaxReferenceItem> SyntaxReferences { get; } = new List<SyntaxReferenceItem>([
                // ---------- 全局与城市 ----------
                new SyntaxReferenceItem
                {
                    Command = "PROGRAM_DEFINE",
                    Format = "PROGRAM_DEFINE ... END_DEFINE",
                    Example = "PROGRAM_DEFINE KEY_CITY '广州' OTHER_CITY ['佛山', '中山'] VERSION 'V1.0' UPDATE_DATE '2026-7-11' END_DEFINE",
                    InsertTemplate = "PROGRAM_DEFINE\n    KEY_CITY '<主城市>'\n    OTHER_CITY ['<城市2>', '<城市3>']\n    VERSION '<版本>'\n    UPDATE_DATE '<日期>'\nEND_DEFINE"
                },

                // ---------- 站点 ----------
                new SyntaxReferenceItem
                {
                    Command = "STATION_DEFINE",
                    Format = "STATION_DEFINE ... END_DEFINE",
                    Example = "STATION_DEFINE NODE '体育中心' COORD 113.328 23.142 END_DEFINE",
                    InsertTemplate = "STATION_DEFINE\n    NODE '<站名>' COORD <经度> <纬度>\nEND_DEFINE"
                },
                new SyntaxReferenceItem
                {
                    Command = "STATION_STATUS",
                    Format = "STATION_STATUS <站名> [线路ID] <状态>",
                    Example = "STATION_STATUS '广州东站' GZ1 PENDING",
                    InsertTemplate = "STATION_STATUS '<站名>' <线路ID> PENDING"
                },
                new SyntaxReferenceItem
                {
                    Command = "STATION_MODE",
                    Format = "STATION_MODE <站名> <模式> [TIME_RANGE 开始 结束]",
                    Example = "STATION_MODE '广州南站' ENTRY_ONLY TIME_RANGE 06:00 22:00",
                    InsertTemplate = "STATION_MODE '<站名>' ENTRY_ONLY"
                },

                // ---------- 线路 ----------
                new SyntaxReferenceItem
                {
                    Command = "LINE_DEFINE",
                    Format = "LINE_DEFINE <线路ID> ... END_DEFINE",
                    Example = "LINE_DEFINE GZ1 NAME '1号线' COLOR '#F47920' END_DEFINE",
                    InsertTemplate = "LINE_DEFINE <线路ID>\n    NAME '<名称>'\n    COLOR '<颜色>'\nEND_DEFINE"
                },

                // ---------- 方向 ----------
                new SyntaxReferenceItem
                {
                    Command = "DIRECTION_DEFINE",
                    Format = "DIRECTION_DEFINE <方向ID> <线路ID> ... END_DEFINE",
                    Example = "DIRECTION_DEFINE GZ1_0 GZ1 STATIONS ['广州东站', '体育中心'] END_DEFINE",
                    InsertTemplate = "DIRECTION_DEFINE <方向ID> <线路ID>\n    STATIONS ['<站1>', '<站2>']\n    DISTANCE AUTO\nEND_DEFINE"
                },

                // ---------- 换乘 ----------
                new SyntaxReferenceItem
                {
                    Command = "TRANSFER",
                    Format = "TRANSFER <类型ID> '<站名>' <线路1> [<线路2>]",
                    Example = "TRANSFER TTHC '体育中心' GZ1 GZ2",
                    InsertTemplate = "TRANSFER <类型ID> '<站名>' <线路1> <线路2>"
                },
                new SyntaxReferenceItem
                {
                    Command = "TRANSFER_TYPE",
                    Format = "TRANSFER_TYPE <类型ID> '<类型名称>' ... END_DEFINE",
                    Example = "TRANSFER_TYPE TTHC '同台换乘' TIME 120 STATUS OPERATING END_DEFINE",
                    InsertTemplate = "TRANSFER_TYPE <类型ID> '<类型名称>'\n    TIME <秒数>\n    STATUS operating\n    SECURITY_CHECK\nEND_DEFINE"
                },
                new SyntaxReferenceItem
                {
                    Command = "TRANSFER_DEFINE",
                    Format = "TRANSFER_DEFINE '<站名>' <线路A> <线路B> ... END_DEFINE",
                    Example = "TRANSFER_DEFINE '体育中心' GZ1 GZ2 TIME 120 STATUS operating END_DEFINE",
                    InsertTemplate = "TRANSFER_DEFINE '<站名>' <线路A> <线路B>\n    TIME <秒数>\n    STATUS operating\nEND_DEFINE"
                },
                new SyntaxReferenceItem
                {
                    Command = "VIRTUAL_TRANSFER_DEFINE",
                    Format = "VIRTUAL_TRANSFER_DEFINE '<站A>' '<站B>' ... END_DEFINE",
                    Example = "VIRTUAL_TRANSFER_DEFINE '广州东站' '佛山新城' TIME_MINUTES 15 END_DEFINE",
                    InsertTemplate = "VIRTUAL_TRANSFER_DEFINE '<站A>' '<站B>'\n    TIME_MINUTES <分钟>\n    VIA_DIRECTIONS [<方向ID>]\nEND_DEFINE"
                },

                // ---------- 票价 ----------
                new SyntaxReferenceItem
                {
                    Command = "PRICE_DEFINE",
                    Format = "PRICE_DEFINE ... END_DEFINE",
                    Example = "PRICE_DEFINE DEFAULT_FARE 2 SEGMENT 0 6 2 END_DEFINE",
                    InsertTemplate = "PRICE_DEFINE\n    DEFAULT_FARE <起步价>\n    SEGMENT <起始> <结束> <票价>\n    LINEAR_EXTENSION THRESHOLD <阈值> BASE_FARE <基础票价> RATE_PER_KM <费率>\nEND_DEFINE"
                },

                // ---------- 运营参数 ----------
                new SyntaxReferenceItem
                {
                    Command = "OPERATION_DEFINE",
                    Format = "OPERATION_DEFINE ... END_DEFINE",
                    Example = "OPERATION_DEFINE TRANSFER_TIME 180 DEFAULT_DWELL 30 END_DEFINE",
                    InsertTemplate = "OPERATION_DEFINE\n    TRANSFER_TIME <秒>\n    DWELL_TIME <秒>\n    OUT_STATION_TRANSFER_TIME <秒>\n    ENTRY_TIME <秒>\n    DEFAULT_DWELL <秒>\nEND_DEFINE"
                },

                // ---------- 发车间隔与拥挤度 ----------
                new SyntaxReferenceItem
                {
                    Command = "HEADWAY_PROFILE",
                    Format = "HEADWAY_PROFILE <线路ID> ... END_DEFINE",
                    Example = "HEADWAY_PROFILE GZ1 PERIOD 07:00 09:00 120 END_DEFINE",
                    InsertTemplate = "HEADWAY_PROFILE <线路ID>\n    PERIOD <开始> <结束> <秒>\nEND_DEFINE"
                },
                new SyntaxReferenceItem
                {
                    Command = "CROWD_PROFILE",
                    Format = "CROWD_PROFILE <方向ID> ... END_DEFINE",
                    Example = "CROWD_PROFILE GZ1_0 PERIOD 08:00 10:00 1.2 WORKDAY END_DEFINE",
                    InsertTemplate = "CROWD_PROFILE <方向ID>\n    PERIOD <开始> <结束> <系数> [WORKDAY]\nEND_DEFINE"
                },

                // ---------- 停站与限速 ----------
                new SyntaxReferenceItem
                {
                    Command = "DWELL_TIME",
                    Format = "DWELL_TIME <站名> <线路ID> <时间秒>",
                    Example = "DWELL_TIME '广州东站' GZ1 45",
                    InsertTemplate = "DWELL_TIME '<站名>' <线路ID> <秒>"
                },
                new SyntaxReferenceItem
                {
                    Command = "DWELL_TIME_DEFINE",
                    Format = "DWELL_TIME_DEFINE ... END_DEFINE",
                    Example = "DWELL_TIME_DEFINE NODE '体育中心' GZ1 30 END_DEFINE",
                    InsertTemplate = "DWELL_TIME_DEFINE\n    NODE '<站名>' <线路ID> <秒>\nEND_DEFINE"
                },
                new SyntaxReferenceItem
                {
                    Command = "SPEED_LIMIT_DEFINE",
                    Format = "SPEED_LIMIT_DEFINE <方向ID> ... END_DEFINE",
                    Example = "SPEED_LIMIT_DEFINE GZ1_0 NODE '广州东站' '体育中心' 70 END_DEFINE",
                    InsertTemplate = "SPEED_LIMIT_DEFINE <方向ID>\n    NODE '<站A>' '<站B>' <速度>\nEND_DEFINE"
                },

                // ---------- 其他 ----------
                new SyntaxReferenceItem
                {
                    Command = "DEPOT",
                    Format = "DEPOT <线路ID> <站名> [方向ID] [首班时间]",
                    Example = "DEPOT GZ1 '广州东站' GZ1_0 06:00",
                    InsertTemplate = "DEPOT <线路ID> '<站名>' <方向ID> <HH:mm>"
                },
                new SyntaxReferenceItem
                {
                    Command = "OPEN_TIME",
                    Format = "OPEN_TIME [GLOBAL|<线路ID>] 开始 结束",
                    Example = "OPEN_TIME GLOBAL 06:00 23:00",
                    InsertTemplate = "OPEN_TIME <线路ID> <开始> <结束>"
                },
                new SyntaxReferenceItem
                {
                    Command = "VIA",
                    Format = "VIA <方向ID> <经由方向ID>",
                    Example = "VIA GZ1_0 GZ1_1",
                    InsertTemplate = "VIA <方向ID> <经由方向ID>"
                },

                // ---------- 条件编译 ----------
                new SyntaxReferenceItem
                {
                    Command = "DEF",
                    Format = "DEF <变量名> = <值>",
                    Example = "DEF ENABLE_EXTRA = TRUE",
                    InsertTemplate = "DEF <变量名> = <值>"
                },
                new SyntaxReferenceItem
                {
                    Command = "IF",
                    Format = "IF <条件> ... END_IF",
                    Example = "IF ENABLE_EXTRA OPEN_TIME GLOBAL 06:00 23:00 END_IF",
                    InsertTemplate = "IF <条件>\n    <命令>\nEND_IF"
                },

                // ---------- 标签定义 ----------
                new SyntaxReferenceItem
                {
                    Command = "LABEL_DEFINE",
                    Format = "LABEL_DEFINE ... END_DEFINE",
                    Example = "LABEL_DEFINE NODE '广州东站' COORD 113.332 23.156 END_DEFINE",
                    InsertTemplate = "LABEL_DEFINE\n    NODE '<标签文本>' COORD <经度> <纬度>\nEND_DEFINE"
                },
            
                // ----- 设施与卫生间 -----
                new SyntaxReferenceItem
                {
                    Command = "FACILITY_DEFINE",
                    Format = "FACILITY_DEFINE ... END_DEFINE",
                    Example = "FACILITY_DEFINE TOILET '公园前' POSITION paid_area ACCESSIBLE END_DEFINE",
                    InsertTemplate = "FACILITY_DEFINE\n    TOILET '<站名>' POSITION paid_area ACCESSIBLE\n    ELEVATOR '<站名>' LOCATION '<位置描述>' ACCESSIBLE\n    ESCALATOR '<站名>' QUANTITY <数量> DIRECTION both\n    TACTILE_PAVING '<站名>' STATUS available\nEND_DEFINE"
                },

                // ---------- 票价优惠策略 ----------
                new SyntaxReferenceItem
                {
                    Command = "PROMOTION_DEFINE",
                    Format = "PROMOTION_DEFINE ... END_DEFINE",
                    Example = "PROMOTION_DEFINE POLICY student_card NAME '学生卡' TYPE rate VALUE 0.5 END_POLICY END_DEFINE",
                    InsertTemplate = "PROMOTION_DEFINE\n    POLICY <优惠ID>\n        NAME '<优惠名称>'\n        DESCRIPTION '<描述>'\n        TYPE rate\n        VALUE <数值>\n        APPLICABLE_LINES [<线路ID>, ...]\n        TIME_RANGE <开始> <结束>\n        DAYS WORKDAY\n    END_POLICY\nEND_DEFINE"
                }
            ]);

        #endregion

        #region 常量与静态资源
        private const string DefaultStatus = "就绪";
        private const string DefaultCity = "北京";
        private const double DebounceIntervalMs = 1000;
        private const double SizeChangedDebounceMs = 500;
        private const double FontSizeMin = 8;
        private const double FontSizeMax = 30;
        private const string RegistryKeyPath = @"Software\MetroEditor";
        private const string RecentFilesValueName = "RecentFiles";
        private const int MaxRecentFiles = 10;
        private List<string> _recentFiles = new List<string>();
        #endregion

        #region 字段
        private string _currentFilePath = string.Empty;
        private bool _isDirty;
        private DispatcherTimer _debounceTimer;
        private readonly object _compileLock = new();
        private bool _isCompiling;
        private List<DirectionViewModel> _lastDirList = new();
        private List<StationViewModel> _lastStationList = new();
        private List<LineViewModel> _lastLineList = new();
        private ParsedData? _currentParsedData;
        private readonly List<StationViewModel> _allStations = new();
        private JsonRoot _currentRoot;

        private ICollectionView? _stationView;
        private ICollectionView? _directionView;
        private ICollectionView? _lineView;

        private Dictionary<string, string> _lineNameMap;
        private string _startupFile;
        private FoldingManager _foldingManager;

        public ObservableCollection<TransferDisplayItem> TransferViewModels { get; set; } = new ObservableCollection<TransferDisplayItem>();
        public ObservableCollection<DwellTimeDisplayItem> DwellTimeViewModels { get; set; } = new ObservableCollection<DwellTimeDisplayItem>();
        public ObservableCollection<FareDisplayItem> FareViewModels { get; set; } = new ObservableCollection<FareDisplayItem>();
        public ObservableCollection<SpecialFareDisplayItem> SpecialFareViewModels { get; set; } = new ObservableCollection<SpecialFareDisplayItem>();
        #endregion

        #region 嵌套视图模型类
        public class DirectionViewModel
        {
            public string DirId { get; set; } = string.Empty;
            public string DisplayLine { get; set; } = string.Empty;
            public string Start { get; set; } = string.Empty;
            public string End { get; set; } = string.Empty;
            public string Attrs { get; set; } = string.Empty;
        }

        public class StationViewModel
        {
            public string Name { get; set; } = string.Empty;
            public double Lon { get; set; }
            public double Lat { get; set; }
            public string Lines { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
        }

        public class LineViewModel
        {
            public string LineId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Color { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public string Formation { get; set; } = string.Empty;
            public double? Maxspeed { get; set; }
            public int OpenYear { get; set; }
            public double? PowerKw { get; set; }
            public double? Brake { get; set; }
            public bool Opened { get; set; } = true;
        }

        public class CompileMessage
        {
            public string Text { get; set; } = string.Empty;
            public Brush ColorBrush { get; set; } = Brushes.Black;
        }


        public class TransferDisplayItem
        {
            public string Station { get; set; }
            public string DisplayLine1 { get; set; }
            public string DisplayLine2 { get; set; }
            public string StatusDisplay { get; set; }
            public double TimeMin { get; set; }
        }

        public class DwellTimeDisplayItem
        {
            public string Station { get; set; }
            public string DisplayLine { get; set; }
            public double TimeMin { get; set; }
        }

        public class FareDisplayItem
        {
            public string Type { get; set; }
            public double Start { get; set; }
            public double End { get; set; }
            public double Price { get; set; }
            public string Note { get; set; }
        }

        public class SpecialFareDisplayItem
        {
            public string DisplayLine { get; set; }                // 单条线路名称
            public string UseLines { get; set; }                   // 线路组（逗号分隔）
            public string TypeDisplay { get; set; }                // 类型（统一票价/完整票价/区间附加费）
            public double Price { get; set; }                      // 票价（用于统一票价或区间附加费）
            public string StartStation { get; set; }               // 区间起点
            public string EndStation { get; set; }                 // 区间终点
            public double? DefaultFare { get; set; }               // 完整票价起步价
            public string SegmentsSummary { get; set; }            // 分段摘要（如 "0-6:3, 6-12:4"）
            public double? LinearExtensionRate { get; set; }       // 线性延伸费率
        }

        public class SyntaxReferenceItem
        {
            public string Command { get; set; }
            public string Format { get; set; }
            public string Example { get; set; }
            public string InsertTemplate { get; set; } // 插入到编辑器的模板文本
        }
        #endregion
    }
}