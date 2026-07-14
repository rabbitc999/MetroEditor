using ICSharpCode.AvalonEdit.Highlighting;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace MetroEditor.Controls.Style
{
    /// <summary>
    /// Metro 语言语法高亮定义
    /// 为 AvalonEdit 编辑器提供智能语法着色功能
    /// 采用两级关键字分类体系：主关键字（核心语法）和辅助关键字（通过上下文隐式高亮）
    /// </summary>
    public class MetroHighlighting : IHighlightingDefinition
    {
        /// <summary>
        /// 高亮方案名称，AvalonEdit 通过该名称识别此高亮定义
        /// </summary>
        public string Name => "Metro";

        /// <summary>
        /// 高亮属性字典（预留扩展接口，当前未使用）
        /// </summary>
        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        /// <summary>
        /// 主规则集：包含所有的高亮规则和跨度规则
        /// </summary>
        public HighlightingRuleSet MainRuleSet { get; }

        // ===== 语法元素颜色定义 =====
        // 采用交通行业相关的色彩语义，增强代码可读性

        /// <summary>主关键字颜色：深蓝色，用于核心语法结构</summary>
        private static readonly Color PrimaryKeywordColor = Color.FromRgb(0, 0, 0xCD);

        /// <summary>辅助关键字颜色：亮橙色，用于次要语法元素</summary>
        private static readonly Color SecondaryKeywordColor = Color.FromRgb(255, 140, 0);

        /// <summary>字符串颜色：深红色，用于文本字面量</summary>
        private static readonly Color StringColor = Colors.DarkRed;

        /// <summary>注释颜色：绿色，用于说明性文本</summary>
        private static readonly Color CommentColor = Colors.Green;

        /// <summary>数字颜色：深紫色，用于数值字面量</summary>
        private static readonly Color NumberColor = Color.FromRgb(0x8B, 0x00, 0x8B);

        /// <summary>符号颜色：深灰色，用于标点符号和结构字符</summary>
        private static readonly Color SymbolColor = Colors.DarkGray;

        /// <summary>
        /// 构造函数：初始化高亮规则集
        /// 按优先级顺序添加各类语法元素的识别规则
        /// </summary>
        public MetroHighlighting()
        {
            MainRuleSet = new HighlightingRuleSet();

            // 规则添加顺序决定了匹配优先级（先添加的优先级更高）
            AddPrimaryKeywordRules();    // 主关键字（最高优先级）
            AddStringRule();             // 字符串字面量
            AddCommentRule();            // 注释（包含单行和多行）
            AddNumberRule();             // 数字字面量
            AddSymbolRule();             // 标点符号
            AddCommitRule();             // 提交相关规则（如 // 注释）
        }

        /// <summary>
        /// 添加提交规则：处理注释的跨度匹配
        /// 使用 HighlightingSpan 实现跨行匹配，区别于普通规则的单行匹配
        /// </summary>
        private void AddCommitRule()
        {
            // 单行注释：从 // 开始到行尾
            var singleLineCommentSpan = new HighlightingSpan
            {
                StartExpression = new Regex(@"//"),
                EndExpression = new Regex(@"$"),
                SpanColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(CommentColor) }
            };
            MainRuleSet.Spans.Add(singleLineCommentSpan);

            // 多行注释：从 /* 开始到 */ 结束
            var multiLineCommentSpan = new HighlightingSpan
            {
                StartExpression = new Regex(@"/\*"),
                EndExpression = new Regex(@"\*/"),
                SpanColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(CommentColor) }
            };
            MainRuleSet.Spans.Add(multiLineCommentSpan);
        }

        /// <summary>
        /// 添加主关键字高亮规则
        /// 主关键字包含 MetroLang 的核心语法元素，采用粗体显示以增强视觉层次
        /// </summary>
        private void AddPrimaryKeywordRules()
        {
            // ===== 主关键字全集 =====
            // 分类说明：
            // 1. 程序结构：PROGRAM_DEFINE, END_DEFINE 等
            // 2. 实体定义：LINE_DEFINE, STATION_DEFINE, DIRECTION_DEFINE 等
            // 3. 运营参数：HEADWAY_PROFILE, CROWD_PROFILE, OPERATION_DEFINE 等
            // 4. 票价规则：PRICE_DEFINE, SPECIAL_DEFINE 等
            // 5. 设施服务：FACILITY_DEFINE, PROMOTION_DEFINE 等
            // 6. 控制语句：IF, DEF, END_IF 等
            // 7. 属性关键字：STATIONS, COLOR, NAME, COORD 等
            // 8. 状态标识：OPERATING, PENDING, FUTURE 等
            // 9. 时间相关：TIME_RANGE, PERIOD, WEEKDAY 等
            string[] primaryKeywords = [
                // 程序元数据
                "KEY_CITY", "PROGRAM_DEFINE", "OTHER_CITY", "VERSION", "UPDATE_DATE", "CITY",
                "AUTHOR", "CREATE_DATE",
                
                // 核心定义块
                "LINE_DEFINE", "DIRECTION_DEFINE", "STATION_DEFINE", "PRICE_DEFINE",
                "TRANSFER_DEFINE", "VIRTUAL_TRANSFER_DEFINE", "FACILITY_DEFINE", "PROMOTION_DEFINE",
                "OPERATION_DEFINE", "HEADWAY_PROFILE", "CROWD_PROFILE", "DWELL_TIME_DEFINE",
                "SPEED_LIMIT_DEFINE", "LABEL_DEFINE", "SPECIAL_DEFINE",
                
                // 结束标记
                "END_DEFINE", "END_SPECIAL", "END_TRANSFER", "END_POLICY", "END_IF",
                
                // 控制结构
                "IF", "DEF",
                
                // 线路属性
                "NAME", "COLOR", "FORMATION", "MAXSPEED", "OPEN_YEAR",
                "POWER", "BRAKE", "DISABLE", "SPECIAL", "LINK",
                
                // 方向属性
                "STATIONS", "NICK_NAME", "NICK_COLOR", "REVERSE",
                "LOOP", "BRANCH", "FAST", "INNER", "OUTER", "FUTURE",
                "SPEED_LIMIT", "CROWD", "HEADWAY",
                
                // 运营参数
                "DEFAULT_TIME", "DWELL_TIME", "OPEN_TIME", "ENTRY_TIME",
                "TRANSFER_TIME", "OUT_STATION_TRANSFER_TIME", "AVG_BOARD_TIME",
                
                // 状态与时间
                "TIME_RANGE", "VALUE", "SECURITY_CHECK", "AUTO", "NIL",
                "ALL", "NORMAL", "OPERATING", "PENDING", "OUT_STATION", "CLOSED",
                "STATUS", "MODE", "ENTRY_ONLY", "EXIT_ONLY", "BOTH",
                
                // 票价相关
                "CURRENCY", "DEFAULT_FARE", "SEGMENT", "LINEAR_EXTENSION",
                "THRESHOLD", "BASE_FARE", "RATE_PER_KM", "TRANSFER_DISCOUNT",
                "ENABLED", "DISCOUNT_AMOUNT", "TIME_LIMIT", "FARE_CAP",
                "SPECIAL_LINE", "SURCHARGE", "TRANSFER_RESTRICT",
                
                // 车站设施
                "TOILET", "ACCESSIBLE_TOILET", "ELEVATOR", "WIDE_GATE",
                "ESCALATOR", "TACTILE_PAVING", "POSITION", "QUANTITY",
                "LOCATION", "GENDER",
                
                // 优惠策略
                "POLICY", "DESCRIPTION", "APPLICABLE_LINES", "TYPE",
                
                // 换乘相关
                "TRANSFER", "TRANSFER_TYPE", "VIA", "VIA_DIRECTIONS",
                
                // 时间单位
                "PERIOD", "TIME", "TIME_MINUTES",
                
                // 星期标识
                "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY",
                "SATURDAY", "SUNDAY", "WEEKEND", "WEEKDAY", "WORKDAY", "GLOBAL",
                
                // 坐标与节点
                "COORD", "NODE", "LABEL_DEFINE",
                
                // 特殊命令
                "REV", "DEPOT", "STATION_STATUS", "STATION_MODE"
            ];

            // 构建正则表达式：\b 确保单词边界，避免部分匹配
            string pattern = @"\b(" + string.Join("|", primaryKeywords) + @")\b";
            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            // 创建高亮规则并添加到主规则集
            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = regex,
                Color = new HighlightingColor
                {
                    Foreground = new SimpleHighlightingBrush(PrimaryKeywordColor),
                    FontWeight = FontWeights.Bold  // 主关键字使用粗体，增强视觉权重
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