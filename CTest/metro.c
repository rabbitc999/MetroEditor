#include "metro.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <ctype.h>
#include "uthash.h"

/* -------------------------------------------------------------------------
* 内存分配包装（兼容性）
* ------------------------------------------------------------------------- */
#ifndef METRO_MALLOC
#define METRO_MALLOC(size)   malloc(size)
#endif
#ifndef METRO_FREE
#define METRO_FREE(ptr)      free(ptr)
#endif
#ifndef METRO_REALLOC
#define METRO_REALLOC(ptr, size) realloc(ptr, size)
#endif

/* -------------------------------------------------------------------------
* 内部常量
* ------------------------------------------------------------------------- */
#define MAGIC               "ROUT"
#define VERSION_SUPPORTED   5

enum SectionType {
    TYPE_STATION = 1,
    TYPE_LINE    = 2,
    TYPE_DIR     = 3,
    TYPE_RECORD  = 4,
    TYPE_ORDER   = 5
};

/* -------------------------------------------------------------------------
* 内部数据结构（用于线路顺序表）
* ------------------------------------------------------------------------- */
typedef struct __LineOrderDir_T {
    char             *dirName;        /* 方向名称 */
    int               stationCount;   /* 站点数 */
    int              *stationIds;     /* 站点ID顺序 */
    unsigned char    *status;         /* 0=正常, 1=pending */
} LineOrderDir_T;

typedef struct __LineOrder_T {
    int               lineId;         /* 线路ID */
    int               dirCount;       /* 方向数量 */
    LineOrderDir_T   *dirs;           /* 方向数组 */
} LineOrder_T;

/* -------------------------------------------------------------------------
* 内部全局变量（静态，符合命名规范）
* ------------------------------------------------------------------------- */
static Station_T   *s_Stations      = NULL;
static int          s_StationCount  = 0;
static Line_T      *s_Lines         = NULL;
static int          s_LineCount     = 0;
static Direction_T *s_Directions    = NULL;
static int          s_DirCount      = 0;
static RouteRecord_T *s_Records     = NULL;
static int          s_RecordCount   = 0;
static LineOrder_T *s_LineOrders    = NULL;   /* 索引0对应线路ID=1 */
static char        *s_KeyCity       = NULL;

/* uthash 索引（用于快速查找路线） */
typedef struct __RouteKey_T {
    int startId;
    int endId;
} RouteKey_T;

typedef struct __RouteIndex_T {
    RouteKey_T       key;
    RouteRecord_T   *record;
    UT_hash_handle   hh;
} RouteIndex_T;

static RouteIndex_T *s_IndexMap = NULL;

/* -------------------------------------------------------------------------
* 内部辅助函数：小端读取
* ------------------------------------------------------------------------- */
static uint16_t ReadLE16(const uint8_t *p) {
    return (uint16_t)p[0] | ((uint16_t)p[1] << 8);
}

static uint32_t ReadLE32(const uint8_t *p) {
    return (uint32_t)p[0] | ((uint32_t)p[1] << 8) |
    ((uint32_t)p[2] << 16) | ((uint32_t)p[3] << 24);
}

static uint64_t ReadLE64(const uint8_t *p) {
    uint64_t v = 0;
    for (int i = 0; i < 8; i++)
        v |= ((uint64_t)p[i] << (i*8));
    return v;
}

static float ReadFloat(const uint8_t *p) {
    uint32_t u = ReadLE32(p);
    float f;
    memcpy(&f, &u, 4);
    return f;
}

