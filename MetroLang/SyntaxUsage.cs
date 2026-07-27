using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang
{
    public static class SyntaxUsage
    {
        public static List<SyntaxReferenceItem> SyntaxReferences = new List<SyntaxReferenceItem>
        {
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
            new SyntaxReferenceItem
            {
                Command = "STATION_COPY",
                Format = "STATION_COPY <旧站名> <新站名> [支持通配符]",
                Example = "STATION_COPY '公园前' '公园前_OLD'",
                InsertTemplate = "STATION_COPY '<旧站名>' '<新站名>'"
            },
            new SyntaxReferenceItem
            {
                Command = "STATION_RENAME",
                Format = "STATION_RENAME <旧站名> <新站名> [支持通配符]",
                Example = "STATION_RENAME '公园前' '公园前_副本'",
                InsertTemplate = "STATION_RENAME '<旧站名>' '<新站名>'"
            },
            new SyntaxReferenceItem
            {
                Command = "STATION_DELETE",
                Format = "STATION_DELETE <站名> [站名...] [支持通配符]",
                Example = "STATION_DELETE '公园前_OLD'",
                InsertTemplate = "STATION_DELETE '<站名>'"
            },
            new SyntaxReferenceItem
            {
                Command = "STATION_MODIFY",
                Format = "STATION_MODIFY '<站名>' ... END_DEFINE",
                Example = "STATION_MODIFY '公园前' SET COORD 113.3, 23.1 END_DEFINE",
                InsertTemplate = "STATION_MODIFY '<站名>'\n    SET COORD <经度>, <纬度>\n    SET CITY '<城市>'\n    SET OTHER_NAMES ['别名1', '别名2']\nEND_DEFINE"
            },

            // ---------- 线路模板 ----------
            new SyntaxReferenceItem
            {
                Command = "LINE_TEMPLATE",
                Format = "LINE_TEMPLATE <模板ID> [INHERITS 父模板] ... END_TEMPLATE",
                Example = "LINE_TEMPLATE URBAN FORMATION '6B' MAXSPEED 80 END_TEMPLATE",
                InsertTemplate = "LINE_TEMPLATE <模板ID>\n    FORMATION '<编组>'\n    MAXSPEED <速度>\n    CITY '<城市>'\nEND_TEMPLATE"
            },
            new SyntaxReferenceItem
            {
                Command = "DIRECTION_TEMPLATE",
                Format = "DIRECTION_TEMPLATE <模板ID> [INHERITS 父模板] ... END_TEMPLATE",
                Example = "DIRECTION_TEMPLATE MAIN NICK_NAME '主线' BRANCH END_TEMPLATE",
                InsertTemplate = "DIRECTION_TEMPLATE <模板ID>\n    NICK_NAME '<别名>'\n    BRANCH\n    LOOP INNER\nEND_TEMPLATE"
            },

            // ---------- 线路 ----------
            new SyntaxReferenceItem
            {
                Command = "LINE_DEFINE",
                Format = "LINE_DEFINE <线路ID> [INHERITS 模板] ... END_DEFINE",
                Example = "LINE_DEFINE GZ1 NAME '1号线' COLOR '#F47920' END_DEFINE",
                InsertTemplate = "LINE_DEFINE <线路ID> INHERITS <模板ID>\n    NAME '<名称>'\n    COLOR '<颜色>'\nEND_DEFINE"
            },
            new SyntaxReferenceItem
            {
                Command = "LINE_COPY",
                Format = "LINE_COPY <源模式> <目标模式> [支持通配符]",
                Example = "LINE_COPY GZ1 GZ1_BACKUP",
                InsertTemplate = "LINE_COPY <源线路> <目标线路>"
            },
            new SyntaxReferenceItem
            {
                Command = "LINE_RENAME",
                Format = "LINE_RENAME <源模式> <目标模式> [支持通配符]",
                Example = "LINE_RENAME GZ1_BACKUP GZ1_OLD",
                InsertTemplate = "LINE_RENAME <旧ID> <新ID>"
            },
            new SyntaxReferenceItem
            {
                Command = "LINE_DELETE",
                Format = "LINE_DELETE <线路ID> [线路ID...] [支持通配符]",
                Example = "LINE_DELETE GZ1_OLD",
                InsertTemplate = "LINE_DELETE <线路ID>"
            },
            new SyntaxReferenceItem
            {
                Command = "LINE_MERGE",
                Format = "LINE_MERGE <线路1> <线路2> ... INTO <新线路> [KEEP_ORIGINAL]",
                Example = "LINE_MERGE GZ1 GZ1_BRANCH INTO GZ1_NEW",
                InsertTemplate = "LINE_MERGE <线路1> <线路2> INTO <新线路>\n    KEEP_ORIGINAL"
            },
            new SyntaxReferenceItem
            {
                Command = "LINE_MODIFY",
                Format = "LINE_MODIFY <线路ID> ... END_DEFINE",
                Example = "LINE_MODIFY GZ1 SET NAME '新1号线' END_DEFINE",
                InsertTemplate = "LINE_MODIFY <线路ID>\n    SET NAME '<新名称>'\n    SET COLOR '#新颜色>'\n    ADD SPECIAL\n    REMOVE SPECIAL\nEND_DEFINE"
            },

            // ---------- 方向 ----------
            new SyntaxReferenceItem
            {
                Command = "DIRECTION_DEFINE",
                Format = "DIRECTION_DEFINE <方向ID> [线路ID] [INHERITS 模板] ... END_DEFINE",
                Example = "DIRECTION_DEFINE GZ1_0 GZ1 STATIONS ['广州东站', '体育中心'] END_DEFINE",
                InsertTemplate = "DIRECTION_DEFINE <方向ID> <线路ID>\n    STATIONS ['<站1>', '<站2>']\n    DISTANCE AUTO\n    NICK_NAME '<别名>'\nEND_DEFINE"
            },
            new SyntaxReferenceItem
            {
                Command = "DIRECTION_COPY",
                Format = "DIRECTION_COPY <源模式> <目标模式> [支持通配符]",
                Example = "DIRECTION_COPY GZ1_0 GZ1_0_BACKUP",
                InsertTemplate = "DIRECTION_COPY <源ID> <目标ID>"
            },
            new SyntaxReferenceItem
            {
                Command = "DIRECTION_RENAME",
                Format = "DIRECTION_RENAME <源模式> <目标模式> [支持通配符]",
                Example = "DIRECTION_RENAME GZ1_0_BACKUP GZ1_0_OLD",
                InsertTemplate = "DIRECTION_RENAME <旧ID> <新ID>"
            },
            new SyntaxReferenceItem
            {
                Command = "DIRECTION_DELETE",
                Format = "DIRECTION_DELETE <方向ID> [方向ID...] [支持通配符]",
                Example = "DIRECTION_DELETE GZ1_0_OLD",
                InsertTemplate = "DIRECTION_DELETE <方向ID>"
            },
            new SyntaxReferenceItem
            {
                Command = "DIRECTION_SPLIT",
                Format = "DIRECTION_SPLIT <方向ID> AT '<站名>' INTO <新方向1> <新方向2>",
                Example = "DIRECTION_SPLIT GZ1_0 AT '体育中心' INTO GZ1_MAIN GZ1_BRANCH",
                InsertTemplate = "DIRECTION_SPLIT <方向ID> AT '<站名>' INTO <新方向1> <新方向2>"
            },
            new SyntaxReferenceItem
            {
                Command = "DIRECTION_REVERSE",
                Format = "DIRECTION_REVERSE <方向ID> [新方向ID]",
                Example = "DIRECTION_REVERSE GZ1_0 GZ1_0_REV",
                InsertTemplate = "DIRECTION_REVERSE <方向ID> <新方向ID>"
            },
            new SyntaxReferenceItem
            {
                Command = "DIRECTION_MODIFY",
                Format = "DIRECTION_MODIFY <方向ID> ... END_DEFINE",
                Example = "DIRECTION_MODIFY GZ1_0 ADD FAST SET NICK_NAME '快车' END_DEFINE",
                InsertTemplate = "DIRECTION_MODIFY <方向ID>\n    ADD BRANCH\n    REMOVE FAST\n    SET NICK_NAME '<别名>'\nEND_DEFINE"
            },

            // ---------- 换乘 ----------
            new SyntaxReferenceItem
            {
                Command = "TRANSFER_TYPE",
                Format = "TRANSFER_TYPE <类型ID> '<类型名称>' ... END_DEFINE",
                Example = "TRANSFER_TYPE TTHC '同台换乘' TIME 120 STATUS OPERATING END_DEFINE",
                InsertTemplate = "TRANSFER_TYPE <类型ID> '<类型名称>'\n    TIME <秒数>\n    STATUS operating\n    SECURITY_CHECK\nEND_DEFINE"
            },
            new SyntaxReferenceItem
            {
                Command = "TRANSFER",
                Format = "TRANSFER <类型ID> '<站名>' <线路1> [<线路2>]",
                Example = "TRANSFER TTHC '体育中心' GZ1 GZ2",
                InsertTemplate = "TRANSFER <类型ID> '<站名>' <线路1> <线路2>"
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
                Example = "OPERATION_DEFINE TRANSFER_TIME 180 DWELL_TIME 30 END_DEFINE",
                InsertTemplate = "OPERATION_DEFINE\n    TRANSFER_TIME <秒>\n    DWELL_TIME <秒>\n    OUT_STATION_TRANSFER_TIME <秒>\n    ENTRY_TIME <秒>\n    AVG_BOARD_TIME <秒>\nEND_DEFINE"
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
                Format = "DWELL_TIME '<站名>' <线路ID> <秒>",
                Example = "DWELL_TIME '广州东站' GZ1 45",
                InsertTemplate = "DWELL_TIME '<站名>' <线路ID> <秒>"
            },
            new SyntaxReferenceItem
            {
                Command = "DWELL_TIME_DEFINE",
                Format = "DWELL_TIME_DEFINE ... END_DEFINE",
                Example = "DWELL_TIME_DEFINE NODE '体育中心' GZ1 30 END_DEFINE",
                InsertTemplate = "DWELL_TIME_DEFINE\n    NODE '<站名>' <线路ID> <秒>\n    LINE <线路ID> <秒>\n    DIRECTION <方向ID> <秒>\nEND_DEFINE"
            },
            new SyntaxReferenceItem
            {
                Command = "SPEED_LIMIT_DEFINE",
                Format = "SPEED_LIMIT_DEFINE <方向ID> ... END_DEFINE",
                Example = "SPEED_LIMIT_DEFINE GZ1_0 NODE '广州东站' '体育中心' 70 END_DEFINE",
                InsertTemplate = "SPEED_LIMIT_DEFINE <方向ID>\n    NODE '<站A>' '<站B>' <速度>\nEND_DEFINE"
            },

            // ---------- 设施 ----------
            new SyntaxReferenceItem
            {
                Command = "FACILITY_DEFINE",
                Format = "FACILITY_DEFINE ... END_DEFINE",
                Example = "FACILITY_DEFINE TOILET '公园前' POSITION paid_area ACCESSIBLE END_DEFINE",
                InsertTemplate = "FACILITY_DEFINE\n    TOILET '<站名>' POSITION paid_area ACCESSIBLE\n    ELEVATOR '<站名>' LOCATION '<位置描述>' ACCESSIBLE\n    ESCALATOR '<站名>' QUANTITY <数量> DIRECTION both\n    TACTILE_PAVING '<站名>' STATUS available\nEND_DEFINE"
            },

            // ---------- 优惠策略 ----------
            new SyntaxReferenceItem
            {
                Command = "PROMOTION_DEFINE",
                Format = "PROMOTION_DEFINE ... END_DEFINE",
                Example = "PROMOTION_DEFINE POLICY student_card NAME '学生卡' TYPE rate VALUE 0.5 END_POLICY END_DEFINE",
                InsertTemplate = "PROMOTION_DEFINE\n    POLICY <优惠ID>\n        NAME '<优惠名称>'\n        DESCRIPTION '<描述>'\n        TYPE rate\n        VALUE <数值>\n        APPLICABLE_LINES [<线路ID>, ...]\n        TIME_RANGE <开始> <结束>\n        DAYS WORKDAY\n    END_POLICY\nEND_DEFINE"
            },

            // ---------- 断言与输出 ----------
            new SyntaxReferenceItem
            {
                Command = "ASSERT",
                Format = "ASSERT <表达式> [MESSAGE '错误消息']",
                Example = "ASSERT DIRECTION_EXISTS('GZ1_0') MESSAGE '方向未定义'",
                InsertTemplate = "ASSERT DIRECTION_EXISTS('<方向ID>') MESSAGE '<错误信息>'"
            },
            new SyntaxReferenceItem
            {
                Command = "ECHO",
                Format = "ECHO '消息' + 变量 + '消息'",
                Example = "ECHO '站点总数：' + STATION_COUNT",
                InsertTemplate = "ECHO '编译完成，版本：' + VERSION"
            },

            // ---------- 其他 ----------
            new SyntaxReferenceItem
            {
                Command = "DEPOT",
                Format = "DEPOT <线路ID> <站名> [方向ID] [HH:mm]",
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
            new SyntaxReferenceItem
            {
                Command = "LABEL_DEFINE",
                Format = "LABEL_DEFINE ... END_DEFINE",
                Example = "LABEL_DEFINE NODE '广州东站' COORD 113.332 23.156 END_DEFINE",
                InsertTemplate = "LABEL_DEFINE\n    NODE '<标签文本>' COORD <经度> <纬度>\nEND_DEFINE"
            },

            // ---------- 条件编译（预处理） ----------
            new SyntaxReferenceItem
            {
                Command = "%define",
                Format = "%define <符号名>",
                Example = "%define CITY_BJ",
                InsertTemplate = "%define <符号名>"
            },
            new SyntaxReferenceItem
            {
                Command = "%undefine",
                Format = "%undefine <符号名>",
                Example = "%undefine CITY_BJ",
                InsertTemplate = "%undefine <符号名>"
            },
            new SyntaxReferenceItem
            {
                Command = "%if",
                Format = "%if <符号名> ... %else ... %endif",
                Example = "%if CITY_BJ %include 'bj.metro' %else %include 'sh.metro' %endif",
                InsertTemplate = "%if <符号名>\n    <命令>\n%else\n    <命令>\n%endif"
            },
            new SyntaxReferenceItem
            {
                Command = "%include",
                Format = "%include '文件路径'",
                Example = "%include 'base_stations.metro'",
                InsertTemplate = "%include '<文件路径>'"
            },
            new SyntaxReferenceItem
            {
                Command = "%import",
                Format = "%import '文件路径' AS STATIONS",
                Example = "%import 'stations.csv' AS STATIONS",
                InsertTemplate = "%import '<CSV文件路径>' AS STATIONS"
            },
            new SyntaxReferenceItem
            {
                Command = "PLATFORM_COORD",
                Format = "PLATFORM_COORD '<站名>' <线路ID> COORD <经度> <纬度>",
                Example = "PLATFORM_COORD '西直门' LINE2 COORD 116.357, 39.945",
                InsertTemplate = "PLATFORM_COORD '<站名>' <线路ID> COORD <经度>, <纬度>"
            },
            new SyntaxReferenceItem
            {
                Command = "GEN_TRANSFER",
                Format = "GEN_TRANSFER [WALK_SPEED <速度>] [UPDATE_EXISTING]",
                Example = "GEN_TRANSFER WALK_SPEED 5.0 UPDATE_EXISTING",
                InsertTemplate = "GEN_TRANSFER WALK_SPEED 5.0"
            },
        };

        public static List<KeyValuePair<string, string>> KeyWords = new()
        {
            // ----- 全局与城市 -----
            new("PROGRAM_DEFINE", "程序定义块"),
            new("OTHER_CITY", "其他城市列表"),
            new("VERSION", "版本号"),
            new("UPDATE_DATE", "更新日期"),
            new("CREATE_DATE", "创建日期"),
            new("AUTHOR", "作者"),

            // ----- 站点与状态 -----
            new("STATION_STATUS", "车站状态"),
            new("STATION_DEFINE", "站点定义块"),
            new("STATION_MODE", "站点进出模式"),
            new("STATION_COPY", "复制车站"),
            new("STATION_RENAME", "重命名车站"),
            new("STATION_DELETE", "删除车站"),
            new("STATION_MODIFY", "修改车站"),

            // ----- 线路定义 -----
            new("LINE_DEFINE", "线路定义块"),
            new("LINE_TEMPLATE", "线路模板"),
            new("DIRECTION_TEMPLATE", "方向模板"),
            new("END_TEMPLATE", "模板结束"),

            // ----- 线路操作 -----
            new("LINE_COPY", "复制线路"),
            new("LINE_RENAME", "重命名线路"),
            new("LINE_DELETE", "删除线路"),
            new("LINE_MERGE", "合并线路"),
            new("LINE_SPLIT", "拆分线路"),
            new("LINE_MODIFY", "修改线路"),

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
            new("REV", "反向"),
            new("MERGE", "方向合并"),
            new("VIA", "经由/共线"),

            // ----- 方向操作 -----
            new("DIRECTION_COPY", "复制方向"),
            new("DIRECTION_RENAME", "重命名方向"),
            new("DIRECTION_DELETE", "删除方向"),
            new("DIRECTION_SPLIT", "拆分方向"),
            new("DIRECTION_REVERSE", "反转方向"),
            new("DIRECTION_MODIFY", "修改方向"),

            // ----- 换乘 -----
            new("TRANSFER_TYPE", "换乘类型定义"),
            new("TRANSFER", "换乘"),
            new("TRANSFER_DEFINE", "换乘定义块"),
            new("VIRTUAL_TRANSFER_DEFINE", "虚拟换乘定义块"),
            new("VIA_DIRECTIONS", "经由方向列表"),
            new("SECURITY_CHECK", "需重新安检"),

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
            new("AVG_BOARD_TIME", "平均上车时间"),

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

            // ----- 断言与输出 -----
            new("ASSERT", "编译时断言"),
            new("ECHO", "编译时输出"),
            new("MESSAGE", "断言自定义消息"),

            // ----- 模板与继承 -----
            new("INHERITS", "模板继承"),
            new("END_TEMPLATE", "模板结束"),

            // ----- 修改操作子命令 -----
            new("ADD", "添加属性"),
            new("REMOVE", "移除属性"),
            new("SET", "设置属性值"),
            new("KEEP_ORIGINAL", "保留原始对象"),
            new("INTO", "合并/拆分为"),
            new("AT", "拆分位置"),

            // ----- 其他 -----
            new("DEPOT", "车辆段"),
            new("OPEN_TIME", "运营时间"),
            new("LABEL_DEFINE", "文字标签定义块"),
            new("COORD", "坐标"),
            new("AUTO", "自动计算"),
            new("NIL", "空值"),
            new("ALL", "全部"),
            new("SURCHARGE", "附加费"),
            new("TRANSFER_RESTRICT", "换乘限制"),
            new("STATUS", "状态"),
            new("MODE", "进出模式"),
            new("ENTRY_ONLY", "仅进站"),
            new("EXIT_ONLY", "仅出站"),
            new("BOTH", "可进可出"),
            new("NORMAL", "正常"),
            new("CLOSED", "关闭"),
            new("PENDING", "待开通"),
            new("OUT_STATION", "出站"),
            new("PLATFORM_COORD", "设置线路平台坐标"),
            new("GEN_TRANSFER", "自动生成换乘时间"),

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

            // ----- 线路属性 -----
            new("NAME", "线路名称"),
            new("COLOR", "线路颜色"),
            new("FORMATION", "编组"),
            new("MAXSPEED", "最高速度"),
            new("OPEN_YEAR", "开通年份"),
            new("POWER_KW", "牵引功率"),
            new("BRAKE", "制动减速度"),
            new("DISABLE", "停运"),
            new("SPECIAL", "特殊线路"),
            new("LINK", "线路链接"),
            new("IMAGE", "线路示意图"),

            // ----- 值关键字 -----
            new("TIME_MINUTES", "时间(分钟)"),
            new("TIME", "时间(秒)"),
        };



        public class SyntaxReferenceItem
        {
            public string Command { get; set; }
            public string Format { get; set; }
            public string Example { get; set; }
            public string InsertTemplate { get; set; }
        }
    }
}
