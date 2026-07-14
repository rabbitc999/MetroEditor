# MetroLang 语言参考手册

MetroLang 是一种面向地铁网络描述的领域特定语言（DSL），设计目标为**可读性强、版本控制友好、支持人工编辑与自动化编译**。  
本手册定义其语法、命令及语义，并明确区分**推荐的新块语法**与**保留的传统命令**。所有块均使用 `END_DEFINE` 作为结束标记。

---

## 1. 基本约定

- **关键字不区分大小写**：`PROGRAM_DEFINE` 与 `program_define` 等效。
- **字符串**：使用单引号 `'` 或双引号 `"` 包裹，内部支持转义（如 `\'`）。
- **标识符**：线路ID、方向ID等由字母、数字、下划线组成，不能以数字开头。
- **注释**：`#` 到行尾的内容被忽略。
- **换行与缩进**：命令通常独占一行，列表元素可用逗号或换行分隔，缩进不影响语义。
- **时间值**：支持三种格式，统一转换为**秒**：
  - 纯数字（如 `30`） → 30 秒
  - 数字加冒号（如 `1:`） → 1 分钟（60 秒）
  - 分:秒（如 `1:30`） → 90 秒
- **块结构**：使用 `命令 ... END_DEFINE` 界定，块内子命令顺序自由，支持嵌套块（如 `SPECIAL_DEFINE`）。
- **变量定义**：使用 `DEF` 定义编译时常量，支持条件编译（详见第 15 章）。

---

## 2. 程序定义（`PROGRAM_DEFINE` 块）

```metro
PROGRAM_DEFINE
    KEY_CITY '广州'                     # 必填，主城市
    OTHER_CITY ['佛山', '中山']         # 可选，其他城市（支持单个字符串或列表）
    VERSION 'V1.0'                      # 可选，版本号，默认 V1.0
    UPDATE_DATE '2026-07-11'            # 可选，更新日期，默认当前日期
    AUTHOR '作者名'                     # 可选，作者信息
    CREATE_DATE '2026-01-01'            # 可选，创建日期，默认当前日期
END_DEFINE
```

- `KEY_CITY`：**必填**，定义主城市。
- `OTHER_CITY`：可选，支持单个字符串（如 `'佛山'`）或字符串列表（如 `['佛山', '中山']`）。
- `VERSION`：可选，版本号，默认为 `V1.0`。
- `UPDATE_DATE`：可选，更新日期，默认为编译当天的日期。
- `AUTHOR`：可选，作者信息，默认为 `"匿名"`。
- `CREATE_DATE`：可选，创建日期，默认为编译当天的日期。

---

## 3. 站点定义（`STATION_DEFINE` 块）

```metro
STATION_DEFINE
    NODE '站名' COORD 经度, 纬度 [CITY '城市']
    NODE ...
END_DEFINE
```

- `NODE`：定义单个站点，包含站名、经纬度坐标，可选指定所属城市。
- 如果未指定 `CITY`，编译器会根据途经线路自动推断。

---

## 4. 线路定义（`LINE_DEFINE` 块）

```metro
LINE_DEFINE 线路ID
    NAME '线路名称'                    # 必填
    COLOR '颜色'                       # 必填
    CITY '城市'                        # 可选，默认 KEY_CITY
    FORMATION '编组'                   # 如 '6B', '8A'
    MAXSPEED 80                        # 最高速度 (km/h)
    OPEN_YEAR 2000                     # 开通年份
    POWER 3680                         # 牵引功率 (kW)
    BRAKE 0.85                         # 制动减速度 (m/s²)
    LINK 'URL'                         # 百科链接
    DISABLE                            # 标记为停运
    SPECIAL                            # 标记为特殊线路（如机场线、APM线）
    PENDING ['站名1', '站名2']         # 暂缓开通站点（支持单个或列表）
    DEPOT '站名' [方向ID] [HH:mm]      # 车辆段
    DIRECTION_DEFINE ... END_DEFINE    # 嵌套方向定义
    SPECIAL_DEFINE ... END_DEFINE      # 嵌套特殊票价定义
END_DEFINE
```

- 方向定义可以**嵌套**在 `LINE_DEFINE` 内部，也可以独立定义。
- 嵌套的 `DIRECTION_DEFINE` 自动继承外层线路ID，无需重复指定。
- `PENDING` 支持单个站点（`'站名'`）或列表（`['站1', '站2']`）。

---

## 5. 方向定义（`DIRECTION_DEFINE` 块）