/* -------------------------------------------------------------------------
* 内部清理函数（释放所有动态资源）
* ------------------------------------------------------------------------- */
static void CleanupInternal(void) {
    /* 站点 */
    for (int i = 0; i < s_StationCount; i++) {
        METRO_FREE(s_Stations[i].name);
        METRO_FREE(s_Stations[i].pinyin);
        METRO_FREE(s_Stations[i].lineIds);
    }
    METRO_FREE(s_Stations);
    s_Stations = NULL;
    s_StationCount = 0;
    
    /* 线路 */
    for (int i = 0; i < s_LineCount; i++) {
        METRO_FREE(s_Lines[i].name);
        METRO_FREE(s_Lines[i].city);
    }
    METRO_FREE(s_Lines);
    s_Lines = NULL;
    s_LineCount = 0;
    
    /* 方向 */
    for (int i = 0; i < s_DirCount; i++) {
        METRO_FREE(s_Directions[i].name);
    }
    METRO_FREE(s_Directions);
    s_Directions = NULL;
    s_DirCount = 0;
    
    /* 路线记录 */
    for (int i = 0; i < s_RecordCount; i++) {
        METRO_FREE(s_Records[i].segs);
    }
    METRO_FREE(s_Records);
    s_Records = NULL;
    s_RecordCount = 0;
    
    /* 线路顺序表 */
    if (s_LineOrders != NULL) {
        for (int i = 0; i < s_LineCount; i++) {
            for (int d = 0; d < s_LineOrders[i].dirCount; d++) {
                METRO_FREE(s_LineOrders[i].dirs[d].dirName);
                METRO_FREE(s_LineOrders[i].dirs[d].stationIds);
                METRO_FREE(s_LineOrders[i].dirs[d].status);
            }
            METRO_FREE(s_LineOrders[i].dirs);
        }
        METRO_FREE(s_LineOrders);
        s_LineOrders = NULL;
    }
    
    /* key_city */
    METRO_FREE(s_KeyCity);
    s_KeyCity = NULL;
    
    /* uthash 索引 */
    RouteIndex_T *cur, *tmp;
    HASH_ITER(hh, s_IndexMap, cur, tmp) {
        HASH_DEL(s_IndexMap, cur);
        METRO_FREE(cur);
    }
    s_IndexMap = NULL;
}

