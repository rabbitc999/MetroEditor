using ICSharpCode.AvalonEdit.Highlighting;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace MetroEditor.Windows.Controls.Style
{
    /// <summary>
    /// Metro 语言语法高亮定义
    /// 为 AvalonEdit 编辑器提供智能语法着色功能
    /// 采用两级关键字分类体系：主关键字（核心语法）和辅助关键字（通过上下文隐式高亮）
    /// </summary>

    public class MetroHighlighting : IHighlightingDefinition
    {
        public string Name => "Metro";
        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();
        public HighlightingRuleSet MainRuleSet { get; }

        private static readonly Color PrimaryKeywordColor = Color.FromRgb(0, 0, 0xCD);
        private static readonly Color SecondaryKeywordColor = Color.FromRgb(255, 140, 0);
        private static readonly Color StringColor = Colors.DarkRed;
        private static readonly Color CommentColor = Colors.Green;
        private static readonly Color NumberColor = Color.FromRgb(0x8B, 0x00, 0x8B);
        private static readonly Color SymbolColor = Colors.DarkGray;

        public MetroHighlighting()
        {
            MainRuleSet = new HighlightingRuleSet();
            AddPrimaryKeywordRules();
            AddStringRule();
            AddCommentRule();
            AddNumberRule();
            AddSymbolRule();
            AddPreprocessorRule(); // 新增：预处理符号高亮
        }

        private void AddPrimaryKeywordRules()
        {
            string[] primaryKeywords = [
                // 程序元数据
                "KEY_CITY", "PROGRAM_DEFINE", "OTHER_CITY", "VERSION", "UPDATE_DATE", "CITY",
                "AUTHOR", "CREATE_DATE", "OTHER_NAME", "OTHER_NAME",
                
                // 核心定义块
                "LINE_DEFINE", "DIRECTION_DEFINE", "STATION_DEFINE", "PRICE_DEFINE",
                "TRANSFER_DEFINE", "VIRTUAL_TRANSFER_DEFINE", "FACILITY_DEFINE", "PROMOTION_DEFINE",
                "OPERATION_DEFINE", "HEADWAY_PROFILE", "CROWD_PROFILE", "DWELL_TIME_DEFINE",
                "SPEED_LIMIT_DEFINE", "LABEL_DEFINE", "SPECIAL_DEFINE",
                
                // 新增模板与修改
                "LINE_TEMPLATE", "DIRECTION_TEMPLATE", "LINE_MODIFY", "DIRECTION_MODIFY",
                "POLICY",
                
                // 结束标记
                "END_DEFINE", "END_TEMPLATE", "END_POLICY", "END_IF",
                
                // 控制结构
                "IF", "DEF", "INHERITS",
                
                // 线路属性
                "NAME", "COLOR", "FORMATION", "MAXSPEED", "OPEN_YEAR",
                "POWER", "BRAKE", "DISABLE", "SPECIAL", "LINK", "DISTANCE", "IMAGE",
                
                // 方向属性
                "STATIONS", "NICK_NAME", "NICK_COLOR", "REVERSE",
                "LOOP", "BRANCH", "FAST", "INNER", "OUTER", "FUTURE",
                "SPEED_LIMIT", "CROWD", "HEADWAY", "MERGE",
                
                // 运营参数
                "DWELL_TIME", "OPEN_TIME", "ENTRY_TIME",
                "TRANSFER_TIME", "OUT_STATION_TRANSFER_TIME", "AVG_BOARD_TIME",
                
                // 状态与时间
                "TIME_RANGE", "VALUE", "SECURITY_CHECK", "AUTO", "NIL",
                "OPERATING", "PENDING", "OUT_STATION", "CLOSED",
                "STATUS", "MODE", "ENTRY_ONLY", "EXIT_ONLY", "BOTH",
                
                // 票价
                "CURRENCY", "DEFAULT_FARE", "SEGMENT", "LINEAR_EXTENSION",
                "THRESHOLD", "BASE_FARE", "RATE_PER_KM", "TRANSFER_DISCOUNT",
                "ENABLED", "DISCOUNT_AMOUNT", "TIME_LIMIT", "FARE_CAP",
                "SPECIAL_LINE", "SURCHARGE",
                
                // 设施
                "TOILET", "ACCESSIBLE_TOILET", "ELEVATOR", "WIDE_GATE",
                "ESCALATOR", "TACTILE_PAVING", "POSITION", "QUANTITY",
                "LOCATION", "GENDER",
                
                // 优惠
                "POLICY", "DESCRIPTION", "APPLICABLE_LINES", "TYPE",
                
                // 换乘
                "TRANSFER", "TRANSFER_TYPE", "VIA", "VIA_DIRECTIONS",
                
                // 时间
                "PERIOD", "TIME", "TIME_MINUTES",
                
                // 星期
                "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY",
                "SATURDAY", "SUNDAY", "WEEKEND", "WEEKDAY", "WORKDAY", "GLOBAL",
                
                // 坐标
                "COORD", "NODE", "LABEL_DEFINE",
                
                // 特殊命令
                "REV", "DEPOT", "STATION_STATUS", "STATION_MODE", "AS",
                "IMPORT", "INCLUDE", "DEFINE", "UNDEFINE", "IF", "END_IF", "ELSE",
                "PLATFORM_COORD", "GEN_TRANSFER", "DEFINE", "UNDEFINE",
                
                // 线路/方向操作（新增）
                "LINE_MERGE", "LINE_SPLIT", "LINE_COPY", "LINE_RENAME", "LINE_DELETE",
                "DIRECTION_COPY", "DIRECTION_RENAME", "DIRECTION_DELETE",
                "DIRECTION_SPLIT", "DIRECTION_REVERSE", "DIRECTION_MODIFY",
                "ASSERT", "ECHO", "MESSAGE",
                "INTO", "AT", "ADD", "REMOVE", "SET", "KEEP_ORIGINAL"
            ];

            string pattern = @"\b(" + string.Join("|", primaryKeywords) + @")\b";
            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = regex,
                Color = new HighlightingColor
                {
                    Foreground = new SimpleHighlightingBrush(PrimaryKeywordColor),
                    FontWeight = FontWeights.Bold
                }
            });
        }

        // 新增：预处理符号 % 高亮（单独作为独立符号）
        private void AddPreprocessorRule()
        {
            var regex = new Regex(@"%", RegexOptions.Compiled);
            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = regex,
                Color = new HighlightingColor
                {
                    Foreground = new SimpleHighlightingBrush(Color.FromRgb(128, 128, 128)), // 灰色
                    FontWeight = FontWeights.Bold
                }
            });
        }

        /// <summary>
        /// 添加字符串字面量高亮规则
        /// 匹配双引号或单引号包围的文本
        /// </summary>
        private void AddStringRule()
        {
            // 匹配双引号字符串或单引号字符串
            var regex = new Regex(@"""[^""]*""|'[^']*'", RegexOptions.Compiled);
            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = regex,
                Color = new HighlightingColor
                {
                    Foreground = new SimpleHighlightingBrush(StringColor)
                }
            });
        }

        /// <summary>
        /// 添加单行注释高亮规则
        /// 匹配 # 开头到行尾的文本
        /// </summary>
        private void AddCommentRule()
        {
            // 匹配 # 开头到行尾的注释（多行模式）
            var regex = new Regex(@"#.*$", RegexOptions.Compiled | RegexOptions.Multiline);
            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = regex,
                Color = new HighlightingColor
                {
                    Foreground = new SimpleHighlightingBrush(CommentColor)
                }
            });
        }

        /// <summary>
        /// 添加数字字面量高亮规则
        /// 匹配整数和浮点数
        /// </summary>
        private void AddNumberRule()
        {
            // 匹配整数或小数（如 123, 45.67）
            var regex = new Regex(@"\b\d+\.?\d*\b", RegexOptions.Compiled);
            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = regex,
                Color = new HighlightingColor
                {
                    Foreground = new SimpleHighlightingBrush(NumberColor)
                }
            });
        }

        /// <summary>
        /// 添加符号高亮规则
        /// 匹配标点符号和结构字符
        /// </summary>
        private void AddSymbolRule()
        {
            // 匹配常用标点符号：[ ] ( ) , =
            var regex = new Regex(@"[\[\](),=]", RegexOptions.Compiled);
            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = regex,
                Color = new HighlightingColor
                {
                    Foreground = new SimpleHighlightingBrush(SymbolColor)
                }
            });
        }

        // ===== IHighlightingDefinition 接口实现 =====
        // 以下方法主要用于支持 AvalonEdit 的高级功能，当前实现返回空值

        /// <summary>
        /// 根据名称获取预定义颜色（当前未使用）
        /// </summary>
        public HighlightingColor GetNamedColor(string name) => null;

        /// <summary>
        /// 根据名称获取规则集（当前未使用）
        /// </summary>
        public HighlightingRuleSet GetNamedRuleSet(string name) => null;

        /// <summary>
        /// 获取默认文本颜色（黑色）
        /// </summary>
        public HighlightingColor GetDefaultTextColor()
        {
            return new HighlightingColor
            {
                Foreground = new SimpleHighlightingBrush(Colors.Black)
            };
        }

        /// <summary>
        /// 获取所有命名高亮颜色的集合（当前为空）
        /// </summary>
        public IEnumerable<HighlightingColor> NamedHighlightingColors => Array.Empty<HighlightingColor>();
    }
}