```metro
DIRECTION_DEFINE 方向ID [线路ID]      # 当独立使用时需提供线路ID，嵌套时可省略
    STATIONS ['站1', '站2', ...]       # 必填
    DISTANCE AUTO | [距离列表]         # 可选，AUTO 自动计算
    NICK_NAME '别名'                   # 可选
    NICK_COLOR '颜色'                  # 可选
    LOOP [INNER | OUTER]               # 环线标记
    BRANCH                             # 支线标记
    FAST                               # 快车标记
    FUTURE                             # 规划中（未来开通）
    SPEED_LIMIT '站A' '站B' 速度(km/h)
    CROWD TIME_RANGE HH:mm HH:mm 系数 [星期修饰符]
    HEADWAY TIME_RANGE HH:mm HH:mm VALUE 秒数
    DWELL_TIME '站名' 秒数
    OPEN_TIME HH:mm HH:mm
    VIA 方向ID                         # 共线/跨线经过的方向
    REVERSE 原方向ID                   # 复制并反转另一个方向的所有属性（可继续追加额外子命令）
END_DEFINE
```

**反向方向简写**（立即返回，不处理后续子命令）：
```metro
DIRECTION_DEFINE 方向ID REV 原方向ID
```

- `REV` 为纯快捷方式，自动继承原方向所有属性并反转站点和距离，**不能**再添加额外子命令。
- `REVERSE` 同样复制并反转，但允许在块内继续添加额外子命令（如增加 `NICK_NAME`、`CROWD` 等），支持更灵活的反向定义。

**距离列表说明**：
- 非环线方向：距离数量 = 站点数 - 1，最后一个距离应为 `NIL`。
- 环线方向：距离数量 = 站点数（首尾相连），无 `NIL`。

**星期修饰符**（用于 `CROWD`、`HEADWAY`、`STATION_MODE`、`OPEN_TIME`）：
- 单个星期：`MONDAY`、`TUESDAY`、`WEDNESDAY`、`THURSDAY`、`FRIDAY`、`SATURDAY`、`SUNDAY`
- 列表：`[MONDAY, WEDNESDAY, FRIDAY]`
- 预定义组合：`WORKDAY`（周一至周五）、`WEEKEND`（周六周日）

---

## 6. 票价定义（`PRICE_DEFINE` 块）

```metro
PRICE_DEFINE
    CURRENCY 'CNY'                     # 可选，默认 CNY
    DEFAULT_FARE 2.0                   # 起步价
    SEGMENT 起始 结束 票价              # 可重复多个
    LINEAR_EXTENSION THRESHOLD 阈值 BASE_FARE 基础票价 RATE_PER_KM 费率
    TRANSFER_DISCOUNT ENABLED TRUE DISCOUNT_AMOUNT 金额 TIME_LIMIT 分钟
    FARE_CAP 封顶价                     # 0 表示无封顶
    SPECIAL_LINE 线路ID ALL 票价        # 全线统一票价
    SPECIAL_LINE 线路ID SEGMENT '站A' '站B' 票价   # 区间附加费
END_DEFINE
```

---

## 7. 换乘定义

### 换乘类型定义（`TRANSFER_TYPE` 块）
```metro
TRANSFER_TYPE 类型ID '类型名称'
    TIME 秒数
    STATUS operating | pending | out_station
    SECURITY_CHECK                     # 可选
END_DEFINE
```

### 换乘规则（`TRANSFER` 命令）
```metro
TRANSFER 类型ID '站名' 线路A [线路B]
```
- 若提供两个线路，直接生成换乘规则。
- 若只提供一个线路，生成通配模板（与该站所有其他线路配对）。

### 换乘定义块（`TRANSFER_DEFINE` 块）
```metro
TRANSFER_DEFINE '站名' 线路A 线路B
    TIME 秒数
    STATUS operating | pending | out_station
    SECURITY_CHECK
END_DEFINE
```

### 虚拟换乘定义块（`VIRTUAL_TRANSFER_DEFINE` 块）
```metro
VIRTUAL_TRANSFER_DEFINE '站A' '站B'
    TIME_MINUTES 分钟数
    SECURITY_CHECK
    VIA_DIRECTIONS [方向ID列表]        # 可选
END_DEFINE
```

---

## 8. 运营参数（`OPERATION_DEFINE` 块）

```metro
OPERATION_DEFINE
    TRANSFER_TIME 秒数                  # 站内换乘时间，默认 180
    DWELL_TIME 秒数                     # 默认停站时间，默认 30
    OUT_STATION_TRANSFER_TIME 秒数      # 出站换乘时间，默认 300
    ENTRY_TIME 秒数                     # 进站时间（闸机→站台），默认 120
    AVG_BOARD_TIME 秒数                 # 平均上车时间，默认 3
END_DEFINE
```
> 所有时间值单位为**秒**，支持冒号格式（如 `1:30` 表示 90 秒）。