/* -------------------------------------------------------------------------
* 加载二进制文件（内部实现）
* ------------------------------------------------------------------------- */
static int LoadBinary(const uint8_t *data, size_t dataSize) {
    if (data == NULL || dataSize < 6) {
        fprintf(stderr, "数据指针无效或长度不足\n");
        return 0;
    }
    
    const uint8_t *ptr = data;
    const uint8_t *end = data + dataSize;
    
    /* 校验魔数和版本 */
    if (end - ptr < 6) {
        fprintf(stderr, "数据长度不足，无法读取文件头\n");
        return 0;
    }
    if (memcmp(ptr, MAGIC, 4) != 0) {
        fprintf(stderr, "无效的魔数\n");
        return 0;
    }
    uint16_t version = ReadLE16(ptr + 4);
    if (version != VERSION_SUPPORTED) {
        fprintf(stderr, "不支持的版本: %u (需要 %d)\n", version, VERSION_SUPPORTED);
        return 0;
    }
    ptr += 6;   /* 跳过 header (magic+version) */
    
    /* 读取 key_city */
    if (end - ptr < 2) {
        fprintf(stderr, "数据长度不足，无法读取key_city长度\n");
        return 0;
    }
    uint16_t keyCityLen = ReadLE16(ptr);
    ptr += 2;
    if (end - ptr < keyCityLen) {
        fprintf(stderr, "数据长度不足，无法读取key_city内容\n");
        return 0;
    }
    s_KeyCity = (char*)METRO_MALLOC(keyCityLen + 1);
    if (s_KeyCity == NULL) {
        fprintf(stderr, "内存不足\n");
        return 0;
    }
    memcpy(s_KeyCity, ptr, keyCityLen);
    s_KeyCity[keyCityLen] = '\0';
    ptr += keyCityLen;
    
    /* 读取偏移表数量 */
    if (end - ptr < 2) {
        fprintf(stderr, "数据长度不足，无法读取区块数量\n");
        CleanupInternal();
        return 0;
    }
    uint16_t sectionCount = ReadLE16(ptr);
    ptr += 2;
    
    /* 读取偏移表（每个条目18字节） */
    size_t secTableSize = (size_t)sectionCount * 18;
    if (end - ptr < secTableSize) {
        fprintf(stderr, "数据长度不足，无法读取偏移表\n");
        CleanupInternal();
        return 0;
    }
    const uint8_t *secBuf = ptr;
    ptr += secTableSize;
    
    uint64_t offStation = 0, lenStation = 0;
    uint64_t offLine    = 0, lenLine    = 0;
    uint64_t offDir     = 0, lenDir     = 0;
    uint64_t offRecord  = 0, lenRecord  = 0;
    uint64_t offOrder   = 0, lenOrder   = 0;
    
    for (int i = 0; i < sectionCount; i++) {
        const uint8_t *p = secBuf + i * 18;
        uint16_t type = ReadLE16(p);
        uint64_t off  = ReadLE64(p + 2);
        uint64_t len  = ReadLE64(p + 10);
        switch (type) {
            case TYPE_STATION: offStation = off; lenStation = len; break;
            case TYPE_LINE:    offLine    = off; lenLine    = len; break;
            case TYPE_DIR:     offDir     = off; lenDir     = len; break;
            case TYPE_RECORD:  offRecord  = off; lenRecord  = len; break;
            case TYPE_ORDER:   offOrder   = off; lenOrder   = len; break;
        }
    }
    
    /* ----- 站点区块 ----- */
    if (offStation == 0 || lenStation == 0) {
        fprintf(stderr, "站点区块偏移或长度无效\n");
        CleanupInternal();
        return 0;
    }
    if (offStation + lenStation > dataSize) {
        fprintf(stderr, "站点区块超出数据范围\n");
        CleanupInternal();
        return 0;
    }
    const uint8_t *staData = data + offStation;
    const uint8_t *staEnd = staData + lenStation;
    /* 先遍历一遍统计站点数量 */
    const uint8_t *p = staData;
    int count = 0;
    while (p < staEnd) {
        if (p + 2 > staEnd) break;
        uint16_t nameLen = ReadLE16(p); p += 2;
        if (p + nameLen > staEnd) break;
        p += nameLen;
        if (p + 2 > staEnd) break;
        uint16_t pyLen = ReadLE16(p); p += 2;
        if (p + pyLen > staEnd) break;
        p += pyLen;
        if (p + 8 > staEnd) break;
        p += 8; /* lon+lat */
        if (p + 2 > staEnd) break;
        uint16_t lineCnt = ReadLE16(p); p += 2;
        if (p + lineCnt * 2 > staEnd) break;
        p += lineCnt * 2;
        count++;
    }
    s_StationCount = count;
    s_Stations = (Station_T*)METRO_MALLOC(count * sizeof(Station_T));
    if (s_Stations == NULL) {
        fprintf(stderr, "内存不足\n");
        CleanupInternal();
        return 0;
    }
    
    p = staData;
    for (int i = 0; i < count; i++) {
        uint16_t nameLen = ReadLE16(p); p += 2;
        char *name = (char*)METRO_MALLOC(nameLen + 1);
        memcpy(name, p, nameLen);
        name[nameLen] = '\0';
        p += nameLen;
        
        uint16_t pyLen = ReadLE16(p); p += 2;
        char *pinyin = (char*)METRO_MALLOC(pyLen + 1);
        memcpy(pinyin, p, pyLen);
        pinyin[pyLen] = '\0';
        p += pyLen;
        
        float lon = ReadFloat(p); p += 4;
        float lat = ReadFloat(p); p += 4;
        uint16_t lineCnt = ReadLE16(p); p += 2;
        int *lineIds = (int*)METRO_MALLOC(lineCnt * sizeof(int));
        for (int j = 0; j < lineCnt; j++) {
            lineIds[j] = ReadLE16(p);
            p += 2;
        }
        
        s_Stations[i].id = i + 1;
        s_Stations[i].name = name;
        s_Stations[i].pinyin = pinyin;
        s_Stations[i].lon = lon;
        s_Stations[i].lat = lat;
        s_Stations[i].lineCount = lineCnt;
        s_Stations[i].lineIds = lineIds;
    }
    
    /* ----- 线路区块 ----- */
    if (offLine == 0 || lenLine == 0) {
        fprintf(stderr, "线路区块偏移或长度无效\n");
        CleanupInternal();
        return 0;
    }
    if (offLine + lenLine > dataSize) {
        fprintf(stderr, "线路区块超出数据范围\n");
        CleanupInternal();
        return 0;
    }
    const uint8_t *lineData = data + offLine;
    const uint8_t *lineEnd = lineData + lenLine;
    p = lineData;
    count = 0;
    while (p < lineEnd) {
        if (p + 2 > lineEnd) break;
        uint16_t nameLen = ReadLE16(p); p += 2;
        if (p + nameLen > lineEnd) break;
        p += nameLen;
        if (p + 4 > lineEnd) break;
        p += 4; /* color */
        if (p + 2 > lineEnd) break;
        uint16_t cityLen = ReadLE16(p); p += 2;
        if (p + cityLen > lineEnd) break;
        p += cityLen;
        count++;
    }
    s_LineCount = count;
    s_Lines = (Line_T*)METRO_MALLOC(count * sizeof(Line_T));
    if (s_Lines == NULL) {
        fprintf(stderr, "内存不足\n");
        CleanupInternal();
        return 0;
    }
    
    p = lineData;
    for (int i = 0; i < count; i++) {
        uint16_t nameLen = ReadLE16(p); p += 2;
        char *name = (char*)METRO_MALLOC(nameLen + 1);
        memcpy(name, p, nameLen);
        name[nameLen] = '\0';
        p += nameLen;
        uint32_t color = ReadLE32(p); p += 4;
        uint16_t cityLen = ReadLE16(p); p += 2;
        char *city = (char*)METRO_MALLOC(cityLen + 1);
        memcpy(city, p, cityLen);
        city[cityLen] = '\0';
        p += cityLen;
        s_Lines[i].id = i + 1;
        s_Lines[i].name = name;
        s_Lines[i].color = color;
        s_Lines[i].city = city;
    }
    
    /* ----- 方向区块 ----- */
    if (offDir == 0 || lenDir == 0) {
        fprintf(stderr, "方向区块偏移或长度无效\n");
        CleanupInternal();
        return 0;
    }
    if (offDir + lenDir > dataSize) {
        fprintf(stderr, "方向区块超出数据范围\n");
        CleanupInternal();
        return 0;
    }
    const uint8_t *dirData = data + offDir;
    const uint8_t *dirEnd = dirData + lenDir;
    p = dirData;
    count = 0;
    while (p < dirEnd) {
        if (p + 2 > dirEnd) break;
        uint16_t nameLen = ReadLE16(p);
        p += 2 + nameLen;
        count++;
    }
    s_DirCount = count;
    s_Directions = (Direction_T*)METRO_MALLOC(count * sizeof(Direction_T));
    if (s_Directions == NULL) {
        fprintf(stderr, "内存不足\n");
        CleanupInternal();
        return 0;
    }
    
    p = dirData;
    for (int i = 0; i < count; i++) {
        uint16_t nameLen = ReadLE16(p); p += 2;
        char *name = (char*)METRO_MALLOC(nameLen + 1);
        memcpy(name, p, nameLen);
        name[nameLen] = '\0';
        p += nameLen;
        s_Directions[i].id = i + 1;
        s_Directions[i].name = name;
    }
    
    /* ----- 路线记录区块 ----- */
    if (offRecord == 0 || lenRecord == 0) {
        fprintf(stderr, "记录区块偏移或长度无效\n");
        CleanupInternal();
        return 0;
    }
    if (offRecord + lenRecord > dataSize) {
        fprintf(stderr, "记录区块超出数据范围\n");
        CleanupInternal();
        return 0;
    }
    const uint8_t *recData = data + offRecord;
    const uint8_t *recEnd = recData + lenRecord;
    p = recData;
    count = 0;
    const uint8_t *q = p;
    while (q < recEnd) {
        if (q + 18 > recEnd) break;
        uint16_t segCnt = ReadLE16(q + 16);
        if (q + 18 + segCnt * 10 > recEnd) break;
        q += 18 + segCnt * 10;
        count++;
    }
    s_RecordCount = count;
    s_Records = (RouteRecord_T*)METRO_MALLOC(count * sizeof(RouteRecord_T));
    if (s_Records == NULL) {
        fprintf(stderr, "内存不足\n");
        CleanupInternal();
        return 0;
    }
    
    p = recData;
    for (int i = 0; i < count; i++) {
        if (p + 18 > recEnd) break;
        RouteRecord_T *rec = &s_Records[i];
        rec->start = ReadLE16(p);
        rec->end   = ReadLE16(p + 2);
        rec->dist  = ReadLE32(p + 4);
        rec->time  = ReadLE16(p + 8);
        rec->fare  = ReadLE16(p + 10);
        rec->transfers = ReadLE16(p + 12);
        rec->totalStops = ReadLE16(p + 14);
        rec->segCount = ReadLE16(p + 16);
        p += 18;
        rec->segs = (RouteSegment_T*)METRO_MALLOC(rec->segCount * sizeof(RouteSegment_T));
        if (rec->segs == NULL) {
            fprintf(stderr, "内存不足\n");
            CleanupInternal();
            return 0;
        }
        for (int j = 0; j < rec->segCount; j++) {
            rec->segs[j].start = ReadLE16(p);
            rec->segs[j].line  = ReadLE16(p + 2);
            rec->segs[j].dir   = ReadLE16(p + 4);
            rec->segs[j].stops = ReadLE16(p + 6);
            rec->segs[j].end   = ReadLE16(p + 8);
            p += 10;
        }
        RouteIndex_T *entry = (RouteIndex_T*)METRO_MALLOC(sizeof(RouteIndex_T));
        entry->key.startId = rec->start;
        entry->key.endId = rec->end;
        entry->record = rec;
        HASH_ADD(hh, s_IndexMap, key, sizeof(RouteKey_T), entry);
    }
    
    /* ----- 线路顺序表 ----- */
    if (offOrder == 0 || lenOrder == 0) {
        fprintf(stderr, "顺序区块偏移或长度无效\n");
        CleanupInternal();
        return 0;
    }
    if (offOrder + lenOrder > dataSize) {
        fprintf(stderr, "顺序区块超出数据范围\n");
        CleanupInternal();
        return 0;
    }
    const uint8_t *ordData = data + offOrder;
    const uint8_t *ordEnd = ordData + lenOrder;
    p = ordData;
    if (p + 2 > ordEnd) {
        fprintf(stderr, "顺序区块数据不足\n");
        CleanupInternal();
        return 0;
    }
    uint16_t lineOrderCount = ReadLE16(p); p += 2;
    if (lineOrderCount != s_LineCount) {
        fprintf(stderr, "顺序表线路数(%d)与线路表(%d)不符\n", lineOrderCount, s_LineCount);
        CleanupInternal();
        return 0;
    }
    s_LineOrders = (LineOrder_T*)METRO_MALLOC(s_LineCount * sizeof(LineOrder_T));
    if (s_LineOrders == NULL) {
        fprintf(stderr, "内存不足\n");
        CleanupInternal();
        return 0;
    }
    for (int i = 0; i < s_LineCount; i++) {
        if (p + 1 > ordEnd) {
            fprintf(stderr, "顺序区块数据不足\n");
            CleanupInternal();
            return 0;
        }
        uint8_t dirCnt = *p; p += 1;
        s_LineOrders[i].lineId = i + 1;
        s_LineOrders[i].dirCount = dirCnt;
        s_LineOrders[i].dirs = (LineOrderDir_T*)METRO_MALLOC(dirCnt * sizeof(LineOrderDir_T));
        if (s_LineOrders[i].dirs == NULL) {
            fprintf(stderr, "内存不足\n");
            CleanupInternal();
            return 0;
        }
        for (int d = 0; d < dirCnt; d++) {
            /* 方向名称 */
            if (p + 2 > ordEnd) {
                fprintf(stderr, "顺序区块数据不足\n");
                CleanupInternal();
                return 0;
            }
            uint16_t dirNameLen = ReadLE16(p); p += 2;
            if (p + dirNameLen > ordEnd) {
                fprintf(stderr, "顺序区块数据不足\n");
                CleanupInternal();
                return 0;
            }
            char *dirName = (char*)METRO_MALLOC(dirNameLen + 1);
            memcpy(dirName, p, dirNameLen);
            dirName[dirNameLen] = '\0';
            p += dirNameLen;
            s_LineOrders[i].dirs[d].dirName = dirName;
            
            /* 站点列表 */
            if (p + 2 > ordEnd) {
                fprintf(stderr, "顺序区块数据不足\n");
                CleanupInternal();
                return 0;
            }
            uint16_t stCnt = ReadLE16(p); p += 2;
            s_LineOrders[i].dirs[d].stationCount = stCnt;
            s_LineOrders[i].dirs[d].stationIds = (int*)METRO_MALLOC(stCnt * sizeof(int));
            s_LineOrders[i].dirs[d].status = (unsigned char*)METRO_MALLOC(stCnt * sizeof(unsigned char));
            if (s_LineOrders[i].dirs[d].stationIds == NULL || s_LineOrders[i].dirs[d].status == NULL) {
                fprintf(stderr, "内存不足\n");
                CleanupInternal();
                return 0;
            }
            for (int j = 0; j < stCnt; j++) {
                if (p + 2 > ordEnd) {
                    fprintf(stderr, "顺序区块数据不足\n");
                    CleanupInternal();
                    return 0;
                }
                s_LineOrders[i].dirs[d].stationIds[j] = ReadLE16(p);
                p += 2;
            }
            for (int j = 0; j < stCnt; j++) {
                if (p + 1 > ordEnd) {
                    fprintf(stderr, "顺序区块数据不足\n");
                    CleanupInternal();
                    return 0;
                }
                s_LineOrders[i].dirs[d].status[j] = *p;
                p += 1;
            }
        }
    }
    
    return 1;
}

