using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MetroEditor.MetroLang.Exporter
{
    /// <summary>
    /// 嵌入式 C 代码生成器
    /// 生成三个文件：metro_parser.h, metro_parser.c, metro_data.h
    /// </summary>
    public static class CCodeExporter
    {
        /// <summary>
        /// 生成完整的嵌入式 C 代码文件（三个文件）
        /// </summary>
        public static void ExportAll(byte[] data, string outputDir, string city = "")
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("数据为空", nameof(data));

            if (string.IsNullOrEmpty(outputDir))
                throw new ArgumentNullException(nameof(outputDir));

            Directory.CreateDirectory(outputDir);

            string cityUpper = (city ?? "DEFAULT").ToUpperInvariant();
            string cityName = string.IsNullOrEmpty(city) ? "DEFAULT" : city;

            // 生成三个文件
            File.WriteAllText(
                Path.Combine(outputDir, "metro_parser.h"),
                GenerateParserHeader(cityUpper, cityName),
                Encoding.UTF8
            );

            File.WriteAllText(
                Path.Combine(outputDir, "metro_parser.c"),
                GenerateParserSource(cityUpper, cityName),
                Encoding.UTF8
            );

            File.WriteAllText(
                Path.Combine(outputDir, "metro_data.h"),
                GenerateDataHeader(data, cityUpper, cityName),
                Encoding.UTF8
            );
        }

        /// <summary>
        /// 生成 metro_parser.h
        /// </summary>
        private static string GenerateParserHeader(string cityUpper, string cityName)
        {
            var sb = new StringBuilder();

            // ---------- 文件头 ----------
            sb.AppendLine("/*");
            sb.AppendLine(" * metro_parser.h - Metro 路网数据解析器");
            sb.AppendLine(" * 通用解析器头文件，可与不同的 metro_data.h 配合使用");
            if (!string.IsNullOrEmpty(cityName))
                sb.AppendLine($" * 城市: {cityName}");
            sb.AppendLine($" * 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(" */");
            sb.AppendLine();

            string guardName = $"_METRO_PARSER_H_";
            sb.AppendLine($"#ifndef {guardName}");
            sb.AppendLine($"#define {guardName}");
            sb.AppendLine();

            // ---------- 包含 ----------
            sb.AppendLine("#include <stdint.h>");
            sb.AppendLine("#include <stdbool.h>");
            sb.AppendLine();

            // ---------- 大端读取宏 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 大端读取宏（所有多字节整数按大端存储）");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("#define MBIN_READ_U16(p) ((uint16_t)(((const uint8_t*)(p))[0] << 8 | ((const uint8_t*)(p))[1]))");
            sb.AppendLine("#define MBIN_READ_U32(p) ((uint32_t)(((const uint8_t*)(p))[0] << 24 | ((const uint8_t*)(p))[1] << 16 | \\");
            sb.AppendLine("                                ((const uint8_t*)(p))[2] << 8 | ((const uint8_t*)(p))[3]))");
            sb.AppendLine("#define MBIN_READ_S32(p) ((int32_t)MBIN_READ_U32(p))");
            sb.AppendLine();

            // ---------- 结构体定义 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 结构体定义（与 MBIN 二进制格式严格对齐）");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("#pragma pack(push, 1)");
            sb.AppendLine();

            // 文件头（64字节）
            sb.AppendLine("/* 文件头（64 字节） */");
            sb.AppendLine("typedef struct {");
            sb.AppendLine("    uint32_t magic;               // 'MBIN'");
            sb.AppendLine("    uint8_t  version;             // 0x02");
            sb.AppendLine("    uint8_t  endian;              // 0x01 = Big-Endian");
            sb.AppendLine("    uint8_t  reserved0[2];");
            sb.AppendLine("    uint32_t str_offset;          // 字符串池偏移");
            sb.AppendLine("    uint32_t line_offset;         // 线路表偏移");
            sb.AppendLine("    uint32_t station_meta_offset; // 站点元数据表偏移");
            sb.AppendLine("    uint32_t node_offset;         // 节点表偏移");
            sb.AppendLine("    uint32_t rail_edge_offset;    // 运行边表偏移");
            sb.AppendLine("    uint32_t transfer_edge_offset;// 换乘边表偏移");
            sb.AppendLine("    uint32_t lookup_offset;       // 查找表偏移");
            sb.AppendLine("    uint32_t fare_offset;         // 票价分段表偏移");
            sb.AppendLine("    uint32_t total_size;          // 总大小");
            sb.AppendLine("    uint16_t line_count;          // 显示线路数量");
            sb.AppendLine("    uint16_t station_count;       // 站点数量");
            sb.AppendLine("    uint16_t node_count;          // 节点数量");
            sb.AppendLine("    uint16_t rail_edge_count;     // 运行边数量");
            sb.AppendLine("    uint16_t transfer_edge_count; // 换乘边数量");
            sb.AppendLine("    uint16_t lookup_count;        // 查找表条目数");
            sb.AppendLine("    uint16_t fare_count;          // 票价分段数");
            sb.AppendLine("    uint8_t  padding[10];");
            sb.AppendLine("} MetroHeader;");
            sb.AppendLine();

            // 线路表（32字节）
            sb.AppendLine("/* 线路条目（32 字节） */");
            sb.AppendLine("typedef struct {");
            sb.AppendLine("    uint32_t name_idx;            // 字符串池偏移");
            sb.AppendLine("    uint32_t color_rgb;           // 0xRRGGBB");
            sb.AppendLine("    uint16_t max_speed;           // km/h");
            sb.AppendLine("    uint16_t open_year;           // 开通年份");
            sb.AppendLine("    uint32_t reserved0[4];");
            sb.AppendLine("} LineEntry;");
            sb.AppendLine();

            // 站点元数据（24字节）
            sb.AppendLine("/* 站点元数据（24 字节） */");
            sb.AppendLine("typedef struct {");
            sb.AppendLine("    uint16_t name_idx;            // 字符串池偏移");
            sb.AppendLine("    int32_t  lon;                 // 经度 × 1e6");
            sb.AppendLine("    int32_t  lat;                 // 纬度 × 1e6");
            sb.AppendLine("    uint16_t reserved0;");
            sb.AppendLine("    uint32_t reserved1[2];");
            sb.AppendLine("} StationMeta;");
            sb.AppendLine();

            // 节点表（4字节）
            sb.AppendLine("/* 节点条目（4 字节） */");
            sb.AppendLine("typedef struct {");
            sb.AppendLine("    uint16_t display_line_idx;    // 显示线路索引");
            sb.AppendLine("    uint16_t station_idx;         // 站点索引");
            sb.AppendLine("} NodeEntry;");
            sb.AppendLine();

            // 运行边（12字节）
            sb.AppendLine("/* 运行边（12 字节） */");
            sb.AppendLine("typedef struct {");
            sb.AppendLine("    uint16_t from;                // 起点节点 ID");
            sb.AppendLine("    uint16_t to;                  // 终点节点 ID");
            sb.AppendLine("    uint32_t dist_cm;             // 距离（厘米）");
            sb.AppendLine("    uint16_t time_sec;            // 预计算运行时间（秒）");
            sb.AppendLine("} RailEdge;");
            sb.AppendLine();

            // 换乘边（8字节）
            sb.AppendLine("/* 换乘边（8 字节） */");
            sb.AppendLine("typedef struct {");
            sb.AppendLine("    uint16_t from;                // 起点节点 ID");
            sb.AppendLine("    uint16_t to;                  // 终点节点 ID");
            sb.AppendLine("    uint16_t time_sec;            // 预计算换乘时间（秒）");
            sb.AppendLine("} TransferEdge;");
            sb.AppendLine();

            // 票价分段（12字节）
            sb.AppendLine("/* 票价分段（12 字节） */");
            sb.AppendLine("typedef struct {");
            sb.AppendLine("    uint32_t start_cm;            // 起始里程（厘米）");
            sb.AppendLine("    uint32_t end_cm;              // 结束里程（厘米）");
            sb.AppendLine("    uint32_t price_fen;           // 票价（分）");
            sb.AppendLine("} FareSegment;");
            sb.AppendLine();

            sb.AppendLine("#pragma pack(pop)");
            sb.AppendLine();

            // ---------- 上下文结构 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 解析器上下文");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("typedef struct {");
            sb.AppendLine("    const uint8_t* base;          // 数据基址");
            sb.AppendLine("    const MetroHeader* hdr;       // 文件头指针");
            sb.AppendLine("    const char* str_pool;         // 字符串池指针");
            sb.AppendLine("    const LineEntry* lines;       // 线路表指针");
            sb.AppendLine("    const StationMeta* stations;  // 站点表指针");
            sb.AppendLine("    const NodeEntry* nodes;       // 节点表指针");
            sb.AppendLine("    const RailEdge* rail_edges;   // 运行边表指针");
            sb.AppendLine("    const TransferEdge* transfer_edges; // 换乘边表指针");
            sb.AppendLine("    const FareSegment* fares;     // 票价表指针");
            sb.AppendLine("} MetroContext;");
            sb.AppendLine();

            // ---------- 函数声明 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 初始化函数");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("void metro_init(MetroContext* ctx, const uint8_t* data);");
            sb.AppendLine();

            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 查询函数");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("uint16_t metro_line_count(const MetroContext* ctx);");
            sb.AppendLine("const char* metro_get_line_name(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine("uint32_t metro_get_line_color(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine("uint16_t metro_get_line_max_speed(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine();
            sb.AppendLine("uint16_t metro_station_count(const MetroContext* ctx);");
            sb.AppendLine("const char* metro_get_station_name(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine("void metro_get_station_pos(const MetroContext* ctx, uint16_t idx, int32_t* lon, int32_t* lat);");
            sb.AppendLine();
            sb.AppendLine("uint16_t metro_node_count(const MetroContext* ctx);");
            sb.AppendLine("uint16_t metro_get_node_station_idx(const MetroContext* ctx, uint16_t node_id);");
            sb.AppendLine("uint16_t metro_get_node_line_idx(const MetroContext* ctx, uint16_t node_id);");
            sb.AppendLine();
            sb.AppendLine("uint16_t metro_rail_edge_count(const MetroContext* ctx);");
            sb.AppendLine("const RailEdge* metro_get_rail_edge(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine();
            sb.AppendLine("uint16_t metro_transfer_edge_count(const MetroContext* ctx);");
            sb.AppendLine("const TransferEdge* metro_get_transfer_edge(const MetroContext* ctx, uint16_t idx);");
            sb.AppendLine();
            sb.AppendLine("uint16_t metro_find_nodes_by_station(const MetroContext* ctx, uint16_t station_idx, const uint16_t** out_nodes);");
            sb.AppendLine("uint32_t metro_calc_fare(const MetroContext* ctx, uint32_t dist_cm);");
            sb.AppendLine();

            // ---------- 路径规划函数 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 路径规划（Dijkstra）");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("#define METRO_MAX_NODES 2048");
            sb.AppendLine("#define METRO_INF_TIME 65535");
            sb.AppendLine();
            sb.AppendLine("uint16_t metro_find_path(const MetroContext* ctx,");
            sb.AppendLine("                          uint16_t start_node,");
            sb.AppendLine("                          uint16_t end_node,");
            sb.AppendLine("                          uint16_t* out_path,");
            sb.AppendLine("                          uint16_t max_path_len);");
            sb.AppendLine();
            sb.AppendLine("uint32_t metro_path_total_time(const MetroContext* ctx,");
            sb.AppendLine("                               const uint16_t* path,");
            sb.AppendLine("                               uint16_t path_len);");
            sb.AppendLine();
            sb.AppendLine("uint32_t metro_path_total_distance(const MetroContext* ctx,");
            sb.AppendLine("                                  const uint16_t* path,");
            sb.AppendLine("                                  uint16_t path_len);");
            sb.AppendLine();

            sb.AppendLine("#endif /* METRO_PARSER_H_ */");
            return sb.ToString();
        }

        /// <summary>
        /// 生成 metro_parser.c
        /// </summary>
        private static string GenerateParserSource(string cityUpper, string cityName)
        {
            var sb = new StringBuilder();

            sb.AppendLine("/*");
            sb.AppendLine(" * metro_parser.c - Metro 路网数据解析器实现");
            sb.AppendLine(" * 通用解析器实现，可与不同的 metro_data.h 配合使用");
            if (!string.IsNullOrEmpty(cityName))
                sb.AppendLine($" * 城市: {cityName}");
            sb.AppendLine($" * 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(" */");
            sb.AppendLine();

            sb.AppendLine("#include \"metro_parser.h\"");
            sb.AppendLine("#include <string.h>");
            sb.AppendLine();

            // ---------- 初始化函数 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 初始化");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("void metro_init(MetroContext* ctx, const uint8_t* data)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || !data) return;");
            sb.AppendLine("    ctx->base = data;");
            sb.AppendLine("    ctx->hdr = (const MetroHeader*)data;");
            sb.AppendLine("    ctx->str_pool = (const char*)(data + ctx->hdr->str_offset);");
            sb.AppendLine("    ctx->lines = (const LineEntry*)(data + ctx->hdr->line_offset);");
            sb.AppendLine("    ctx->stations = (const StationMeta*)(data + ctx->hdr->station_meta_offset);");
            sb.AppendLine("    ctx->nodes = (const NodeEntry*)(data + ctx->hdr->node_offset);");
            sb.AppendLine("    ctx->rail_edges = (const RailEdge*)(data + ctx->hdr->rail_edge_offset);");
            sb.AppendLine("    ctx->transfer_edges = (const TransferEdge*)(data + ctx->hdr->transfer_edge_offset);");
            sb.AppendLine("    ctx->fares = (const FareSegment*)(data + ctx->hdr->fare_offset);");
            sb.AppendLine("}");
            sb.AppendLine();

            // ---------- 线路查询 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 线路查询");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("uint16_t metro_line_count(const MetroContext* ctx)");
            sb.AppendLine("{");
            sb.AppendLine("    return ctx->hdr ? MBIN_READ_U16(&ctx->hdr->line_count) : 0;");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("const char* metro_get_line_name(const MetroContext* ctx, uint16_t idx)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || idx >= metro_line_count(ctx)) return NULL;");
            sb.AppendLine("    const LineEntry* line = &ctx->lines[idx];");
            sb.AppendLine("    return ctx->str_pool + MBIN_READ_U32(&line->name_idx);");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("uint32_t metro_get_line_color(const MetroContext* ctx, uint16_t idx)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || idx >= metro_line_count(ctx)) return 0xCCCCCC;");
            sb.AppendLine("    return MBIN_READ_U32(&ctx->lines[idx].color_rgb);");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("uint16_t metro_get_line_max_speed(const MetroContext* ctx, uint16_t idx)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || idx >= metro_line_count(ctx)) return 0;");
            sb.AppendLine("    return MBIN_READ_U16(&ctx->lines[idx].max_speed);");
            sb.AppendLine("}");
            sb.AppendLine();

            // ---------- 站点查询 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 站点查询");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("uint16_t metro_station_count(const MetroContext* ctx)");
            sb.AppendLine("{");
            sb.AppendLine("    return ctx->hdr ? MBIN_READ_U16(&ctx->hdr->station_count) : 0;");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("const char* metro_get_station_name(const MetroContext* ctx, uint16_t idx)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || idx >= metro_station_count(ctx)) return NULL;");
            sb.AppendLine("    const StationMeta* st = &ctx->stations[idx];");
            sb.AppendLine("    return ctx->str_pool + MBIN_READ_U16(&st->name_idx);");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("void metro_get_station_pos(const MetroContext* ctx, uint16_t idx, int32_t* lon, int32_t* lat)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || idx >= metro_station_count(ctx)) {");
            sb.AppendLine("        if (lon) *lon = 0;");
            sb.AppendLine("        if (lat) *lat = 0;");
            sb.AppendLine("        return;");
            sb.AppendLine("    }");
            sb.AppendLine("    const StationMeta* st = &ctx->stations[idx];");
            sb.AppendLine("    if (lon) *lon = MBIN_READ_S32(&st->lon);");
            sb.AppendLine("    if (lat) *lat = MBIN_READ_S32(&st->lat);");
            sb.AppendLine("}");
            sb.AppendLine();

            // ---------- 节点查询 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 节点查询");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("uint16_t metro_node_count(const MetroContext* ctx)");
            sb.AppendLine("{");
            sb.AppendLine("    return ctx->hdr ? MBIN_READ_U16(&ctx->hdr->node_count) : 0;");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("uint16_t metro_get_node_station_idx(const MetroContext* ctx, uint16_t node_id)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || node_id >= metro_node_count(ctx)) return 0xFFFF;");
            sb.AppendLine("    return MBIN_READ_U16(&ctx->nodes[node_id].station_idx);");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("uint16_t metro_get_node_line_idx(const MetroContext* ctx, uint16_t node_id)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || node_id >= metro_node_count(ctx)) return 0xFFFF;");
            sb.AppendLine("    return MBIN_READ_U16(&ctx->nodes[node_id].display_line_idx);");
            sb.AppendLine("}");
            sb.AppendLine();

            // ---------- 边查询 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 边查询");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("uint16_t metro_rail_edge_count(const MetroContext* ctx)");
            sb.AppendLine("{");
            sb.AppendLine("    return ctx->hdr ? MBIN_READ_U16(&ctx->hdr->rail_edge_count) : 0;");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("const RailEdge* metro_get_rail_edge(const MetroContext* ctx, uint16_t idx)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || idx >= metro_rail_edge_count(ctx)) return NULL;");
            sb.AppendLine("    return &ctx->rail_edges[idx];");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("uint16_t metro_transfer_edge_count(const MetroContext* ctx)");
            sb.AppendLine("{");
            sb.AppendLine("    return ctx->hdr ? MBIN_READ_U16(&ctx->hdr->transfer_edge_count) : 0;");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("const TransferEdge* metro_get_transfer_edge(const MetroContext* ctx, uint16_t idx)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || idx >= metro_transfer_edge_count(ctx)) return NULL;");
            sb.AppendLine("    return &ctx->transfer_edges[idx];");
            sb.AppendLine("}");
            sb.AppendLine();

            // ---------- 查找表 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 站名 -> 节点列表查找");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("uint16_t metro_find_nodes_by_station(const MetroContext* ctx, uint16_t station_idx, const uint16_t** out_nodes)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || !out_nodes) return 0;");
            sb.AppendLine("    const uint8_t* ptr = ctx->base + ctx->hdr->lookup_offset;");
            sb.AppendLine("    uint16_t count = MBIN_READ_U16(ptr);");
            sb.AppendLine("    ptr += 2;");
            sb.AppendLine("    for (uint16_t i = 0; i < count; i++) {");
            sb.AppendLine("        uint16_t st_idx = MBIN_READ_U16(ptr);");
            sb.AppendLine("        uint16_t node_cnt = MBIN_READ_U16(ptr + 2);");
            sb.AppendLine("        ptr += 4;");
            sb.AppendLine("        if (st_idx == station_idx) {");
            sb.AppendLine("            *out_nodes = (const uint16_t*)ptr;");
            sb.AppendLine("            return node_cnt;");
            sb.AppendLine("        }");
            sb.AppendLine("        ptr += node_cnt * 2;");
            sb.AppendLine("    }");
            sb.AppendLine("    *out_nodes = NULL;");
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");
            sb.AppendLine();

            // ---------- 票价计算 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 票价计算");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("uint32_t metro_calc_fare(const MetroContext* ctx, uint32_t dist_cm)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx) return 200;");
            sb.AppendLine("    uint16_t count = MBIN_READ_U16(&ctx->hdr->fare_count);");
            sb.AppendLine("    for (uint16_t i = 0; i < count; i++) {");
            sb.AppendLine("        uint32_t start = MBIN_READ_U32(&ctx->fares[i].start_cm);");
            sb.AppendLine("        uint32_t end = MBIN_READ_U32(&ctx->fares[i].end_cm);");
            sb.AppendLine("        if (dist_cm >= start && dist_cm < end) {");
            sb.AppendLine("            return MBIN_READ_U32(&ctx->fares[i].price_fen);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    return 200; // 默认2元（分）");
            sb.AppendLine("}");
            sb.AppendLine();

            // ---------- Dijkstra 路径规划 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 路径规划 (Dijkstra)");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("uint16_t metro_find_path(const MetroContext* ctx,");
            sb.AppendLine("                          uint16_t start_node,");
            sb.AppendLine("                          uint16_t end_node,");
            sb.AppendLine("                          uint16_t* out_path,");
            sb.AppendLine("                          uint16_t max_path_len)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || !out_path || start_node == end_node) return 0;");
            sb.AppendLine("");
            sb.AppendLine("    uint16_t n = metro_node_count(ctx);");
            sb.AppendLine("    if (n > METRO_MAX_NODES) n = METRO_MAX_NODES;");
            sb.AppendLine("");
            sb.AppendLine("    uint16_t dist[METRO_MAX_NODES];");
            sb.AppendLine("    uint16_t prev[METRO_MAX_NODES];");
            sb.AppendLine("    bool visited[METRO_MAX_NODES];");
            sb.AppendLine("");
            sb.AppendLine("    for (uint16_t i = 0; i < n; i++) {");
            sb.AppendLine("        dist[i] = METRO_INF_TIME;");
            sb.AppendLine("        prev[i] = 0xFFFF;");
            sb.AppendLine("        visited[i] = false;");
            sb.AppendLine("    }");
            sb.AppendLine("    dist[start_node] = 0;");
            sb.AppendLine("");
            sb.AppendLine("    for (uint16_t k = 0; k < n; k++) {");
            sb.AppendLine("        uint16_t u = 0xFFFF;");
            sb.AppendLine("        uint16_t min_d = METRO_INF_TIME;");
            sb.AppendLine("        for (uint16_t i = 0; i < n; i++) {");
            sb.AppendLine("            if (!visited[i] && dist[i] < min_d) {");
            sb.AppendLine("                min_d = dist[i];");
            sb.AppendLine("                u = i;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        if (u == 0xFFFF || u == end_node) break;");
            sb.AppendLine("        visited[u] = true;");
            sb.AppendLine("");
            sb.AppendLine("        // 遍历运行边");
            sb.AppendLine("        uint16_t rail_cnt = metro_rail_edge_count(ctx);");
            sb.AppendLine("        for (uint16_t i = 0; i < rail_cnt; i++) {");
            sb.AppendLine("            const RailEdge* e = metro_get_rail_edge(ctx, i);");
            sb.AppendLine("            uint16_t from = MBIN_READ_U16(&e->from);");
            sb.AppendLine("            if (from != u) continue;");
            sb.AppendLine("            uint16_t to = MBIN_READ_U16(&e->to);");
            sb.AppendLine("            uint16_t w = MBIN_READ_U16(&e->time_sec);");
            sb.AppendLine("            if (dist[u] + w < dist[to]) {");
            sb.AppendLine("                dist[to] = dist[u] + w;");
            sb.AppendLine("                prev[to] = u;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("");
            sb.AppendLine("        // 遍历换乘边");
            sb.AppendLine("        uint16_t trans_cnt = metro_transfer_edge_count(ctx);");
            sb.AppendLine("        for (uint16_t i = 0; i < trans_cnt; i++) {");
            sb.AppendLine("            const TransferEdge* e = metro_get_transfer_edge(ctx, i);");
            sb.AppendLine("            uint16_t from = MBIN_READ_U16(&e->from);");
            sb.AppendLine("            if (from != u) continue;");
            sb.AppendLine("            uint16_t to = MBIN_READ_U16(&e->to);");
            sb.AppendLine("            uint16_t w = MBIN_READ_U16(&e->time_sec);");
            sb.AppendLine("            if (dist[u] + w < dist[to]) {");
            sb.AppendLine("                dist[to] = dist[u] + w;");
            sb.AppendLine("                prev[to] = u;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    if (dist[end_node] == METRO_INF_TIME) return 0;");
            sb.AppendLine("");
            sb.AppendLine("    uint16_t path_len = 0;");
            sb.AppendLine("    uint16_t cur = end_node;");
            sb.AppendLine("    while (cur != 0xFFFF && cur != start_node && path_len < max_path_len) {");
            sb.AppendLine("        out_path[path_len++] = cur;");
            sb.AppendLine("        cur = prev[cur];");
            sb.AppendLine("    }");
            sb.AppendLine("    if (cur == start_node) out_path[path_len++] = start_node;");
            sb.AppendLine("");
            sb.AppendLine("    // 反转路径");
            sb.AppendLine("    for (uint16_t i = 0; i < path_len / 2; i++) {");
            sb.AppendLine("        uint16_t tmp = out_path[i];");
            sb.AppendLine("        out_path[i] = out_path[path_len - 1 - i];");
            sb.AppendLine("        out_path[path_len - 1 - i] = tmp;");
            sb.AppendLine("    }");
            sb.AppendLine("    return path_len;");
            sb.AppendLine("}");
            sb.AppendLine();

            // ---------- 路径统计 ----------
            sb.AppendLine("// ============================================================");
            sb.AppendLine("// 路径统计");
            sb.AppendLine("// ============================================================");
            sb.AppendLine("uint32_t metro_path_total_time(const MetroContext* ctx,");
            sb.AppendLine("                               const uint16_t* path,");
            sb.AppendLine("                               uint16_t path_len)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || !path || path_len < 2) return 0;");
            sb.AppendLine("    uint32_t total = 0;");
            sb.AppendLine("    for (uint16_t i = 0; i < path_len - 1; i++) {");
            sb.AppendLine("        uint16_t from = path[i];");
            sb.AppendLine("        uint16_t to = path[i + 1];");
            sb.AppendLine("        bool found = false;");
            sb.AppendLine("        uint16_t rail_cnt = metro_rail_edge_count(ctx);");
            sb.AppendLine("        for (uint16_t j = 0; j < rail_cnt; j++) {");
            sb.AppendLine("            const RailEdge* e = metro_get_rail_edge(ctx, j);");
            sb.AppendLine("            if (MBIN_READ_U16(&e->from) == from && MBIN_READ_U16(&e->to) == to) {");
            sb.AppendLine("                total += MBIN_READ_U16(&e->time_sec);");
            sb.AppendLine("                found = true;");
            sb.AppendLine("                break;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        if (found) continue;");
            sb.AppendLine("        uint16_t trans_cnt = metro_transfer_edge_count(ctx);");
            sb.AppendLine("        for (uint16_t j = 0; j < trans_cnt; j++) {");
            sb.AppendLine("            const TransferEdge* e = metro_get_transfer_edge(ctx, j);");
            sb.AppendLine("            if (MBIN_READ_U16(&e->from) == from && MBIN_READ_U16(&e->to) == to) {");
            sb.AppendLine("                total += MBIN_READ_U16(&e->time_sec);");
            sb.AppendLine("                found = true;");
            sb.AppendLine("                break;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    return total;");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("uint32_t metro_path_total_distance(const MetroContext* ctx,");
            sb.AppendLine("                                  const uint16_t* path,");
            sb.AppendLine("                                  uint16_t path_len)");
            sb.AppendLine("{");
            sb.AppendLine("    if (!ctx || !path || path_len < 2) return 0;");
            sb.AppendLine("    uint32_t total = 0;");
            sb.AppendLine("    for (uint16_t i = 0; i < path_len - 1; i++) {");
            sb.AppendLine("        uint16_t from = path[i];");
            sb.AppendLine("        uint16_t to = path[i + 1];");
            sb.AppendLine("        uint16_t rail_cnt = metro_rail_edge_count(ctx);");
            sb.AppendLine("        for (uint16_t j = 0; j < rail_cnt; j++) {");
            sb.AppendLine("            const RailEdge* e = metro_get_rail_edge(ctx, j);");
            sb.AppendLine("            if (MBIN_READ_U16(&e->from) == from && MBIN_READ_U16(&e->to) == to) {");
            sb.AppendLine("                total += MBIN_READ_U32(&e->dist_cm);");
            sb.AppendLine("                break;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    return total;");
            sb.AppendLine("}");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// 生成 metro_data.h（只含数据数组）
        /// </summary>
        private static string GenerateDataHeader(byte[] data, string cityUpper, string cityName)
        {
            var sb = new StringBuilder();

            sb.AppendLine("/*");
            sb.AppendLine(" * metro_data.h - 具体城市的路网数据");
            if (!string.IsNullOrEmpty(cityName))
                sb.AppendLine($" * 城市: {cityName}");
            sb.AppendLine($" * 数据大小: {data.Length} 字节");
            sb.AppendLine($" * 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(" */");
            sb.AppendLine();

            string guardName = $"_METRO_DATA_H_";
            sb.AppendLine($"#ifndef {guardName}");
            sb.AppendLine($"#define {guardName}");
            sb.AppendLine();

            sb.AppendLine("#include <stdint.h>");
            sb.AppendLine();

            string macroName = string.IsNullOrEmpty(cityName)
                ? "METRO_DATA_SIZE"
                : $"METRO_DATA_SIZE";

            sb.AppendLine($"#define {macroName} ({data.Length})");
            sb.AppendLine();

            // 如果城市名不为空，也导出一个通用宏指向具体宏
            if (!string.IsNullOrEmpty(cityName))
            {
                sb.AppendLine($"#define METRO_DATA_SIZE {macroName}");
                sb.AppendLine();
            }

            sb.AppendLine($"static const uint8_t metro_data[{data.Length}] = {{");

            for (int i = 0; i < data.Length; i++)
            {
                if (i % 16 == 0)
                    sb.Append("    ");
                sb.Append($"0x{data[i]:X2}");
                if (i < data.Length - 1)
                    sb.Append(", ");
                if ((i + 1) % 16 == 0 && i < data.Length - 1)
                    sb.AppendLine();
            }

            if (data.Length % 16 != 0)
                sb.AppendLine();

            sb.AppendLine("};");
            sb.AppendLine();

            sb.AppendLine("#endif /* METRO_DATA_H_ */");
            return sb.ToString();
        }
    }
}