---

## 9. 时段配置文件（块语法）

### `HEADWAY_PROFILE` —— 按线路定义发车间隔
```metro
HEADWAY_PROFILE 线路ID
    PERIOD HH:mm HH:mm 秒数 [星期修饰符]
    PERIOD ...
END_DEFINE
```

### `CROWD_PROFILE` —— 按方向定义拥挤度系数
```metro
CROWD_PROFILE 方向ID
    PERIOD HH:mm HH:mm 系数 [星期修饰符]
    PERIOD ...
END_DEFINE
```
- 方向ID 必须已定义。
- 系数 ≥ 1.0，表示拥挤程度（如 1.8 表示需等约 1.8 趟车）。

---

## 10. 停站时间定义（`DWELL_TIME_DEFINE` 块）

```metro
DWELL_TIME_DEFINE
    NODE '站名' 线路ID 秒数            # 指定站点在某线路上的停站时间
    LINE 线路ID 秒数                   # 为某线路的所有站点统一设置停站时间
    DIRECTION 方向ID 秒数              # 为某方向上的所有站点统一设置停站时间
END_DEFINE
```
- 三种子命令可根据需要混合使用，优先级为 `NODE` > `DIRECTION` > `LINE`（即最具体的覆盖更广泛的）。

---

## 11. 限速定义（`SPEED_LIMIT_DEFINE` 块）

```metro
SPEED_LIMIT_DEFINE 方向ID
    NODE '站A' '站B' 速度(km/h)
    NODE ...
END_DEFINE
```

---

## 12. 站点进出模式（单行命令）

```metro
STATION_MODE '站名' 模式 [TIME_RANGE HH:mm HH:mm] [星期修饰符]
```
- 模式：`ENTRY_ONLY`（仅进站）、`EXIT_ONLY`（仅出站）、`BOTH`（正常）。

---

## 13. 运营时间（单行命令）

```metro
OPEN_TIME [GLOBAL | 线路ID] HH:mm HH:mm [星期修饰符]
```
- `GLOBAL` 表示全局默认时间。

---

## 14. 车辆段（单行命令）

```metro
DEPOT 线路ID '站名' [方向ID] [HH:mm]
```

---

## 15. 共线/跨线声明（单行命令）

```metro
VIA 方向A 方向B
```
将 `方向B` 添加到 `方向A` 的 `via_directions` 列表中，用于指示共线关系。

---

## 16. 条件编译

### 定义变量
```metro
DEF 变量名 = 值
```
- 值类型：布尔（`TRUE`/`FALSE`）、字符串、数字。

### 条件指令
```metro
IF 条件表达式
    ... 代码块 ...
END_IF
```
- 条件表达式：`变量名`（布尔）、`变量名 == 值`、`变量名 != 值`。

---

## 17. 标签定义（`LABEL_DEFINE` 块）

```metro
LABEL_DEFINE
    NODE '标签文本' COORD 经度, 纬度
    NODE ...
END_DEFINE
```

---

## 18. 无障碍设施（`FACILITY_DEFINE` 命令）

```metro
STATION_STATUS ['站名1', '站名2'] [线路ID] 状态
```
- 为指定站点（单个或列表）在特定线路（可选）上设置状态。
- 状态：`operating`、`pending`、`out_station`。

---

### 概述

`FACILITY_DEFINE` 块用于描述各站点的无障碍设施、卫生间、电梯、自动扶梯、盲道等信息。**这些数据仅用于前端展示和查询，不参与路径规划或票价计算。**

### 语法格式

```metro
FACILITY_DEFINE
    # 普通卫生间
    TOILET '站名' POSITION paid_area [ACCESSIBLE] [GENDER male|female|mixed]

    # 无障碍卫生间（残疾人/母婴）
    ACCESSIBLE_TOILET '站名' POSITION paid_area [ACCESSIBLE] [GENDER male|female|mixed]

    # 无障碍电梯
    ELEVATOR '站名' LOCATION '具体位置描述' [ACCESSIBLE]

    # 宽闸机（轮椅通道）
    WIDE_GATE '站名' QUANTITY 数量

    # 自动扶梯（数量及方向）
    ESCALATOR '站名' QUANTITY 数量 [DIRECTION up|down|both]

    # 盲道铺设情况
    TACTILE_PAVING '站名' STATUS available|partial|none
END_DEFINE
```

### 子命令详解