/* -------------------------------------------------------------------------
* 公开 API 实现
* ------------------------------------------------------------------------- */
int Metro_Init(const uint8_t *data, size_t dataSize) {
    if (s_Stations != NULL || s_Lines != NULL || s_Records != NULL) {
        CleanupInternal();
    }
    int ret = LoadBinary(data, dataSize);
    if (ret == 0) {
        CleanupInternal();
    }
    return ret;
}

void Metro_Cleanup(void) {
    CleanupInternal();
}

int Metro_GetStationCount(void) {
    return s_StationCount;
}

int Metro_GetLineCount(void) {
    return s_LineCount;
}

int Metro_GetDirectionCount(void) {
    return s_DirCount;
}

int Metro_GetRecordCount(void) {
    return s_RecordCount;
}

const Station_T* Metro_GetStation(int id) {
    if (id < 1 || id > s_StationCount) return NULL;
    return &s_Stations[id - 1];
}

const Line_T* Metro_GetLine(int id) {
    if (id < 1 || id > s_LineCount) return NULL;
    return &s_Lines[id - 1];
}

const Direction_T* Metro_GetDirection(int id) {
    if (id < 1 || id > s_DirCount) return NULL;
    return &s_Directions[id - 1];
}

int Metro_FindStationByName(const char *name) {
    if (name == NULL) return -1;
    for (int i = 0; i < s_StationCount; i++) {
        if (strcmp(s_Stations[i].name, name) == 0)
            return s_Stations[i].id;
    }
    return -1;
}

