# MetroLang JSON 格式参考文档

本文档描述 MetroLang 编译器生成的 JSON 数据结构，该结构是 Android 应用和 Web 端地图渲染的标准数据交换格式。所有字段均为可选，除非特别说明，缺失字段使用默认值。

---

## 根对象

```json
{
  "key_city": "广州",
  "author": "路网编制组",
  "version": "V2.0",
  "create_date": "2024-01-01",
  "update_date": "2026-07-11",
  "operation_params": { ... },
  "price": { ... },
  "stations": { ... },
  "lines": { ... },
  "headway_rules": [ ... ],
  "station_modes": [ ... ],
  "crowd_rules": [ ... ],
  "open_times": [ ... ],
  "depots": [ ... ],
  "speed_limits": [ ... ],
  "virtual_transfers": [ ... ],
  "labels": [ ... ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `key_city` | string | 主城市名称 |
| `author` | string | 作者，默认 `"匿名"` |
| `version` | string | 版本号，默认 `"V1.0"` |
| `create_date` | string | 创建日期 `YYYY-MM-DD`，默认编译当天 |
| `update_date` | string | 更新日期 `YYYY-MM-DD`，默认编译当天 |
| `operation_params` | object | 运营参数，见 §1 |
| `price` | object | 票价规则，见 §2 |
| `stations` | object | 站点字典（键为站名），见 §3 |
| `lines` | object | 线路字典（键为线路ID），见 §4 |
| `headway_rules` | array | 发车间隔规则，见 §5.1 |
| `station_modes` | array | 站点进出模式，见 §5.2 |
| `crowd_rules` | array | 拥挤度系数，见 §5.3 |
| `open_times` | array | 运营时间，见 §5.4 |
| `depots` | array | 车辆段，见 §5.5 |
| `speed_limits` | array | 区间限速，见 §5.6 |
| `virtual_transfers` | array | 虚拟换乘，见 §5.7 |
| `labels` | array | 文字标签，见 §5.8 |
| `zones` | array of Zone | 城市功能区/区域定义列表 |
| `operator_companies` | array of OperatorCompany | 运营公司定义列表 |

---

## 1. `operation_params` 运营参数

```json
{
  "inner_transfer_time": 180,
  "dwell_time": 30,
  "out_station_transfer_time": 300,
  "entry_time": 120
}
```

| 字段 | 类型 | 单位 | 说明 |
|------|------|------|------|
| `inner_transfer_time` | number | 秒 | 站内换乘平均时间，默认 180 |
| `dwell_time` | number | 秒 | 列车平均停站时间，默认 30 |
| `out_station_transfer_time` | number | 秒 | 出站换乘平均时间，默认 300 |
| `entry_time` | number | 秒 | 进站（闸机→站台）平均时间，默认 120 |

---

## 2. `price` 票价规则

```json
{
  "currency": "CNY",
  "pricingType": "distance",
  "default_fare": 2.0,
  "segments": [
    { "rangeStart": 0, "rangeEnd": 6, "fare": 2.0 },
    { "rangeStart": 6, "rangeEnd": 12, "fare": 3.0 }
  ],
  "linearExtension": {
    "threshold": 12,
    "baseFare": 3.0,
    "ratePerKm": 0.2
  },
  "transferDiscount": {
    "enabled": true,
    "discount_amount": 1.0,
    "time_limit_minutes": 120
  },
  "fare_cap": 15.0,
  "specialLines": [
    {
      "lineId": "APM",
      "all": 3.0
    },
    {
      "lineId": "G1",
      "surcharge": [
        { "node_a": "机场北", "node_b": "机场南", "price": 5.0 }
      ]
    }
  ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `currency` | string | 货币代码，默认 `"CNY"` |
| `pricingType` | string | 固定为 `"distance"` |
| `default_fare` | number | 起步价，默认 2.0 |
| `segments` | array of Segment | 分段票价（按里程） |
| `linearExtension` | LinearExtension | 线性延伸规则 |
| `transferDiscount` | TransferDiscount | 换乘优惠配置 |
| `fare_cap` | number | 单程票价封顶，0 表示无封顶 |
| `specialLines` | array of SpecialLine | 特殊线路票价 |

### Segment
| 字段 | 类型 | 说明 |
|------|------|------|
| `rangeStart` | number | 区间起始里程（km） |
| `rangeEnd` | number | 区间结束里程（km） |
| `fare` | number | 该区间票价（元） |

### LinearExtension
| 字段 | 类型 | 说明 |
|------|------|------|
| `threshold` | number | 线性延伸起始里程（km） |
| `baseFare` | number | 基础票价 |
| `ratePerKm` | number | 每公里加价（元/km） |

### TransferDiscount
| 字段 | 类型 | 说明 |
|------|------|------|
| `enabled` | boolean | 是否启用 |
| `discount_amount` | number | 优惠金额（元） |
| `time_limit_minutes` | number | 有效时间限制（分钟） |

### SpecialLine
| 字段 | 类型 | 说明 |
|------|------|------|
| `lineId` | string | 线路ID（当 `use_lines` 不存在时生效） |
| `all` | number (可选) | 统一票价（整个线路） |
| `surcharge` | array of SurchargeItem (可选) | 区间附加费列表 |
| `use_lines` | array of string (可选) | 适用线路列表（城市/线路组票价） |
| `default_fare` | number (可选) | 组内默认起步价 |
| `segments` | array of Segment (可选) | 组内分段票价 |
| `linear_extension` | LinearExtension (可选) | 组内线性延伸 |

### SurchargeItem
| 字段 | 类型 | 说明 |
|------|------|------|
| `node_a` | string | 起始站 |
| `node_b` | string | 终点站 |
| `price` | number | 附加票价（元） |

---

## 3. `stations` 站点字典

键为站名字符串，值为 `Station` 对象。

```json
{
  "广州东站": {
    "lon": 113.332,
    "lat": 23.156,
    "city": "广州",
    "lines": [
      { "id": "G1", "start": "广州东站", "end": "西塱", "status": "operating" }
    ],
    "transfers": {
      "G1|G2": "operating",
      "G2|G1": {
        "status": "out_station",
        "time": 420,
        "transfer_type": "同台换乘"
      }
    }
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `lon` | number | 经度 (WGS-84) |
| `lat` | number | 纬度 (WGS-84) |
| `city` | string (可选) | 所属城市，若未提供则由编译器推断 |
| `lines` | array of StationLine | 途经该站的所有方向信息 |
| `transfers` | object (可选) | 自定义换乘信息，键为 `"线路A\|线路B"`，值见下 |

### StationLine
| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 线路ID |
| `start` | string | 该方向的起始站 |
| `end` | string | 该方向的终点站 |
| `status` | string (可选) | 该站在该线路上的状态：`"operating"`, `"pending"`, `"out_station"` |
| `platform_coord` | object (可选) | 该线路在该车站的平台坐标（独立于全局坐标）。格式：`{ "lon": number, "lat": number }`。若存在，前端绘制线路路径时优先使用此坐标。 |

### `transfers` 值说明
- **字符串**：仅表示状态（如 `"operating"`），换乘时间使用全局默认值。
- **对象**：可包含：
  - `status`：`"operating"`, `"pending"`, `"out_station"`
  - `time` (可选)：换乘时间（秒）
  - `transfer_type` (可选)：换乘类型名称（由 `TRANSFER_TYPE` 定义）

---

## 4. `lines` 线路字典

键为线路ID，值为 `Line` 对象。

```json
{
  "G1": {
    "color": "#F47920",
    "formation": "6B",
    "open_year": 2000,
    "city": "广州",
    "maxspeed": 80,
    "link": "https://...",
    "name": "1号线",
    "power_kw": 3680,
    "brake": 0.85,
    "disabled": false,
    "special": false,
    "direction": [
      {
        "is_branch": false,
        "is_fast": false,
        "start": "广州东站",
        "end": "西塱",
        "is_loop": false,
        "direction_type": null,
        "nick_name": null,
        "nick_color": null,
        "station": [
          { "name": "广州东站", "next": 1.5, "dwell_time": 30 },
          { "name": "体育中心", "next": 2.0, "dwell_time": 30 },
          { "name": "西塱", "next": null }
        ],
        "is_future": false,
        "connect_station": null,
        "via_directions": [ "G1_1" ]
      }
    ]
  }
}
```

### Line
| 字段 | 类型 | 说明 |
|------|------|------|
| `color` | string | 十六进制颜色值（带 `#`） |
| `formation` | string | 编组信息，如 `"6B"` |
| `open_year` | number | 开通年份 |
| `city` | string | 所属城市 |
| `maxspeed` | number | 最高速度 (km/h) |
| `link` | string | 百科或官网 URL |
| `name` | string | 显示名称 |
| `power_kw` | number (可选) | 牵引功率 (kW) |
| `brake` | number (可选) | 制动减速度 (m/s?) |
| `disabled` | boolean (可选) | 是否已停运 |
| `special` | boolean | 是否为特殊线路（机场线、APM等） |
| `direction` | array of Direction | 该线路所有方向列表 |

### Direction
| 字段 | 类型 | 说明 |
|------|------|------|
| `is_branch` | boolean | 是否为支线 |
| `is_fast` | boolean | 是否为快车 |
| `start` | string | 始发站 |
| `end` | string | 终点站 |
| `is_loop` | boolean | 是否为环线 |
| `direction_type` | string (可选) | 环线方向：`"inner"` 或 `"outer"` |
| `nick_name` | string (可选) | 方向别名 |
| `nick_color` | string (可选) | 别名颜色 |
| `station` | array of DirectionStation | 站点顺序列表（从起点到终点） |
| `is_future` | boolean | 是否为规划/未来线路 |
| `connect_station` | string (可选) | 接驳站 |
| `via_directions` | array of string (可选) | 经过的方向ID列表（共线/跨线） |

### DirectionStation
| 字段 | 类型 | 说明 |
|------|------|------|
| `name` | string | 站名 |
| `next` | number 或 null | 到下一站的距离（km），`null` 表示终点（无下一站） |
| `dwell_time` | number (可选) | 该站停站时间（秒） |

---

## 5. 时段规则（含 `days` 字段）

以下所有规则均包含可选 `days` 字段，用于指定适用日期。取值可为：
- 星期名称全称：`"Monday"`, `"Tuesday"`, ..., `"Sunday"`
- 预定义组合：`"WORKDAY"`（周一至周五）, `"WEEKEND"`（周六周日）
- 数组：`["Monday", "Wednesday", "Friday"]`

若 `days` 不存在或为空，则视为每日适用。

### 5.1 `headway_rules` 发车间隔
| 字段 | 类型 | 说明 |
|------|------|------|
| `line_id` | string | 线路ID |
| `start_time` | string | 起始时间 `"HH:mm"` |
| `end_time` | string | 结束时间 `"HH:mm"` |
| `interval_seconds` | number | 发车间隔（秒） |
| `days` | array of string (可选) | 适用日期 |

### 5.2 `station_modes` 站点进出模式
| 字段 | 类型 | 说明 |
|------|------|------|
| `station` | string | 站名 |
| `mode` | string | `"ENTRY_ONLY"`, `"EXIT_ONLY"`, `"BOTH"` |
| `start_time` | string (可选) | 时段开始 |
| `end_time` | string (可选) | 时段结束 |
| `days` | array of string (可选) | 适用日期 |

### 5.3 `crowd_rules` 拥挤度系数
| 字段 | 类型 | 说明 |
|------|------|------|
| `direction_key` | string | 方向标识，格式 `"线路ID\|起始站\|终点站"`（由编译器自动生成） |
| `start_time` | string | 起始时间 `"HH:mm"` |
| `end_time` | string | 结束时间 `"HH:mm"` |
| `factor` | number | 拥挤度系数（≥1.0） |
| `days` | array of string (可选) | 适用日期 |

### 5.4 `open_times` 运营时间
| 字段 | 类型 | 说明 |
|------|------|------|
| `line_id` | string 或 null | 线路ID，`null` 表示全局默认 |
| `start_time` | string | 首班车时间 `"HH:mm"` |
| `end_time` | string | 末班车时间 `"HH:mm"` |
| `days` | array of string (可选) | 适用日期 |

### 5.5 `depots` 车辆段
| 字段 | 类型 | 说明 |
|------|------|------|
| `line_id` | string | 所属线路ID |
| `station` | string | 所在车站 |
| `direction_key` | string (可选) | 关联方向（外部格式） |
| `departure_time` | string (可选) | 首班发车时间 `"HH:mm"` |

### 5.6 `speed_limits` 区间限速
| 字段 | 类型 | 说明 |
|------|------|------|
| `direction_key` | string | 方向标识（外部格式） |
| `from_station` | string | 起始站 |
| `to_station` | string | 终点站 |
| `speed_kmh` | number | 限速值 (km/h) |

### 5.7 `virtual_transfers` 虚拟换乘
| 字段 | 类型 | 说明 |
|------|------|------|
| `station_a` | string | 起点站 |
| `station_b` | string | 终点站 |
| `time_minutes` | number | 换乘时间（分钟） |
| `security_check` | boolean | 是否需要重新安检 |
| `via_directions` | array of string (可选) | 经过的方向ID列表（内部ID） |

### 5.8 `labels` 文字标签
| 字段 | 类型 | 说明 |
|------|------|------|
| `text` | string | 显示文本 |
| `lon` | number | 经度 |
| `lat` | number | 纬度 |

---

## 6. 校验规则

- 所有引用的站名、线路ID必须在 `stations` 和 `lines` 中定义。
- 每条线路的 `direction` 列表至少包含一个方向。
- 非环线方向中，`DirectionStation.next` 的最后一个元素必须为 `null`。
- `transfers` 中的换乘对应对称出现（双向）。
- `crowd_rules` 的 `factor` ≥ 1.0。
- 时间字段遵循 24 小时制 `HH:mm`。
- `specialLines` 中引用的线路必须存在且其 `special` 为 `true`。

---

## 7 无障碍设施（`facilities` 字段）

在根对象中新增顶级字段 `facilities`，类型为 `object`（键为站名，值为 `StationFacilities` 对象）。

```json
{
  "facilities": {
    "公园前": {
      "toilet": {
        "position": "paid_area",
        "accessible": true,
        "gender": "mixed"
      },
      "accessible_toilet": {
        "position": "paid_area",
        "gender": "mixed"
      },
      "elevators": [
        { "location": "A口出口旁", "accessible": true }
      ],
      "wide_gate": { "quantity": 2 },
      "escalators": [
        { "quantity": 4, "direction": "both" }
      ],
      "tactile_paving": { "status": "available" }
    }
  }
}
```

**字段说明**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `toilet` | ToiletInfo | 普通卫生间信息（可选） |
| `accessible_toilet` | ToiletInfo | 无障碍卫生间信息（可选） |
| `elevators` | ElevatorInfo[] | 无障碍电梯列表（可选） |
| `wide_gate` | WideGateInfo | 宽闸机信息（可选） |
| `escalators` | EscalatorInfo[] | 自动扶梯列表（可选） |
| `tactile_paving` | TactilePavingInfo | 盲道铺设信息（可选） |

**内部对象结构**：

- **ToiletInfo**
  - `position`：`"paid_area"` 或 `"unpaid_area"`
  - `accessible`：boolean
  - `gender`：`"mixed"`、`"male"` 或 `"female"`

- **ElevatorInfo**
  - `location`：string（位置描述）
  - `accessible`：boolean

- **WideGateInfo**
  - `quantity`：number

- **EscalatorInfo**
  - `quantity`：number
  - `direction`：`"up"`、`"down"` 或 `"both"`

- **TactilePavingInfo**
  - `status`：`"available"`、`"partial"` 或 `"none"`

## 8 票价优惠策略（`promotions` 字段）

在根对象中新增顶级数组 `promotions`，每项为 `PromotionPolicy`。

```json
{
  "promotions": [
    {
      "id": "student_card",
      "name": "学生卡",
      "description": "广州市中小学生卡",
      "type": "rate",
      "value": 0.5,
      "applicable_lines": [ "G1", "G2" ]
    },
    {
      "id": "elder_card",
      "name": "老人卡",
      "description": "60岁以上老人",
      "type": "rate",
      "value": 0.4,
      "start_time": "09:00",
      "end_time": "17:00",
      "days": [ "WORKDAY" ]
    }
  ]
}
```

**字段说明**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | string | 优惠唯一标识，如 `"student_card"` |
| `name` | string | 显示名称 |
| `description` | string | 详细描述（可选） |
| `type` | string | `"rate"`、`"fixed"` 或 `"fixed_discount"` |
| `value` | number | 对应类型的数值 |
| `applicable_lines` | string[] | 适用线路列表（可选），空或省略表示全路网 |
| `start_time` | string | 时段起始时间 `HH:mm`（可选） |
| `end_time` | string | 时段结束时间 `HH:mm`（可选） |
| `days` | string[] | 适用日期列表（可选），取值可为星期全称或 `"WORKDAY"`/`"WEEKEND"` |

# 9. Zone 对象结构：
```json
{
  "id": "zone_zhujiang",
  "name": "珠江新城",
  "type": "CBD",
  "city": "广州",
  "color": "#FF6B6B",
  "center": { "lon": 113.322, "lat": 23.118 },
  "radius": null,
  "polygon": [
    { "lon": 113.312, "lat": 23.112 },
    { "lon": 113.325, "lat": 23.110 },
    { "lon": 113.330, "lat": 23.125 },
    { "lon": 113.315, "lat": 23.130 }
  ],
  "stations": ["珠江新城", "猎德", "妇儿中心"],
  "properties": {
    "peak_hours": ["08:30-09:30", "18:00-19:30"],
    "population_density": 3.5,
    "avg_rent": 85.0
  }
}
```

| 字段 | 类型 | 说明 |
| :--- | :--- | :--- |
| `id` | string | 区域唯一标识（如 `zone_tianhe`） |
| `name` | string | 显示名称 |
| `type` | string | 类型枚举（见上表） |
| `city` | string | 所属城市，默认继承 `key_city` |
| `color` | string | 十六进制颜色值，含 `#`，用于前端渲染 |
| `center` | object | 中心点坐标 `{ lon, lat }`（可选） |
| `radius` | number | 圆形半径（米），与 `polygon` 互斥（可选） |
| `polygon` | array of { lon, lat } | 多边形边界点集（可选），优先于 `center`/`radius` 用于面渲染 |
| `stations` | array of string | 关联的站点名称列表，用于客流OD分析或“该区域最近站点”查询 |
| `properties` | object | 扩展属性字典（可选），业务方可自定义内部字段 |

---

# 10. OperatorCompany 对象结构（`operator_companies`）

```json
{
  "id": "gz_metro",
  "name": "广州地铁集团有限公司",
  "short_name": "广州地铁",
  "en_name": "Guangzhou Metro Group",
  "country": "中国",
  "city": "广州",
  "founded": 1992,
  "website": "https://www.gzmtr.com",
  "hotline": "020-96891",
  "description": "负责广州城市轨道交通的建设与运营",
  "logo_base64": "data:image/png;base64,....",
  "lines": ["LINE1", "LINE2", "LINE3"]
}
```

| 字段 | 类型 | 说明 |
| :--- | :--- | :--- |
| `id` | string | 运营公司唯一标识（如 `gz_metro`） |
| `name` | string | 公司显示名称（必填） |
| `short_name` | string | 公司简称（可选） |
| `en_name` | string | 英文名称（可选） |
| `country` | string | 国家/地区（可选） |
| `city` | string | 总部所在城市（可选，默认继承 `key_city`） |
| `founded` | number | 成立年份（可选） |
| `website` | string | 官方网站 URL（可选） |
| `hotline` | string | 客服热线/联系电话（可选） |
| `description` | string | 公司简介（可选） |
| `logo_base64` | string | 公司 LOGO 的 Base64 编码（可选，仅前端展示） |
| `lines` | array of string | 该公司运营的线路 ID 列表（可选） |

---

## 11. 扩展性

该格式预留扩展能力，未来可能增加：
- 站点 POI 数据