| 子命令 | 参数 | 说明 |
|--------|------|------|
| `TOILET` | `'站名'`、`POSITION`（`paid_area`/`unpaid_area`）、可选 `ACCESSIBLE`、可选 `GENDER` | 普通卫生间，`ACCESSIBLE` 表示无障碍卫生间（若同时指定，表示该卫生间具备无障碍设施） |
| `ACCESSIBLE_TOILET` | 同上 | 专门的无障碍卫生间（优先推荐给轮椅使用者），可独立于普通卫生间 |
| `ELEVATOR` | `'站名'`、`LOCATION '描述'`、可选 `ACCESSIBLE` | 无障碍电梯，`ACCESSIBLE` 表示该电梯可供轮椅使用（通常都是） |
| `WIDE_GATE` | `'站名'`、`QUANTITY`（整数） | 宽闸机数量 |
| `ESCALATOR` | `'站名'`、`QUANTITY`（整数）、可选 `DIRECTION`（`up`/`down`/`both`） | 自动扶梯，数量及运行方向，默认 `both` |
| `TACTILE_PAVING` | `'站名'`、`STATUS`（`available`/`partial`/`none`） | 盲道铺设情况 |

**参数说明**：
- `POSITION`：卫生间位置，`paid_area`（付费区内）或 `unpaid_area`（付费区外）。
- `ACCESSIBLE`：布尔标志，表示是否具备无障碍设施（如扶手、轮椅空间等）。
- `GENDER`：卫生间性别，默认 `mixed`（不分男女），可设为 `male` 或 `female`。

### 示例

```metro
FACILITY_DEFINE
    TOILET '公园前' POSITION paid_area ACCESSIBLE GENDER mixed
    ACCESSIBLE_TOILET '公园前' POSITION paid_area GENDER mixed
    ELEVATOR '公园前' LOCATION 'A口出口旁' ACCESSIBLE
    WIDE_GATE '公园前' QUANTITY 2
    ESCALATOR '公园前' QUANTITY 4 DIRECTION both
    TACTILE_PAVING '公园前' STATUS available

    TOILET '广州东站' POSITION unpaid_area
    ELEVATOR '广州东站' LOCATION '站厅中部'
END_DEFINE
```

### 1注意事项
- 每个站点的设施可以多次出现（例如多条扶梯、多部电梯），编译器会合并到同一个站点的设施列表中。
- 若某个设施类型（如 `TOILET`）在同一个站点重复定义，后面的会覆盖前面的（建议只定义一次）。
- 站名必须已通过 `STATION_DEFINE` 定义。

---

## 19. 票价优惠策略（`PROMOTION_DEFINE` 命令）

### 概述

`PROMOTION_DEFINE` 块用于定义票价优惠策略（如学生卡、老人卡、月票等）。**这些策略仅供展示说明，不参与实际票价计算**（票价计算由 `PRICE_DEFINE` 决定）。

### 语法格式

```metro
PROMOTION_DEFINE
    POLICY 优惠ID
        NAME '优惠名称'
        DESCRIPTION '详细描述'          # 可选
        TYPE rate|fixed|fixed_discount
        VALUE 数值
        APPLICABLE_LINES [线路ID列表]   # 可选，空表示全线网
        TIME_RANGE HH:mm HH:mm          # 可选，有效时间段
        DAYS 星期修饰符                 # 可选，如 WORKDAY、WEEKEND 或 [Monday, ...]
    END_POLICY

    POLICY ...
END_DEFINE
```

### 子命令详解

| 子命令 | 参数/值 | 说明 |
|--------|---------|------|
| `POLICY` | `优惠ID`（标识符） | 开始定义一个优惠策略，`END_POLICY` 结束 |
| `NAME` | `'展示名称'` | 必填，显示给用户的名称 |
| `DESCRIPTION` | `'描述文本'` | 可选，详细说明 |
| `TYPE` | `rate` / `fixed` / `fixed_discount` | **必填**：`rate` 表示折扣比例（如 0.5 为半价）；`fixed` 表示一口价（如月票 100 元）；`fixed_discount` 表示固定减免金额（如换乘立减 1 元） |
| `VALUE` | 数字 | **必填**：对应类型的数值 |
| `APPLICABLE_LINES` | `[线路ID, ...]` | 可选，限制适用线路；若不指定或为空，表示适用于全路网 |
| `TIME_RANGE` | `HH:mm HH:mm` | 可选，有效时间段（如平峰优惠） |
| `DAYS` | `WORKDAY`/`WEEKEND`/单个星期/星期列表 | 可选，适用日期；默认每日 |

