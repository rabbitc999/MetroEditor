# MetroLang 语言参考手册

MetroLang 是一种面向地铁网络描述的领域特定语言（DSL），设计目标为**可读性强、版本控制友好、支持人工编辑与自动化编译**。  
本手册定义其语法、命令及语义，并明确区分**块语法**与**单行命令**。所有块均使用 `END_DEFINE` 作为结束标记。

---

## 1. 基本约定

- **关键字不区分大小写**：`PROGRAM_DEFINE` 与 `program_DEFINE` 等效。
- **字符串**：使用单引号 `'` 或双引号 `"` 包裹，内部支持转义（如 `\'`）。
- **标识符**：线路ID、方向ID等由字母、数字、下划线组成，不能以数字开头。
- **注释**：`#` 到行尾的内容被忽略。
- **换行与缩进**：命令通常独占一行，列表元素可用逗号或换行分隔，缩进不影响语义。
- **时间值**：支持三种格式，统一转换为**秒**：
  - 纯数字（如 `30`） → 30 秒
  - 数字加冒号（如 `1:`） → 1 分钟（60 秒）
  - 分:秒（如 `1:30`） → 90 秒
- **块结构**：使用 `命令 ... END_DEFINE` 界定，块内子命令顺序自由，支持嵌套块。
- **通配符**：在 `COPY`、`RENAME`、`DELETE` 等操作中支持 `*` 匹配任意字符串，用于批量操作。

---

## 2. 预处理指令

预处理指令在词法分析后、语法分析前执行，以 `%` 开头，独占一行。

| 指令 | 语法 | 说明 |
|------|------|------|
| `%DEFINE` | `%DEFINE 符号名` | 定义编译期符号（大小写不敏感） |
| `%UNDEFINE` | `%UNDEFINE 符号名` | 移除符号定义 |
| `%IF` | `%IF 符号名` | 条件编译开始，若符号已定义则保留后续代码 |
| `%ELSE` | `%ELSE` | 与最近的 `%IF` 配对，取反条件 |
| `%ENDIF` | `%ENDIF` | 结束条件编译块 |
| `%INCLUDE` | `%INCLUDE "文件路径"` | 嵌入外部 `.metro` 文件（支持相对/绝对路径，自动防循环） |
| `%IMPORT` | `%IMPORT "文件路径" AS STATIONS` | 从 CSV 导入站点数据（自动生成 `STATION_DEFINE`） |

**示例**：
```metro
%DEFINE CITY_BJ
%IF CITY_BJ
    %INCLUDE "beijing_config.metro"
%ELSE
    %INCLUDE "shanghai_config.metro"
%ENDIF

%IMPORT "stations.csv" AS STATIONS
```

---

## 3. 程序定义（`PROGRAM_DEFINE` 块）

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

## 4. 站点定义（`STATION_DEFINE` 块）

```metro
STATION_DEFINE
    NODE '站名' COORD 经度, 纬度 [CITY '城市']
    OTHER_NAME '别名'                   # 紧随 NODE 之后，为当前站点添加多语言别名
    NODE ...
END_DEFINE
```

- `NODE`：定义单个站点，包含站名、经纬度坐标，可选指定所属城市。
- `OTHER_NAME`：紧随 `NODE` 之后，为当前站点添加别名（支持单字符串或列表），用于多语言展示。
- 如果未指定 `CITY`，编译器会根据途经线路自动推断。
- **站点键归一化**：若同一站名出现在多个城市，内部使用 `城市::站名` 存储；若仅一个城市，则使用纯站名（兼容旧逻辑）。最终导出 JSON 时，复合键格式为 `站名 (城市)`。

---

## 5. 站点操作命令（单行）

