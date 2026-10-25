# MetroView 开发者文档

> 本文档面向 MetroView 项目的开发者与维护者，涵盖架构设计、代码组织、构建说明和编码约定。
> 用户向功能介绍请参见 [README.md](README.md)。

---

## 1. 项目定位

MetroView（包名 `com.rabbitc.subway`）是 [Metro Editor](https://gitee.com/embed2026/metro_editor) 桌面端编辑器的配套 Android 客户端，承担"数据消费"角色：加载 Metro Editor 编译输出的 JSON/BMetro 数据文件，提供交互式地铁地图浏览与 9 种路径规划算法。

**核心原则：纯消费端，不包含任何编辑功能。**

---

## 2. 技术栈与版本

| 项目 | 版本/技术 |
|:---|:---|
| 语言 | Java 11（无 Kotlin） |
| 最低 SDK | 31（Android 12） |
| 目标 SDK | 35（Android 15） |
| 构建工具 | Gradle 8.8 + AGP 8.8.0 |
| JSON 解析 | Gson 2.11.0 |
| UI 框架 | Material Design 1.10.0 + AndroidX |
| 地图渲染 | WebView + D3.js（全图）/ Canvas（单线路示意图） |
| 依赖管理 | Gradle Version Catalog (`gradle/libs.versions.toml`) |

---

## 3. 项目结构

```
MetroView/
├── app/src/main/java/com/embed2026/subway/
│   ├── activity/          # UI 层 — 8 个 Activity（含 BaseActivity 基类）
│   ├── adapter/           # 适配器层 — 5 个 ListView 适配器
│   ├── algroithm/         # 算法层 — 图构建、路径搜索、票价/时间计算
│   │   └── graph/         #   图数据结构（Edge、NodeState、Segment）
│   ├── data/              # 数据层 — 全局缓存、加载器、解析器、DTO
│   │   ├── json/          #   Gson POJO — 28 个 JSON 映射类
│   │   └── parse/         #   解析后结构 — 10 个运行时缓存模型
│   ├── listener/          # 回调接口 — 2 个站点交互监听器
│   ├── utils/             # 工具层 — 10 个工具类
│   ├── view/              # 自定义视图 — SubwayMapView / LineMapView
│   └── widget/            # 自定义控件 — FlowLayout / StartEndBar / LegendListView
├── app/src/main/assets/   # Web 前端资源（map.html, map.css, map.js, d3.min.js）
└── app/src/main/res/      # Android 资源（layout, values, drawable, mipmap）
```

### 包职责速览

| 包 | 职责 | 依赖方向 |
|:---|:---|:---|
| `activity` | 页面入口，协调 UI 与业务逻辑 | → adapter, data, algroithm, utils, view |
| `adapter` | ListView 数据绑定 | → data |
| `algroithm` | 图论算法（Dijkstra/A* 变形、DFS、物理时间计算） | → data |
| `data` | 数据加载、解析、缓存 | → data/json, data/parse |
| `data/json` | Gson POJO，与 JSON 字段一一对应 | 无 |
| `data/parse` | 解析后的运行时结构，存入 DataCache | 无 |
| `listener` | 接口定义 | 无 |
| `utils` | 通用工具（坐标转换、文本匹配、文件 I/O） | → data |
| `view` | 自定义 View（WebView 地图、Canvas 示意图） | → data |
| `widget` | 自定义控件 | 无 |

---

## 4. 架构设计

### 4.1 架构模式

采用**以 Activity 为中心的传统分层架构**（非 MVVM/MVP），核心设计模式：

| 模式 | 应用位置 | 说明 |
|:---|:---|:---|
| **模板方法** | `BaseActivity` | `onCreate` 中验证 DataCache 有效性 → 调用子类 `onDataReady()` |
| **单例** | `DataCache` | 静态内部类懒加载，线程安全；全局唯一数据访问点 |
| **委托** | `SubwayMapView` | WebView ↔ Java 通过 `@JavascriptInterface` 双向通信 |
| **策略** | `MetroGraph` / `SearchWeight` | 不同权重组合实现 9 种路径策略 |
| **生产者-消费者** | `MainActivity` | `ExecutorService` 单线程池执行路径搜索，`AtomicLong` queryId 取消过期任务 |

### 4.2 数据流

```
JSON/BMetro 文件
    │
    ▼
DataLoader.load()
    │  解压缩（.bmetro 为 GZIP 格式）
    ▼
DataParser.parse()
    │  Gson 反序列化 → MetroData
    │  遍历 lines/stations → 填充 DataCache 的各种 HashMap
    ▼
DataCache（全局单例）
    │  ├── getStationCoords()       → Map<String, double[]> 
    │  ├── getDirStations()         → Map<String, List<StationInfo>>
    │  ├── getTransferStations()    → Map<String, List<TransferLineInfo>>
    │  ├── getLineColorMap()        → Map<String, String>
    │  └── ...（共 30+ 个缓存 Map）
    │
    ▼
Activity / MetroGraph / SubwayMapView 等消费者
```

### 4.3 路径规划流程

```
用户选择起终点 + 策略
    │
    ▼
MainActivity → ExecutorService.submit()
    │  递增 queryId（AtomicLong）
    ▼
MetroGraph.findTransferPath(from, to, weights)
    │  Dijkstra/A* 变形：同线相邻站权值 = 0，换乘边权值 = SearchWeight.computeValue()
    │  DFS 随机路径 / 最长路径等其他策略
    ▼
PathResult
    │  pathNodes, transferCount, totalCost, fare
    ▼
MainActivity Handler → UI 更新
    │  展示 RouteDetailActivity / 地图绘制路径
    ▼
TimeCalculator.calculateTime() / FareCalculator.calculateFare()
```

### 4.4 混合渲染策略

| 场景 | 实现 | 文件 |
|:---|:---|:---|
| 全量城市地图（含缩放、平移、线路高亮） | WebView + D3.js SVG 渲染 | `SubwayMapView.java` + `assets/map.js` |
| 单条线路示意图（路线详情页） | Android Canvas 原生绘制 | `LineMapView.java` |

Java ↔ JavaScript 通信：`SubwayMapView.AndroidBridge` 通过 `@JavascriptInterface` 暴露方法，JS 通过 `Android.onStationLongClick(name, x, y)` 回调。

---

## 5. 关键类说明

### 5.1 算法层

**`MetroGraph`** — 核心图结构（约 1200+ 行）：
- `buildGraphs()` — 从 DataCache 构建换乘图和线路邻接表
- `findTransferPath(from, to, weights)` — 基于 Dijkstra 变体的最短路径搜索
- `findLongestPath(from, to)` — DFS 最长路径（观光/刷里程场景）
- `findRandomPath(from, to)` — DFS 随机路径
- 支持主线/支线、环线、快车线路、未来线路过滤

**`TrainTimeCalculator`** — 基于 Davis 阻力公式的物理模型时间计算：
- 区分 A/B/C/L/D 五种车型参数（质量、电机功率、最大速度等）
- 使用自适应辛普森积分计算加速段时间
- `computeSegmentTime(vehicleType, distance, maxSpeed, speedLimit)` 为核心入口

**`TimeCalculator`** — 旅行时间汇总：
- 列车行驶时间 + 换乘时间（含步行）+ 停站时间 + 进站时间 + 起点等待时间
- 拥挤因子从 `CrowdRule` 读入，修正行驶时间

**`FareCalculator`** — 多模式票价计算：
- 普通分段计价（`FareSegment` / `LinearExtension`）
- 特殊线路票价（`SpecialLineRule`：统一票价、附加费、线路组）
- 换乘优惠

### 5.2 数据层

**`DataCache`** — 全局单例，存储 30+ 个 HashMap：
- 站点坐标、方向站点列表、换乘关系、线路颜色/名称/城市
- 票价规则、运营参数、发车间隔、拥挤度、限速、虚拟换乘
- 关键方法均为 `synchronized` 保证线程安全

**`DataLoader`** — 多来源数据加载：
- Assets 内置数据（预置 6 个城市）
- 内部存储（用户导入的文件）
- 外部文件（Content URI）
- `.bmetro` 格式自动 GZIP 解压

### 5.3 UI 层

**`MainActivity`** — 主界面（约 1147 行，最大文件）：
- 起终点站点模糊搜索（AutoCompleteTextView + Levenshtein 编辑距离）
- 9 种路径策略选择
- 收藏路线管理（SharedPreferences + Gson）
- 步行建议与高德地图唤起

**`MapActivity`** — 交互式地铁地图：
- 线路高亮/筛选、站点长按设起终点、路径绘制
- 图例点击聚焦、空白处长按切换城市

---

## 6. 构建与运行

### 6.1 前置条件

- Android Studio Hedgehog (2023.1.1) 或更新版本
- JDK 11+
- Android SDK 35

### 6.2 Gradle 命令

```bash
# 编译 Debug APK
./gradlew assembleDebug

# 编译 Release APK
./gradlew assembleRelease

# 运行单元测试
./gradlew test

# 运行插桩测试
./gradlew connectedAndroidTest

# 清理
./gradlew clean
```

### 6.3 版本目录

依赖版本统一在 `gradle/libs.versions.toml` 中管理，模块 `app/build.gradle.kts` 通过 `libs.xxx` 引用。

---

## 7. 编码约定

### 7.1 JavaDoc

- **语言**：中文
- **必须添加注释的对象**：
  - 所有 `public` 类（类级别 Javadoc）
  - 所有 `public` / `protected` 方法（包含 `@param`、`@return`，必要时 `@see`）
  - 关键数据结构字段
- 复杂算法（如 Dijkstra 变体、Davis 公式积分）应在方法注释中简述思路

### 7.2 命名规范

| 类型 | 规范 | 示例 |
|:---|:---|:---|
| 包名 | 全小写，英文 | `algroithm`（注意历史拼写） |
| 类名 | PascalCase | `MetroGraph`, `MainActivity` |
| 方法/变量 | camelCase | `findTransferPath`, `queryId` |
| 常量 | UPPER_SNAKE_CASE | `MAX_SEARCH_RESULTS` |
| 布局文件 | `activity_` / `item_` 前缀 | `activity_main.xml`, `item_route_segment.xml` |
| 资源 ID | 小写 + 下划线 | `@id/start_station`, `@string/app_name` |

### 7.3 线程安全

- DataCache 的所有写操作用 `synchronized` 保护
- 路径搜索在 `ExecutorService` 单线程池中执行
- UI 更新通过 `Handler.post()` 回主线程
- 使用 `AtomicLong` 实现查询取消（queryId 机制）

### 7.4 注意事项

- `algroithm` 包名存在历史拼写错误（应为 `algorithm`），暂不修改以避免不必要的影响范围
- `.bmetro` 文件是 GZIP 压缩的 JSON，DataLoader 自动检测并解压
- WebView 地图使用 D3.js v7，加载在 `assets/map.html` 中完成

---

## 8. 预置数据

项目 `assets/map/` 中预置 6 个中国城市的 `.bmetro` 数据：

| 文件 | 城市 |
|:---|:---|
| `上海地铁.bmetro` | 上海 |
| `北京地铁.bmetro` | 北京 |
| `广州地铁.bmetro` | 广州 |
| `深圳地铁.bmetro` | 深圳 |
| `南京地铁.bmetro` | 南京 |
| `合肥地铁.bmetro` | 合肥 |

---

## 9. 扩展指南

### 添加新的路径策略

1. 在 `MetroGraph` 中实现搜索方法（如 `findNewStrategy(from, to)`）
2. 在 `MainActivity` 的策略选择列表中添加条目
3. 在 `strings.xml` 中添加对应的策略名称

### 添加新的数据字段

1. 在 `data/json/` 中添加或修改 Gson POJO 字段（含 `@SerializedName`）
2. 在 `DataParser.parse()` 中解析新字段并存入 `DataCache`
3. 在 `DataCache` 中添加对应的 getter 方法
4. 在 UI/算法层消费新数据

### 添加新城市数据

1. 在 Metro Editor 中编辑并编译生成 `.bmetro` 文件
2. 放入 `assets/map/` 目录
3. 或通过 App 内的文件管理功能导入

---

## 10. 相关资源

- [Metro Editor](https://gitee.com/embed2026/metro_editor) — 桌面端编辑器，本应用的数据源
- [D3.js](https://d3js.org/) — Web 端地图渲染库
- [Gson](https://github.com/google/gson) — JSON 解析库

---

> 文档最后更新：2026-08-02