int* Metro_FindStationsByPinyin(const char *prefix, int *count) {
    int *results = NULL;
    int found = 0;
    if (prefix == NULL) {
        if (count != NULL) *count = 0;
        return NULL;
    }
    int len = (int)strlen(prefix);
    for (int i = 0; i < s_StationCount; i++) {
        if (strncasecmp(s_Stations[i].pinyin, prefix, len) == 0) {
            results = (int*)METRO_REALLOC(results, (found + 1) * sizeof(int));
            if (results == NULL) {
                if (count != NULL) *count = 0;
                return NULL;
            }
            results[found++] = s_Stations[i].id;
        }
    }
    if (count != NULL) *count = found;
    return results;
}

const RouteRecord_T* Metro_FindRoute(int startId, int endId) {
    RouteKey_T key = { startId, endId };
    RouteIndex_T *entry = NULL;
    HASH_FIND(hh, s_IndexMap, &key, sizeof(RouteKey_T), entry);
    return (entry != NULL) ? entry->record : NULL;
}

void Metro_PrintRoute(const RouteRecord_T *record) {
    if (record == NULL) {
        printf("未找到该路线！\n");
        return;
    }
    const Station_T *sStart = Metro_GetStation(record->start);
    const Station_T *sEnd   = Metro_GetStation(record->end);
    if (sStart == NULL || sEnd == NULL) {
        printf("无效的站点ID\n");
        return;
    }
    printf("[%s] -> [%s]\n", sStart->name, sEnd->name);
    printf("  距离: %.3f km\n", record->dist / 1000.0);
    printf("  时间: %.1f 分钟\n", record->time / 100.0);
    printf("  票价: %.2f 元\n", record->fare / 100.0);
    printf("  换乘: %d 次 | 总站数: %d 站\n", record->transfers, record->totalStops);
    printf("  分段数: %d\n", record->segCount);
    for (int i = 0; i < record->segCount; i++) {
        const RouteSegment_T *seg = &record->segs[i];
        const Station_T *s1 = Metro_GetStation(seg->start);
        const Line_T *line = Metro_GetLine(seg->line);
        const Direction_T *dir = Metro_GetDirection(seg->dir);
        const Station_T *s2 = Metro_GetStation(seg->end);
        if (s1 == NULL || line == NULL || dir == NULL || s2 == NULL) {
            printf("   第%d段: 数据无效\n", i+1);
            continue;
        }
        printf("   第%d段: %s -> 坐 %s(开往%s方向) %d 站 -> %s\n",
            i+1, s1->name, line->name, dir->name, seg->stops, s2->name);
    }
}