| 命令 | 语法 | 说明 |
|------|------|------|
| `STATION_COPY` | `STATION_COPY 旧站名 新站名` | 复制站点坐标、城市、别名（支持通配符） |
| `STATION_RENAME` | `STATION_RENAME 旧站名 新站名` | 重命名站点，自动更新所有引用（支持通配符） |
| `STATION_DELETE` | `STATION_DELETE 站名 [站名...]` | 删除站点，若被方向引用则报错（支持通配符） |
| `STATION_MODIFY` | `STATION_MODIFY 站名 ... END_DEFINE` | 修改站点属性（坐标、城市、别名等） |

**`STATION_MODIFY` 块内子命令**：
```metro
STATION_MODIFY '旧站名'
    SET COORD 经度, 纬度
    SET CITY '城市'
    SET OTHER_NAMES ['别名1', '别名2']   # 覆盖别名列表
    ADD OTHER_NAME '别名'                # 追加别名
    REMOVE OTHER_NAME '别名'             # 删除别名
END_DEFINE
```

---

## 6. 线路模板（`LINE_TEMPLATE` 块）

定义可复用的线路属性模板，支持继承。

```metro
LINE_TEMPLATE 模板ID [INHERITS 父模板ID]
    NAME '名称'
    COLOR '颜色'
    CITY '城市'
    OPEN_YEAR 年份
    FORMATION '编组'
    MAXSPEED 速度
    POWER 功率
    BRAKE 减速度
    DISABLED TRUE|FALSE
    IMAGE '图片路径'
    LINK 'URL'
    SPECIAL
END_DEFINE
```

- 在 `LINE_DEFINE` 中使用 `INHERITS 模板ID` 应用模板，可覆盖模板中的属性。

---

## 7. 线路定义（`LINE_DEFINE` 块）

```metro
LINE_DEFINE 线路ID [INHERITS 模板ID]
    NAME '线路名称'                    # 必填
    COLOR '颜色'                       # 必填
    CITY '城市'                        # 可选，默认 KEY_CITY
    FORMATION '编组'                   # 如 '6B', '8A'
    MAXSPEED 80                        # 最高速度 (km/h)
    OPEN_YEAR 2000                     # 开通年份
    POWER 3680                      # 牵引功率 (kW)
    BRAKE 0.85                         # 制动减速度 (m/s²)
    LINK 'URL'                         # 百科链接
    IMAGE '图片路径'                   # 线路示意图（转为 Base64 存储）
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
- `IMAGE` 支持相对路径（相对于当前文件）和绝对路径，图片内容自动转为 Base64。

---

## 8. 线路操作命令（单行，支持通配符 `*`）

| 命令 | 语法 | 说明 |
|------|------|------|
| `LINE_COPY` | `LINE_COPY 源模式 目标模式` | 复制整条线路及所有关联数据（方向、票价、时刻表等） |
| `LINE_RENAME` | `LINE_RENAME 源模式 目标模式` | 重命名线路，自动更新所有引用 |
| `LINE_DELETE` | `LINE_DELETE 线路ID [线路ID...]` | 删除线路及所有关联数据 |
| `LINE_MERGE` | `LINE_MERGE 线路1 线路2 ... INTO 新线路 [KEEP_ORIGINAL]` | 合并多条线路为一条新线路（自动处理方向ID冲突） |
| `LINE_MODIFY` | `LINE_MODIFY 线路ID ... END_DEFINE` | 修改线路属性 |

**`LINE_MODIFY` 块内子命令**：
```metro
LINE_MODIFY 线路ID
    SET NAME '新名称'
    SET COLOR '#新颜色'
    SET MAXSPEED 新速度
    SET IMAGE '新图片路径'
    ADD SPECIAL
    REMOVE SPECIAL
END_DEFINE
```

---

## 9. 方向模板（`DIRECTION_TEMPLATE` 块）

定义可复用的方向属性模板，支持继承。

```metro
DIRECTION_TEMPLATE 模板ID [INHERITS 父模板ID]
    NICK_NAME '别名'
    NICK_COLOR '颜色'
    IS_FUTURE TRUE|FALSE
    CONNECT_STATION '站名'
    LOOP [INNER | OUTER]
    BRANCH
    FAST
