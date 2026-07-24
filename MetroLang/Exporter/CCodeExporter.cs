using System.IO;
using System.Text;

namespace MetroEditor.MetroLang.Exporter
{
    /// <summary>
    /// 嵌入式 C 代码生成器（小端模式，配合 MBINExporter 使用）
    /// </summary>
    public static class CCodeExporter
    {
        public static void ExportAll(byte[] data, string outputDir)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("数据为空", nameof(data));
            if (string.IsNullOrEmpty(outputDir))
                throw new ArgumentNullException(nameof(outputDir));

            Directory.CreateDirectory(outputDir);

            File.WriteAllText(
                Path.Combine(outputDir, "metro_parser.h"),
                GenerateParserHeader(),
                Encoding.UTF8
            );

            File.WriteAllText(
                Path.Combine(outputDir, "metro_parser.c"),
                GenerateParserSource(),
                Encoding.UTF8
            );

            File.WriteAllText(
                Path.Combine(outputDir, "metro_data.h"),
                GenerateDataHeader(data),
                Encoding.UTF8
            );
        }

        private static string GenerateParserHeader()
        {
            var sb = new StringBuilder();
            sb.AppendLine("/**");
            sb.AppendLine(" * @file metro_parser.h");
            sb.AppendLine(" * @brief Metro 路网数据解析器（小端）");
            sb.AppendLine(" * @details 所有多字节整数按小端存储，读取宏直接使用主机字节序。");
            sb.AppendLine($" * @date {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(" */");
            sb.AppendLine("#ifndef _METRO_PARSER_H_");
            sb.AppendLine("#define _METRO_PARSER_H_");
            sb.AppendLine("#include <stdint.h>");
            sb.AppendLine("#include <stdbool.h>");
            sb.AppendLine();
            sb.AppendLine("/** @defgroup Endian 小端读取宏（主机序） */");
            sb.AppendLine("/** @{ */");
            sb.AppendLine("/** 从指针 p 读取小端 uint16_t（直接取值） */");
            sb.AppendLine("#define MBIN_READ_U16(p) (*(const uint16_t*)(p))");
            sb.AppendLine("/** 从指针 p 读取小端 uint32_t（直接取值） */");
            sb.AppendLine("#define MBIN_READ_U32(p) (*(const uint32_t*)(p))");
            sb.AppendLine("/** 从指针 p 读取小端 int32_t（直接取值） */");
            sb.AppendLine("#define MBIN_READ_S32(p) (*(const int32_t*)(p))");
            sb.AppendLine("/** @} */");
            sb.AppendLine();

            sb.AppendLine("/** @defgroup Structs 二进制结构体定义 */");
            sb.AppendLine("/** @{ */");
            sb.AppendLine("#pragma pack(push, 1)");
            sb.AppendLine("/** 文件头结构（64 字节） */");
            sb.AppendLine("typedef struct {");
            sb.AppendLine("    uint32_t magic;               /**< 'MBIN' 魔数 */");
            sb.AppendLine("    uint8_t  version;             /**< 版本号 */");
            sb.AppendLine("    uint8_t  endian;              /**< 0x00 小端 */");
            sb.AppendLine("    uint8_t  reserved0[2];");
            sb.AppendLine("    uint32_t str_offset;          /**< 字符串池偏移 */");
            sb.AppendLine("    uint32_t line_offset;         /**< 线路表偏移 */");
            sb.AppendLine("    uint32_t station_meta_offset; /**< 站点元数据表偏移 */");
            sb.AppendLine("    uint32_t node_offset;         /**< 节点表偏移 */");
            sb.AppendLine("    uint32_t rail_edge_offset;    /**< 运行边表偏移 */");
            sb.AppendLine("    uint32_t transfer_edge_offset;/**< 换乘边表偏移 */");
            sb.AppendLine("    uint32_t lookup_offset;       /**< 查找表偏移 */");
            sb.AppendLine("    uint32_t fare_offset;         /**< 票价分段表偏移 */");
            sb.AppendLine("    uint32_t total_size;          /**< 文件总大小 */");
            sb.AppendLine("    uint16_t line_count;          /**< 线路数量 */");
            sb.AppendLine("    uint16_t station_count;       /**< 站点数量 */");
            sb.AppendLine("    uint16_t node_count;          /**< 节点数量 */");
            sb.AppendLine("    uint16_t rail_edge_count;     /**< 运行边数量 */");
            sb.AppendLine("    uint16_t transfer_edge_count; /**< 换乘边数量 */");
            sb.AppendLine("    uint16_t lookup_count;        /**< 查找表条目数 */");
            sb.AppendLine("    uint16_t fare_count;          /**< 票价分段数 */");
            sb.AppendLine("    uint8_t  padding[10];        /**< 语言表信息存储在此 */");
            sb.AppendLine("} MetroHeader;");
            sb.AppendLine("/** 线路条目（32 字节） */");
            sb.AppendLine("typedef struct { uint32_t name_idx; uint32_t color_rgb; uint16_t max_speed; uint16_t open_year; uint32_t reserved0[4]; } LineEntry;");
            sb.AppendLine("/** 站点元数据（20 字节，注意 name_idx 为 4 字节） */");
            sb.AppendLine("typedef struct {");
            sb.AppendLine("    uint32_t name_idx;            /**< 名称在字符串池中的偏移（4 字节） */");
            sb.AppendLine("    int32_t  lon;                 /**< 经度 × 1e6 */");
            sb.AppendLine("    int32_t  lat;                 /**< 纬度 × 1e6 */");
            sb.AppendLine("    uint32_t reserved0;           /**< 保留 */");
            sb.AppendLine("    uint32_t reserved1;           /**< 保留 */");
            sb.AppendLine("} StationMeta;");
            sb.AppendLine("/** 节点条目（4 字节） */");
            sb.AppendLine("typedef struct { uint16_t display_line_idx; uint16_t station_idx; } NodeEntry;");
            sb.AppendLine("/** 运行边（10 字节） */");
            sb.AppendLine("typedef struct { uint16_t from; uint16_t to; uint32_t dist_cm; uint16_t time_sec; } RailEdge;");
            sb.AppendLine("/** 换乘边（6 字节） */");
            sb.AppendLine("typedef struct { uint16_t from; uint16_t to; uint16_t time_sec; } TransferEdge;");
            sb.AppendLine("/** 票价分段（12 字节） */");
            sb.AppendLine("typedef struct { uint32_t start_cm; uint32_t end_cm; uint32_t price_fen; } FareSegment;");
            sb.AppendLine("#pragma pack(pop)");
            sb.AppendLine("/** @} */");
            sb.AppendLine();

            sb.AppendLine("/** 解析器上下文结构 */");
            sb.AppendLine("typedef struct {");
            sb.AppendLine("    const uint8_t* base;          /**< 数据基址 */");
            sb.AppendLine("    const MetroHeader* hdr;       /**< 文件头指针 */");
            sb.AppendLine("    const char* str_pool;         /**< 字符串池指针（GBK 编码） */");
            sb.AppendLine("    const LineEntry* lines;       /**< 线路表指针 */");
            sb.AppendLine("    const StationMeta* stations;  /**< 站点表指针 */");
            sb.AppendLine("    const NodeEntry* nodes;       /**< 节点表指针 */");
            sb.AppendLine("    const RailEdge* rail_edges;   /**< 运行边表指针 */");
            sb.AppendLine("    const TransferEdge* transfer_edges; /**< 换乘边表指针 */");
            sb.AppendLine("    const FareSegment* fares;     /**< 票价表指针 */");
            sb.AppendLine("    const uint32_t* lang_table;   /**< 语言表偏移数组 */");
            sb.AppendLine("    uint16_t lang_count;          /**< 语言数量 */");
            sb.AppendLine("} MetroContext;");
            sb.AppendLine();

            sb.AppendLine("/** @defgroup API 公共 API */");
            sb.AppendLine("/** @{ */");
            sb.AppendLine("/**");
            sb.AppendLine(" * @brief 初始化解析器上下文");
            sb.AppendLine(" * @param ctx  上下文指针");
            sb.AppendLine(" * @param data 二进制数据指针");
            sb.AppendLine(" * @note 所有多字节数据直接按小端解释，适合 x86/ARM 等小端平台");
            sb.AppendLine(" */");
            sb.AppendLine("void metro_init(MetroContext* ctx, const uint8_t* data);");

            sb.AppendLine("/** @name 线路查询 */");
            sb.AppendLine("/** @{ */");
            sb.AppendLine("/** 获取线路总数 */");
            sb.AppendLine("uint16_t metro_line_count(const MetroContext* ctx);");
            sb.AppendLine("/** 获取线路名称（GBK 编码） */");
            sb.AppendLine("const char* metro_get_line_name(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine("/** 获取线路颜色值 (0xRRGGBB) */");
            sb.AppendLine("uint32_t metro_get_line_color(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine("/** 获取线路最高速度 (km/h) */");
            sb.AppendLine("uint16_t metro_get_line_max_speed(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine("/** @} */");

            sb.AppendLine("/** @name 站点查询 */");
            sb.AppendLine("/** @{ */");
            sb.AppendLine("/** 获取站点总数 */");
            sb.AppendLine("uint16_t metro_station_count(const MetroContext* ctx);");
            sb.AppendLine("/** 获取站点名称（GBK 编码） */");
            sb.AppendLine("const char* metro_get_station_name(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine("/** 获取站点坐标（经纬度 × 1e6） */");
            sb.AppendLine("void metro_get_station_pos(const MetroContext* ctx, uint16_t idx, int32_t* lon, int32_t* lat);");
            sb.AppendLine("/** @} */");

            sb.AppendLine("/** @name 节点查询 */");
            sb.AppendLine("/** @{ */");
            sb.AppendLine("/** 获取节点总数 */");
            sb.AppendLine("uint16_t metro_node_count(const MetroContext* ctx);");
            sb.AppendLine("/** 获取节点所属站点索引 */");
            sb.AppendLine("uint16_t metro_get_node_station_idx(const MetroContext* ctx, uint16_t node_id);");
            sb.AppendLine("/** 获取节点所属显示线路索引 */");
            sb.AppendLine("uint16_t metro_get_node_line_idx(const MetroContext* ctx, uint16_t node_id);");
            sb.AppendLine("/** @} */");

            sb.AppendLine("/** @name 边查询 */");
            sb.AppendLine("/** @{ */");
            sb.AppendLine("/** 获取运行边总数 */");
            sb.AppendLine("uint16_t metro_rail_edge_count(const MetroContext* ctx);");
            sb.AppendLine("/** 获取指定运行边指针 */");
            sb.AppendLine("const RailEdge* metro_get_rail_edge(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine("/** 获取换乘边总数 */");
            sb.AppendLine("uint16_t metro_transfer_edge_count(const MetroContext* ctx);");
            sb.AppendLine("/** 获取指定换乘边指针 */");
            sb.AppendLine("const TransferEdge* metro_get_transfer_edge(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine("/** @} */");

            sb.AppendLine("/** @name 查找表 */");
            sb.AppendLine("/** @{ */");
            sb.AppendLine("/** 通过站点索引获取该站对应的所有节点列表 */");
            sb.AppendLine("/** @param ctx 上下文指针 */");
            sb.AppendLine("/** @param station_idx 站点索引 */");
            sb.AppendLine("/** @param[out] out_nodes 返回节点数组指针 */");
            sb.AppendLine("/** @return 节点数量，若为0表示未找到 */");
            sb.AppendLine("uint16_t metro_find_nodes_by_station(const MetroContext* ctx, uint16_t station_idx, const uint16_t** out_nodes);");
            sb.AppendLine("/** @} */");

            sb.AppendLine("/** @name 票价计算 */");
            sb.AppendLine("/** @{ */");
            sb.AppendLine("/** 根据里程（厘米）计算票价（分） */");
            sb.AppendLine("/** @param ctx 上下文指针 */");
            sb.AppendLine("/** @param dist_cm 里程（厘米） */");
            sb.AppendLine("/** @return 票价（分），若未找到分段则返回默认 200 分 */");
            sb.AppendLine("uint32_t metro_calc_fare(const MetroContext* ctx, uint32_t dist_cm);");
            sb.AppendLine("/** @} */");

            sb.AppendLine("/** @name 语言表查询 */");
            sb.AppendLine("/** @{ */");
            sb.AppendLine("/** 获取语言数量 */");
            sb.AppendLine("uint16_t metro_language_count(const MetroContext* ctx);");
            sb.AppendLine("/** 获取指定语言代码（如 \"zh\", \"en\"） */");
            sb.AppendLine("/** @param ctx 上下文指针 */");
            sb.AppendLine("/** @param idx 语言索引（0 ~ language_count-1） */");
            sb.AppendLine("/** @return 语言代码字符串（GBK 编码），若 idx 无效返回 NULL */");
            sb.AppendLine("const char* metro_get_language_code(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine("/** @} */");

            sb.AppendLine("/** @name 路径规划（Dijkstra） */");
            sb.AppendLine("/** @{ */");
            sb.AppendLine("#define METRO_MAX_NODES 2048");
            sb.AppendLine("#define METRO_INF_TIME 65535");
            sb.AppendLine("/**");
            sb.AppendLine(" * @brief 使用 Dijkstra 算法查找最短时间路径");
            sb.AppendLine(" * @param ctx 上下文指针");
            sb.AppendLine(" * @param start_node 起点节点 ID");
            sb.AppendLine(" * @param end_node 终点节点 ID");
            sb.AppendLine(" * @param[out] out_path 输出路径节点 ID 数组（需预先分配）");
            sb.AppendLine(" * @param max_path_len out_path 的最大长度");
            sb.AppendLine(" * @return 路径节点数（包含起点和终点），若无法到达返回 0");
            sb.AppendLine(" */");
            sb.AppendLine("uint16_t metro_find_path(const MetroContext* ctx, uint16_t start_node, uint16_t end_node, uint16_t* out_path, uint16_t max_path_len);");
            sb.AppendLine("/**");
            sb.AppendLine(" * @brief 计算路径的总时间");
            sb.AppendLine(" * @param ctx 上下文指针");
            sb.AppendLine(" * @param path 路径节点数组");
            sb.AppendLine(" * @param path_len 路径长度");
            sb.AppendLine(" * @return 总时间（秒）");
            sb.AppendLine(" */");
            sb.AppendLine("uint32_t metro_path_total_time(const MetroContext* ctx, const uint16_t* path, uint16_t path_len);");
            sb.AppendLine("/**");
            sb.AppendLine(" * @brief 计算路径的总距离");
            sb.AppendLine(" * @param ctx 上下文指针");
            sb.AppendLine(" * @param path 路径节点数组");
            sb.AppendLine(" * @param path_len 路径长度");
            sb.AppendLine(" * @return 总距离（厘米）");
            sb.AppendLine(" */");
            sb.AppendLine("uint32_t metro_path_total_distance(const MetroContext* ctx, const uint16_t* path, uint16_t path_len);");
            sb.AppendLine("/** @} */");

            sb.AppendLine("/** @} */");
            sb.AppendLine("#endif");
            return sb.ToString();
        }

        private static string GenerateParserSource()
        {
            var sb = new StringBuilder();
            sb.AppendLine("/**");
            sb.AppendLine(" * @file metro_parser.c");
            sb.AppendLine(" * @brief Metro 路网数据解析器实现（小端）");
            sb.AppendLine($" * @date {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(" */");
            sb.AppendLine("#include \"metro_parser.h\"");
            sb.AppendLine("#include <string.h>");
            sb.AppendLine();

            // 初始化
            sb.AppendLine("/** @brief 初始化解析器上下文 */");
            sb.AppendLine("void metro_init(MetroContext* ctx, const uint8_t* data)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || !data) return;");
            sb.AppendLine("    ctx->base = data;");
            sb.AppendLine("    ctx->hdr = (const MetroHeader*)data;");
            sb.AppendLine("    ctx->str_pool = (const char*)(data + MBIN_READ_U32(&ctx->hdr->str_offset));");
            sb.AppendLine("    ctx->lines = (const LineEntry*)(data + MBIN_READ_U32(&ctx->hdr->line_offset));");
            sb.AppendLine("    ctx->stations = (const StationMeta*)(data + MBIN_READ_U32(&ctx->hdr->station_meta_offset));");
            sb.AppendLine("    ctx->nodes = (const NodeEntry*)(data + MBIN_READ_U32(&ctx->hdr->node_offset));");
            sb.AppendLine("    ctx->rail_edges = (const RailEdge*)(data + MBIN_READ_U32(&ctx->hdr->rail_edge_offset));");
            sb.AppendLine("    ctx->transfer_edges = (const TransferEdge*)(data + MBIN_READ_U32(&ctx->hdr->transfer_edge_offset));");
            sb.AppendLine("    ctx->fares = (const FareSegment*)(data + MBIN_READ_U32(&ctx->hdr->fare_offset));");
            sb.AppendLine("    uint32_t lang_off = MBIN_READ_U32(&ctx->hdr->padding[0]);");
            sb.AppendLine("    uint32_t lang_size = MBIN_READ_U32(&ctx->hdr->padding[4]);");
            sb.AppendLine("    ctx->lang_count = MBIN_READ_U16(&ctx->hdr->padding[8]);");
            sb.AppendLine("    if (lang_off && lang_size && ctx->lang_count)");
            sb.AppendLine("        ctx->lang_table = (const uint32_t*)(data + lang_off);");
            sb.AppendLine("    else");
            sb.AppendLine("        ctx->lang_table = NULL;");
            sb.AppendLine("}");
            sb.AppendLine();

            // 所有查询函数
            sb.AppendLine("/** @brief 获取线路总数 */");
            sb.AppendLine("uint16_t metro_line_count(const MetroContext* ctx) { return ctx->hdr ? MBIN_READ_U16(&ctx->hdr->line_count) : 0; }");
            sb.AppendLine("/** @brief 获取线路名称 */");
            sb.AppendLine("const char* metro_get_line_name(const MetroContext* ctx, uint16_t idx) { if (!ctx || idx >= metro_line_count(ctx)) return NULL; const LineEntry* line = &ctx->lines[idx]; return ctx->str_pool + MBIN_READ_U32(&line->name_idx); }");
            sb.AppendLine("/** @brief 获取线路颜色 */");
            sb.AppendLine("uint32_t metro_get_line_color(const MetroContext* ctx, uint16_t idx) { if (!ctx || idx >= metro_line_count(ctx)) return 0xCCCCCC; return MBIN_READ_U32(&ctx->lines[idx].color_rgb); }");
            sb.AppendLine("/** @brief 获取线路最高速度 */");
            sb.AppendLine("uint16_t metro_get_line_max_speed(const MetroContext* ctx, uint16_t idx) { if (!ctx || idx >= metro_line_count(ctx)) return 0; return MBIN_READ_U16(&ctx->lines[idx].max_speed); }");
            sb.AppendLine("/** @brief 获取站点总数 */");
            sb.AppendLine("uint16_t metro_station_count(const MetroContext* ctx) { return ctx->hdr ? MBIN_READ_U16(&ctx->hdr->station_count) : 0; }");
            sb.AppendLine("/** @brief 获取站点名称（使用 4 字节偏移） */");
            sb.AppendLine("const char* metro_get_station_name(const MetroContext* ctx, uint16_t idx) { if (!ctx || idx >= metro_station_count(ctx)) return NULL; const StationMeta* st = &ctx->stations[idx]; return ctx->str_pool + MBIN_READ_U32(&st->name_idx); }");
            sb.AppendLine("/** @brief 获取站点坐标 */");
            sb.AppendLine("void metro_get_station_pos(const MetroContext* ctx, uint16_t idx, int32_t* lon, int32_t* lat) { if (!ctx || idx >= metro_station_count(ctx)) { if (lon) *lon=0; if (lat) *lat=0; return; } const StationMeta* st = &ctx->stations[idx]; if (lon) *lon = MBIN_READ_S32(&st->lon); if (lat) *lat = MBIN_READ_S32(&st->lat); }");
            sb.AppendLine("/** @brief 获取节点总数 */");
            sb.AppendLine("uint16_t metro_node_count(const MetroContext* ctx) { return ctx->hdr ? MBIN_READ_U16(&ctx->hdr->node_count) : 0; }");
            sb.AppendLine("/** @brief 获取节点站点索引 */");
            sb.AppendLine("uint16_t metro_get_node_station_idx(const MetroContext* ctx, uint16_t node_id) { if (!ctx || node_id >= metro_node_count(ctx)) return 0xFFFF; return MBIN_READ_U16(&ctx->nodes[node_id].station_idx); }");
            sb.AppendLine("/** @brief 获取节点线路索引 */");
            sb.AppendLine("uint16_t metro_get_node_line_idx(const MetroContext* ctx, uint16_t node_id) { if (!ctx || node_id >= metro_node_count(ctx)) return 0xFFFF; return MBIN_READ_U16(&ctx->nodes[node_id].display_line_idx); }");
            sb.AppendLine("/** @brief 获取运行边总数 */");
            sb.AppendLine("uint16_t metro_rail_edge_count(const MetroContext* ctx) { return ctx->hdr ? MBIN_READ_U16(&ctx->hdr->rail_edge_count) : 0; }");
            sb.AppendLine("/** @brief 获取运行边指针 */");
            sb.AppendLine("const RailEdge* metro_get_rail_edge(const MetroContext* ctx, uint16_t idx) { if (!ctx || idx >= metro_rail_edge_count(ctx)) return NULL; return &ctx->rail_edges[idx]; }");
            sb.AppendLine("/** @brief 获取换乘边总数 */");
            sb.AppendLine("uint16_t metro_transfer_edge_count(const MetroContext* ctx) { return ctx->hdr ? MBIN_READ_U16(&ctx->hdr->transfer_edge_count) : 0; }");
            sb.AppendLine("/** @brief 获取换乘边指针 */");
            sb.AppendLine("const TransferEdge* metro_get_transfer_edge(const MetroContext* ctx, uint16_t idx) { if (!ctx || idx >= metro_transfer_edge_count(ctx)) return NULL; return &ctx->transfer_edges[idx]; }");

            // 查找表
            sb.AppendLine("/** @brief 通过站点索引查找节点列表 */");
            sb.AppendLine("uint16_t metro_find_nodes_by_station(const MetroContext* ctx, uint16_t station_idx, const uint16_t** out_nodes)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || !out_nodes) return 0;");
            sb.AppendLine("    const uint8_t* ptr = ctx->base + MBIN_READ_U32(&ctx->hdr->lookup_offset);");
            sb.AppendLine("    uint16_t count = MBIN_READ_U16(ptr); ptr += 2;");
            sb.AppendLine("    for (uint16_t i = 0; i < count; i++) {");
            sb.AppendLine("        uint16_t st_idx = MBIN_READ_U16(ptr);");
            sb.AppendLine("        uint16_t node_cnt = MBIN_READ_U16(ptr + 2);");
            sb.AppendLine("        ptr += 4;");
            sb.AppendLine("        if (st_idx == station_idx) { *out_nodes = (const uint16_t*)ptr; return node_cnt; }");
            sb.AppendLine("        ptr += node_cnt * 2;");
            sb.AppendLine("    }");
            sb.AppendLine("    *out_nodes = NULL; return 0;");
            sb.AppendLine("}");

            // 票价
            sb.AppendLine("/** @brief 计算票价 */");
            sb.AppendLine("uint32_t metro_calc_fare(const MetroContext* ctx, uint32_t dist_cm)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx) return 200;");
            sb.AppendLine("    uint16_t count = MBIN_READ_U16(&ctx->hdr->fare_count);");
            sb.AppendLine("    for (uint16_t i = 0; i < count; i++) {");
            sb.AppendLine("        uint32_t start = MBIN_READ_U32(&ctx->fares[i].start_cm);");
            sb.AppendLine("        uint32_t end = MBIN_READ_U32(&ctx->fares[i].end_cm);");
            sb.AppendLine("        if (dist_cm >= start && dist_cm < end) return MBIN_READ_U32(&ctx->fares[i].price_fen);");
            sb.AppendLine("    }");
            sb.AppendLine("    return 200;");
            sb.AppendLine("}");

            // 语言表
            sb.AppendLine("/** @brief 获取语言数量 */");
            sb.AppendLine("uint16_t metro_language_count(const MetroContext* ctx) { return ctx ? ctx->lang_count : 0; }");
            sb.AppendLine("/** @brief 获取语言代码 */");
            sb.AppendLine("const char* metro_get_language_code(const MetroContext* ctx, uint16_t idx) { if (!ctx || !ctx->lang_table || idx >= ctx->lang_count) return NULL; uint32_t off = MBIN_READ_U32(&ctx->lang_table[idx]); return off ? ctx->str_pool + off : NULL; }");

            // Dijkstra
            sb.AppendLine("/** @brief Dijkstra 最短路径 */");
            sb.AppendLine("uint16_t metro_find_path(const MetroContext* ctx, uint16_t start_node, uint16_t end_node, uint16_t* out_path, uint16_t max_path_len)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || !out_path || start_node == end_node) return 0;");
            sb.AppendLine("    uint16_t n = metro_node_count(ctx); if (n > METRO_MAX_NODES) n = METRO_MAX_NODES;");
            sb.AppendLine("    uint16_t dist[METRO_MAX_NODES], prev[METRO_MAX_NODES]; bool visited[METRO_MAX_NODES];");
            sb.AppendLine("    for (uint16_t i = 0; i < n; i++) { dist[i] = METRO_INF_TIME; prev[i] = 0xFFFF; visited[i] = false; }");
            sb.AppendLine("    dist[start_node] = 0;");
            sb.AppendLine("    for (uint16_t k = 0; k < n; k++) {");
            sb.AppendLine("        uint16_t u = 0xFFFF, min_d = METRO_INF_TIME;");
            sb.AppendLine("        for (uint16_t i = 0; i < n; i++) if (!visited[i] && dist[i] < min_d) { min_d = dist[i]; u = i; }");
            sb.AppendLine("        if (u == 0xFFFF || u == end_node) break; visited[u] = true;");
            sb.AppendLine("        uint16_t rail_cnt = metro_rail_edge_count(ctx);");
            sb.AppendLine("        for (uint16_t i = 0; i < rail_cnt; i++) { const RailEdge* e = metro_get_rail_edge(ctx, i); uint16_t from = MBIN_READ_U16(&e->from); if (from != u) continue; uint16_t to = MBIN_READ_U16(&e->to); uint16_t w = MBIN_READ_U16(&e->time_sec); if (dist[u] + w < dist[to]) { dist[to] = dist[u] + w; prev[to] = u; } }");
            sb.AppendLine("        uint16_t trans_cnt = metro_transfer_edge_count(ctx);");
            sb.AppendLine("        for (uint16_t i = 0; i < trans_cnt; i++) { const TransferEdge* e = metro_get_transfer_edge(ctx, i); uint16_t from = MBIN_READ_U16(&e->from); if (from != u) continue; uint16_t to = MBIN_READ_U16(&e->to); uint16_t w = MBIN_READ_U16(&e->time_sec); if (dist[u] + w < dist[to]) { dist[to] = dist[u] + w; prev[to] = u; } }");
            sb.AppendLine("    }");
            sb.AppendLine("    if (dist[end_node] == METRO_INF_TIME) return 0;");
            sb.AppendLine("    uint16_t path_len = 0, cur = end_node;");
            sb.AppendLine("    while (cur != 0xFFFF && cur != start_node && path_len < max_path_len) { out_path[path_len++] = cur; cur = prev[cur]; }");
            sb.AppendLine("    if (cur == start_node) out_path[path_len++] = start_node;");
            sb.AppendLine("    for (uint16_t i = 0; i < path_len / 2; i++) { uint16_t tmp = out_path[i]; out_path[i] = out_path[path_len - 1 - i]; out_path[path_len - 1 - i] = tmp; }");
            sb.AppendLine("    return path_len;");
            sb.AppendLine("}");

            sb.AppendLine("/** @brief 计算路径总时间 */");
            sb.AppendLine("uint32_t metro_path_total_time(const MetroContext* ctx, const uint16_t* path, uint16_t path_len)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || !path || path_len < 2) return 0;");
            sb.AppendLine("    uint32_t total = 0;");
            sb.AppendLine("    for (uint16_t i = 0; i < path_len - 1; i++) {");
            sb.AppendLine("        uint16_t from = path[i], to = path[i+1]; bool found = false;");
            sb.AppendLine("        uint16_t rail_cnt = metro_rail_edge_count(ctx);");
            sb.AppendLine("        for (uint16_t j = 0; j < rail_cnt; j++) { const RailEdge* e = metro_get_rail_edge(ctx, j); if (MBIN_READ_U16(&e->from) == from && MBIN_READ_U16(&e->to) == to) { total += MBIN_READ_U16(&e->time_sec); found = true; break; } }");
            sb.AppendLine("        if (found) continue;");
            sb.AppendLine("        uint16_t trans_cnt = metro_transfer_edge_count(ctx);");
            sb.AppendLine("        for (uint16_t j = 0; j < trans_cnt; j++) { const TransferEdge* e = metro_get_transfer_edge(ctx, j); if (MBIN_READ_U16(&e->from) == from && MBIN_READ_U16(&e->to) == to) { total += MBIN_READ_U16(&e->time_sec); break; } }");
            sb.AppendLine("    }");
            sb.AppendLine("    return total;");
            sb.AppendLine("}");

            sb.AppendLine("/** @brief 计算路径总距离 */");
            sb.AppendLine("uint32_t metro_path_total_distance(const MetroContext* ctx, const uint16_t* path, uint16_t path_len)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || !path || path_len < 2) return 0;");
            sb.AppendLine("    uint32_t total = 0;");
            sb.AppendLine("    for (uint16_t i = 0; i < path_len - 1; i++) {");
            sb.AppendLine("        uint16_t from = path[i], to = path[i+1];");
            sb.AppendLine("        uint16_t rail_cnt = metro_rail_edge_count(ctx);");
            sb.AppendLine("        for (uint16_t j = 0; j < rail_cnt; j++) { const RailEdge* e = metro_get_rail_edge(ctx, j); if (MBIN_READ_U16(&e->from) == from && MBIN_READ_U16(&e->to) == to) { total += MBIN_READ_U32(&e->dist_cm); break; } }");
            sb.AppendLine("    }");
            sb.AppendLine("    return total;");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateDataHeader(byte[] data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("/**");
            sb.AppendLine(" * @file metro_data.h");
            sb.AppendLine(" * @brief 路网数据（小端 MBIN）");
            sb.AppendLine($" * @date {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(" */");
            sb.AppendLine("#ifndef _METRO_DATA_H_");
            sb.AppendLine("#define _METRO_DATA_H_");
            sb.AppendLine("#include <stdint.h>");
            sb.AppendLine("/** 数据总大小（字节） */");
            sb.AppendLine($"#define METRO_DATA_SIZE ({data.Length})");
            sb.AppendLine("/** 地铁路网数据数组（小端格式） */");
            sb.AppendLine($"static const uint8_t metro_data[{data.Length}] = {{");
            for (int i = 0; i < data.Length; i++)
            {
                if (i % 16 == 0) sb.Append("    ");
                sb.Append($"0x{data[i]:X2}");
                if (i < data.Length - 1) sb.Append(", ");
                if ((i + 1) % 16 == 0 && i < data.Length - 1) sb.AppendLine();
            }
            if (data.Length % 16 != 0) sb.AppendLine();
            sb.AppendLine("};");
            sb.AppendLine("#endif");
            return sb.ToString();
        }
    }
}