void Metro_PrintAllLines(void) {
    printf("\n线路列表：\n");
    for (int i = 0; i < s_LineCount; i++) {
        const char *prefix = "";
        if (strcmp(s_Lines[i].city, s_KeyCity) != 0) {
            prefix = s_Lines[i].city;
        }
        if (strlen(prefix) == 0) {
            printf("%d. %s (颜色: #%06X)\n", i+1, s_Lines[i].name, s_Lines[i].color);
        } else {
            printf("%d. %s%s (颜色: #%06X)\n", i+1, prefix, s_Lines[i].name, s_Lines[i].color);
        }
    }
}

void Metro_PrintLineStations(const char *lineName) {
    if (lineName == NULL) return;
    int lid = -1;
    
    /* 先直接匹配name（优先key_city） */
    for (int i = 0; i < s_LineCount; i++) {
        if (strcmp(s_Lines[i].name, lineName) == 0) {
            if (strcmp(s_Lines[i].city, s_KeyCity) == 0) {
                lid = i + 1;
                break;
            } else if (lid == -1) {
                lid = i + 1;
            }
        }
    }
    /* 若未找到，尝试匹配 "city+name" */
    if (lid == -1) {
        for (int i = 0; i < s_LineCount; i++) {
            char full[256];
            snprintf(full, sizeof(full), "%s%s", s_Lines[i].city, s_Lines[i].name);
            if (strcmp(full, lineName) == 0) {
                lid = i + 1;
                break;
            }
        }
    }
    if (lid == -1) {
        printf("未找到线路: %s\n", lineName);
        return;
    }
    
    if (lid > s_LineCount || s_LineOrders == NULL) {
        printf("该线路暂无顺序信息\n");
        return;
    }
    
    LineOrder_T *order = &s_LineOrders[lid - 1];
    printf("线路 [%s] 途经站点 (共%d个方向):\n", lineName, order->dirCount);
    for (int d = 0; d < order->dirCount; d++) {
        printf("  方向: %s\n", order->dirs[d].dirName);
        for (int i = 0; i < order->dirs[d].stationCount; i++) {
            int sid = order->dirs[d].stationIds[i];
            const Station_T *st = Metro_GetStation(sid);
            if (st == NULL) continue;
            printf("    %s", st->name);
            if (order->dirs[d].status[i] == 1) {
                printf(" [暂缓开通]");
            }
            if (i < order->dirs[d].stationCount - 1) {
                printf(", ");
            }
        }
        printf("\n");
    }
}