END_DEFINE
```

- 在 `DIRECTION_DEFINE` 中使用 `INHERITS 模板ID` 应用模板，可覆盖模板中的属性。

---

## 10. 方向定义（`DIRECTION_DEFINE` 块）

```metro
DIRECTION_DEFINE 方向ID [INHERITS 模板ID] [线路ID]      # 独立使用时需提供线路ID，嵌套时可省略
    STATIONS ['站1', '站2', ...]       # 必填（除非使用 MERGE 或 REVERSE）
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
    MERGE 方向A 方向B                  # 合并两个方向（公共后缀-前缀匹配）
END_DEFINE
```

**反向方向简写**（立即返回，不处理后续子命令）：
```metro
DIRECTION_DEFINE 方向ID REV 原方向ID
```

- `REV` 为纯快捷方式，自动继承原方向所有属性并反转站点和距离，**不能**再添加额外子命令。
- `REVERSE` 同样复制并反转，但允许在块内继续添加额外子命令（如增加 `NICK_NAME`、`CROWD` 等），支持更灵活的反向定义。
- `MERGE` 将两个已有方向合并为一个新方向（站点和距离自动拼接），要求两个方向有公共后缀-前缀。

**距离列表说明**：
- 非环线方向：距离数量 = 站点数 - 1，最后一个距离应为 `NIL`。
- 环线方向：距离数量 = 站点数（首尾相连），无 `NIL`。

**星期修饰符**（用于 `CROWD`、`HEADWAY`、`STATION_MODE`、`OPEN_TIME`）：
- 单个星期：`MONDAY`、`TUESDAY`、`WEDNESDAY`、`THURSDAY`、`FRIDAY`、`SATURDAY`、`SUNDAY`
- 列表：`[MONDAY, WEDNESDAY, FRIDAY]`
- 预定义组合：`WORKDAY`（周一至周五）、`WEEKEND`（周六周日）

---

## 11. 方向操作命令（单行，支持通配符 `*`）

| 命令 | 语法 | 说明 |
|------|------|------|
| `DIRECTION_COPY` | `DIRECTION_COPY 源模式 目标模式` | 复制方向（站点、距离、属性） |
| `DIRECTION_RENAME` | `DIRECTION_RENAME 源模式 目标模式` | 重命名方向，自动更新所有引用 |
| `DIRECTION_DELETE` | `DIRECTION_DELETE 方向ID [方向ID...]` | 删除方向，自动清理引用规则 |
| `DIRECTION_MODIFY` | `DIRECTION_MODIFY 方向ID ... END_DEFINE` | 修改方向属性 |
| `DIRECTION_SPLIT` | `DIRECTION_SPLIT 方向ID AT '站名' INTO 新方向1 新方向2` | 在指定站点拆分方向为两个新方向 |
| `DIRECTION_REVERSE` | `DIRECTION_REVERSE 方向ID [新方向ID]` | 原地反转或复制反转方向（自动更新规则） |

**`DIRECTION_MODIFY` 块内子命令**：
```metro
DIRECTION_MODIFY 方向ID
    ADD BRANCH | FAST | LOOP            # 添加属性
    REMOVE BRANCH | FAST | LOOP         # 移除属性
    SET NICK_NAME '新别名'
    SET NICK_COLOR '#新颜色'
    SET CONNECT_STATION '站名'
    SET IS_FUTURE TRUE|FALSE
    SET DIRECTION_TYPE INNER|OUTER      # 仅环线有效
END_DEFINE
```

---

## 12. 票价定义（`PRICE_DEFINE` 块）

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

## 13. 换乘定义

### 换乘类型定义（`TRANSFER_TYPE` 块）
```metro
TRANSFER_TYPE 类型ID '类型名称'
    TIME 秒数
    STATUS OPERATING | PENDING | OUT_STATION
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
    STATUS OPERATING | PENDING | OUT_STATION
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