### 示例

```metro
PROMOTION_DEFINE
    POLICY student_card
        NAME '学生卡'
        DESCRIPTION '广州市中小学生卡'
        TYPE rate
        VALUE 0.5
        APPLICABLE_LINES [G1, G2]
    END_POLICY

    POLICY elder_card
        NAME '老人卡'
        DESCRIPTION '60岁以上老人'
        TYPE rate
        VALUE 0.4
        TIME_RANGE 09:00 17:00
        DAYS WORKDAY
    END_POLICY

    POLICY monthly_pass_g1
        NAME '1号线月票'
        DESCRIPTION '1号线无限次乘坐'
        TYPE fixed
        VALUE 100.0
        APPLICABLE_LINES [G1]
    END_POLICY

    POLICY transfer_discount
        NAME '换乘立减'
        DESCRIPTION '120分钟内换乘优惠1元'
        TYPE fixed_discount
        VALUE 1.0
    END_POLICY
END_DEFINE
```

### 注意事项
- 每个 `POLICY` 必须包含 `NAME`、`TYPE`、`VALUE`，否则编译器会报错。
- `APPLICABLE_LINES` 的线路ID必须已在 `LINE_DEFINE` 中定义。
- `DAYS` 支持单个星期（如 `MONDAY`）、预定义组合（`WORKDAY`/`WEEKEND`）或列表（如 `[MONDAY, WEDNESDAY]`），与现有规则保持一致。

---

## 20. 传统命令速查表（仍有效，但推荐使用新块语法）

| 命令 | 说明 | 推荐替代 |
|------|------|----------|
| `STATION_MODE '站名' 模式 ...` | 进出站模式 | 暂无块替代 |
| `OPEN_TIME [GLOBAL\|线路ID] ...` | 运营时间 | 暂无块替代 |
| `DEPOT 线路ID '站名' ...` | 车辆段 | 暂无块替代 |
| `VIA 方向A 方向B` | 共线声明 | 暂无块替代 |
| `STATION_STATUS ...` | 站点状态 | 暂无块替代 |

---

## 21. 示例编写

```metro
PROGRAM_DEFINE
    KEY_CITY '广州'
    OTHER_CITY ['佛山']
    VERSION 'V2.0'
    UPDATE_DATE '2026-07-11'
    AUTHOR '路网编制组'
    CREATE_DATE '2024-01-01'
END_DEFINE

STATION_DEFINE
    NODE '广州东站' COORD 113.332, 23.156
    NODE '体育中心' COORD 113.328, 23.142
    NODE '西塱' COORD 113.233, 23.082
END_DEFINE

OPERATION_DEFINE
    TRANSFER_TIME 180
    DWELL_TIME 30
    OUT_STATION_TRANSFER_TIME 300
    ENTRY_TIME 120
    AVG_BOARD_TIME 3
END_DEFINE

LINE_DEFINE GZ1
    NAME '1号线'
    COLOR '#F47920'
    DIRECTION_DEFINE GZ1_0
        STATIONS ['广州东站', '体育中心', '西塱']
        DISTANCE AUTO
        CROWD TIME_RANGE 07:00 09:00 1.5 WORKDAY
        HEADWAY TIME_RANGE 07:00 09:00 VALUE 120
    END_DEFINE
    DIRECTION_DEFINE GZ1_1 REV GZ1_0
END_DEFINE

PRICE_DEFINE
    DEFAULT_FARE 2.0
    SEGMENT 0 6 2.0
    SEGMENT 6 12 3.0
    LINEAR_EXTENSION THRESHOLD 12 BASE_FARE 3.0 RATE_PER_KM 0.2
END_DEFINE

FACILITY_DEFINE
    TOILET '体育中心' POSITION paid_area ACCESSIBLE GENDER mixed
    ELEVATOR '体育中心' LOCATION 'A口出口旁' ACCESSIBLE # 仅为示例
    ESCALATOR '体育中心' QUANTITY 4 DIRECTION both
    WIDE_GATE '体育中心' QUANTITY 2
    TACTILE_PAVING '体育中心' STATUS available
END_DEFINE

PROMOTION_DEFINE
    POLICY student_card
        NAME '学生卡'
        DESCRIPTION '广州市中小学生卡'
        TYPE rate
        VALUE 0.5
        APPLICABLE_LINES [G1, G2]
    END_POLICY
    POLICY elder_card
        NAME '老人卡'
        TYPE rate
        VALUE 0.4
        TIME_RANGE 09:00 17:00
        DAYS WORKDAY
    END_POLICY
END_DEFINE
```