/* -------------------------------------------------------------------------
* 打印站点详情（含线路暂缓状态）
* ------------------------------------------------------------------------- */
void Metro_PrintStationDetail(const char *stationName) {
    if (stationName == NULL) {
        printf("站点名称无效\n");
        return;
    }
    
    /* 查找站点ID */
    int stationId = Metro_FindStationByName(stationName);
    if (stationId == -1) {
        printf("未找到站点: %s\n", stationName);
        return;
    }
    
    const Station_T *st = Metro_GetStation(stationId);
    if (st == NULL) {
        printf("站点数据无效\n");
        return;
    }
    
    /* 打印基本信息 */
    printf("\n========== 站点详情 ==========\n");
    printf("名称: %s\n", st->name);
    printf("拼音: %s\n", st->pinyin);
    printf("经度: %.6f\n", st->lon);
    printf("纬度: %.6f\n", st->lat);
    printf("途经线路数: %d\n", st->lineCount);
    
    /* 遍历所有线路，找出包含该站点的线路和方向 */
    printf("\n途经线路及方向（含暂缓状态）:\n");
    int foundAny = 0;
    for (int i = 0; i < s_LineCount; i++) {
        const Line_T *line = &s_Lines[i];
        const LineOrder_T *order = &s_LineOrders[i];  // 内部变量
        int hasStation = 0;
        
        /* 先检查该线路是否包含该站点（通过线路自带 lineIds 快速判断） */
        for (int k = 0; k < st->lineCount; k++) {
            if (st->lineIds[k] == line->id) {
                hasStation = 1;
                break;
            }
        }
        if (!hasStation) continue;
        
        foundAny = 1;
        printf("  %s (颜色: #%06X)\n", line->name, line->color);
        
        /* 遍历该线路的所有方向，查找该站点 */
        int dirFound = 0;
        for (int d = 0; d < order->dirCount; d++) {
            const LineOrderDir_T *dir = &order->dirs[d];
            for (int s = 0; s < dir->stationCount; s++) {
                if (dir->stationIds[s] == stationId) {
                    const char *statusStr = (dir->status[s] == 1) ? " [暂缓开通]" : " [运营中]";
                    printf("    - 方向: %s  %s\n", dir->dirName, statusStr);
                    dirFound = 1;
                    break;  /* 该方向只出现一次 */
                }
            }
        }
        if (!dirFound) {
            /* 理论上不会发生，因为 lineIds 已包含，但以防数据不一致 */
            printf("    （未找到方向信息，可能是数据异常）\n");
        }
    }
    
    if (!foundAny) {
        printf("  （该站点暂无线路信息）\n");
    }
    printf("================================\n");
}

void Metro_Free(void *ptr) {
    METRO_FREE(ptr);
}