## 14. 运营参数（`OPERATION_DEFINE` 块）

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

## 15. 时段配置文件（块语法）

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

## 16. 停站时间定义（`DWELL_TIME_DEFINE` 块）

```metro
DWELL_TIME_DEFINE
    NODE '站名' 线路ID 秒数            # 指定站点在某线路上的停站时间
    LINE 线路ID 秒数                   # 为某线路的所有站点统一设置停站时间
    DIRECTION 方向ID 秒数              # 为某方向上的所有站点统一设置停站时间
END_DEFINE
```
- 三种子命令可根据需要混合使用，优先级为 `NODE` > `DIRECTION` > `LINE`（即最具体的覆盖更广泛的）。

---

## 17. 限速定义（`SPEED_LIMIT_DEFINE` 块）

```metro
SPEED_LIMIT_DEFINE 方向ID
    NODE '站A' '站B' 速度(km/h)
    NODE ...
END_DEFINE
```

---

## 18. 站点进出模式（单行命令）

```metro
STATION_MODE '站名' 模式 [TIME_RANGE HH:mm HH:mm] [星期修饰符]
```
- 模式：`ENTRY_ONLY`（仅进站）、`EXIT_ONLY`（仅出站）、`BOTH`（正常）。

---

## 19. 运营时间（单行命令）

```metro
OPEN_TIME [GLOBAL | 线路ID] HH:mm HH:mm [星期修饰符]
```
- `GLOBAL` 表示全局默认时间。

---

## 20. 车辆段（单行命令）

```metro
DEPOT 线路ID '站名' [方向ID] [HH:mm]
```

---

## 21. 共线/跨线声明（单行命令）

```metro
VIA 方向A 方向B
```
将 `方向B` 添加到 `方向A` 的 `via_directions` 列表中，用于指示共线关系。

---

## 22. 标签定义（`LABEL_DEFINE` 块）

```metro
LABEL_DEFINE
    NODE '标签文本' COORD 经度, 纬度
    NODE ...
END_DEFINE
```

---

## 23. 车站状态（单行命令）

```metro
STATION_STATUS ['站名1', '站名2'] [线路ID] 状态
```
- 为指定站点（单个或列表）在特定线路（可选）上设置状态。
- 状态：`OPERATING`、`PENDING`、`OUT_STATION`。

---

## 24. 无障碍设施（`FACILITY_DEFINE` 块）

`FACILITY_DEFINE` 块用于描述各站点的无障碍设施、卫生间、电梯、自动扶梯、盲道等信息。**这些数据仅用于前端展示和查询，不参与路径规划或票价计算。**

```metro
FACILITY_DEFINE
    TOILET '站名' POSITION PAID_AREA [ACCESSIBLE] [GENDER MALE|FEMALE|MIXED]
    ACCESSIBLE_TOILET '站名' POSITION PAID_AREA [ACCESSIBLE] [GENDER MALE|FEMALE|MIXED]
    ELEVATOR '站名' LOCATION '具体位置描述' [ACCESSIBLE]
    WIDE_GATE '站名' QUANTITY 数量
    ESCALATOR '站名' QUANTITY 数量 [DIRECTION up|down|both]
    TACTILE_PAVING '站名' STATUS available|partial|none
END_DEFINE
```

**参数说明**：
- `POSITION`：`PAID_AREA`（付费区内）或 `UNPAID_AREA`（付费区外）。
- `ACCESSIBLE`：表示具备无障碍设施。
- `GENDER`：卫生间性别，默认 `MIXED`。

---

## 25. 票价优惠策略（`PROMOTION_DEFINE` 块）

```metro
PROMOTION_DEFINE
    POLICY 优惠ID
        NAME '优惠名称'
        DESCRIPTION '详细描述'
        TYPE RATE|FIXED|FIXED_DISCOUNT
        VALUE 数值
        APPLICABLE_LINES [线路ID列表]   # 可选，空表示全线网
        TIME_RANGE HH:mm HH:mm          # 可选
        DAYS 星期修饰符                 # 可选
    END_POLICY
    POLICY ...
END_DEFINE
```

**子命令详解**：
- `TYPE`：`RATE`（折扣比例）、`FIXED`（一口价）、`FIXED_DISCOUNT`（固定减免）。
- `VALUE`：对应类型的数值。
- `APPLICABLE_LINES`：限制适用线路，不指定或为空表示全线网。

---

## 26. 编译时断言（`ASSERT`）

`ASSERT` 在编译完成后执行，用于自定义业务校验。

```metro
ASSERT <表达式> [MESSAGE "自定义错误信息"]
```

**支持的谓词**（括号内参数为字符串，用引号括起）：
- `DIRECTION_EXISTS("方向ID")`
- `LINE_EXISTS("线路ID")`
- `STATION_EXISTS("站名")`
- `STATION_COUNT("线路ID")`（返回站点数）
- `TRANSFER_EXISTS("站名", "线路A", "线路B")`
- `DISTANCE_BETWEEN("站A", "站B")`（返回米数）
- `DIRECTION_ATTR("方向ID", "属性名")`（如 BRANCH、FAST）

**比较操作**：`<`, `>`, `<=`, `>=`, `==`, `!=`（仅对数值有效）  
**逻辑运算**：`AND`, `OR`, `NOT`，支持括号分组。

**示例**：
```metro
ASSERT DIRECTION_EXISTS("LINE1_0") MESSAGE "1号线正向方向未定义"
ASSERT STATION_COUNT("LINE1") >= 5 MESSAGE "1号线站点数不足"
ASSERT (DIRECTION_EXISTS("LINE1_0") AND STATION_EXISTS("C")) MESSAGE "方向或站点缺失"
ASSERT DISTANCE_BETWEEN("A", "B") < 5000
```

---

## 27. 后处理输出（`ECHO`）

`ECHO` 在编译完成后输出信息，用于调试和数据统计。

```metro
ECHO "自定义消息"
ECHO "字符串{变量}字符串"
```

**内置变量**：
- `STATION_COUNT`（站点总数）
- `LINE_COUNT`（线路总数）
- `DIRECTION_COUNT`（方向总数）
- `TRANSFER_COUNT`（换乘规则总数）
- `CITY`（主城市名称）
- `VERSION`（数据版本）

**示例**：
```metro
ECHO "数据加载完成"
ECHO "主城市：{CITY}"
ECHO "站点总数：{STATION_COUNT}"
ECHO "方向总数：{DIRECTION_COUNT}"
```

---

## 28. 完整示例

```metro
%DEFINE CITY_BJ
%INCLUDE "base_stations.metro"

PROGRAM_DEFINE
    KEY_CITY '北京'
    VERSION 'V2.0'
    UPDATE_DATE '2026-07-11'
    AUTHOR '路网编制组'
END_DEFINE

STATION_DEFINE
    NODE '复兴门' COORD 116.35, 39.91 CITY '北京'
    NODE '西单' COORD 116.37, 39.91
END_DEFINE

LINE_TEMPLATE URBAN
    FORMATION '6B'
    MAXSPEED 80
END_DEFINE

LINE_DEFINE LINE1 INHERITS URBAN
    NAME '1号线'
    COLOR '#FF0000'
    CITY '北京'
    IMAGE 'line1.png'
    DIRECTION_DEFINE LINE1_0
        STATIONS ['复兴门', '西单']
        DISTANCE AUTO
    END_DEFINE
END_DEFINE

%IF CITY_BJ
    LINE_COPY LINE1 LINE1_BACKUP
%ENDIF

DIRECTION_REVERSE LINE1_0 LINE1_1

ASSERT DIRECTION_EXISTS("LINE1_0") MESSAGE "方向未定义"

ECHO "编译完成，版本：{VERSION}"
```