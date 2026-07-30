#ifndef __METRO_H
#define __METRO_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* -------------------------------------------------------------------------
* 公开数据结构（只读）
* ------------------------------------------------------------------------- */

/** 站点信息 */
typedef struct __Station_T {
    int         id;          /* 站点ID（从1开始） */
    char       *name;        /* 站点名称（UTF-8） */
    char       *pinyin;      /* 拼音（小写，无空格） */
    float       lon;         /* 经度 */
    float       lat;         /* 纬度 */
    int         lineCount;   /* 途经线路数量 */
    int        *lineIds;     /* 线路ID数组（长度lineCount） */
} Station_T;

/** 线路信息 */
typedef struct __Line_T {
    int         id;          /* 线路ID（从1开始） */
    char       *name;        /* 线路名称（如"1号线"） */
    uint32_t    color;       /* 颜色（RGB，如0xFF0000） */
    char       *city;        /* 所属城市 */
} Line_T;

/** 方向信息（方向名称） */
typedef struct __Direction_T {
    int         id;          /* 方向ID（从1开始） */
    char       *name;        /* 方向名称（如"内环"） */
} Direction_T;

/** 路线分段信息 */
typedef struct __RouteSegment_T {
    int         start;       /* 起始站点ID */
    int         line;        /* 线路ID */
    int         dir;         /* 方向ID */
    int         stops;       /* 该段乘坐站数 */
    int         end;         /* 结束站点ID */
} RouteSegment_T;

/** 路线记录（完整路线） */
typedef struct __RouteRecord_T {
    int                 start;        /* 起点ID */
    int                 end;          /* 终点ID */
    int                 dist;         /* 距离（米） */
    int                 time;         /* 耗时（0.01分钟） */
    int                 fare;         /* 票价（0.01元） */
    int                 transfers;    /* 换乘次数 */
    int                 totalStops;   /* 总站数 */
    int                 segCount;     /* 分段数 */
    RouteSegment_T     *segs;         /* 分段数组（长度segCount） */
} RouteRecord_T;

/* -------------------------------------------------------------------------
* API 函数
* ------------------------------------------------------------------------- */

/**
 * @brief  从内存中的二进制数据初始化地铁系统
 * @param  data      二进制数据起始指针（必须保持有效直到函数返回）
 * @param  dataSize  数据长度（字节）
 * @retval 1 成功，0 失败
 * @note   内部会复制数据解析所需内容，调用者可随后释放 data 内存
 */
int Metro_Init(const uint8_t *data, size_t dataSize);
    
/**
 * @brief  释放所有内部动态内存，恢复初始状态
 * @retval 无
 */
void Metro_Cleanup(void);

/**
 * @brief  获取站点总数
 * @retval 站点数量
 */
int Metro_GetStationCount(void);

/**
 * @brief  获取线路总数
 * @retval 线路数量
 */
int Metro_GetLineCount(void);

/**
 * @brief  获取方向总数
 * @retval 方向数量
 */
int Metro_GetDirectionCount(void);

/**
 * @brief  获取路线记录总数
 * @retval 路线记录数量
 */
int Metro_GetRecordCount(void);

/**
 * @brief  根据ID获取站点信息（只读指针）
 * @param  id  站点ID（从1开始）
 * @retval 站点指针，若ID无效则返回NULL
 */
const Station_T* Metro_GetStation(int id);

/**
 * @brief  根据ID获取线路信息（只读指针）
 * @param  id  线路ID（从1开始）
 * @retval 线路指针，若ID无效则返回NULL
 */
const Line_T* Metro_GetLine(int id);

/**
 * @brief  根据ID获取方向信息（只读指针）
 * @param  id  方向ID（从1开始）
 * @retval 方向指针，若ID无效则返回NULL
 */
const Direction_T* Metro_GetDirection(int id);

/**
 * @brief  根据站点全名查询站点ID
 * @param  name  站点名称（完全匹配）
 * @retval 站点ID（从1开始），若未找到则返回-1
 */
int Metro_FindStationByName(const char *name);

/**
 * @brief  不区分大小写的前缀比较（用于拼音匹配）
 * @param  cmp0  第一个字符串
 * @param  cmp1  第二个字符串
 * @param  len   比较的最大字符数
 * @retval 0 表示相等，非0表示差异（与strcmp语义一致）
 * @note   仅比较前 len 个字符，且忽略大小写
 */
int Metro_StrCaseCmp(const char *cmp0, const char *cmp1, int len);

/**
 * @brief  根据拼音前缀查找所有匹配站点
 * @param  prefix  拼音前缀（不区分大小写）
 * @param  count   输出匹配数量（可为NULL）
 * @retval 动态分配的站点ID数组，调用者需使用 metro_free() 释放（若count为0则返回NULL）
 * @note   返回的数组使用 metro_free() 释放（因为内部使用 stdlib 的 malloc）
 */
int* Metro_FindStationsByPinyin(const char *prefix, int *count);

/**
 * @brief  查询两个站点之间的最优路线
 * @param  startId  起点ID
 * @param  endId    终点ID
 * @retval 路线记录指针（只读），若未找到则返回NULL
 */
const RouteRecord_T* Metro_FindRoute(int startId, int endId);

/**
 * @brief  打印所有线路的列表（含城市前缀）
 * @retval 无
 */
void Metro_PrintAllLines(void);

/**
 * @brief  打印指定线路的途经站点（所有方向）
 * @param  lineName  线路名称（如"1号线"）或"城市+名称"（如"广州1号线"）
 * @retval 无
 * @note   若未找到，则输出错误信息
 */
void Metro_PrintLineStations(const char *lineName);

/**
 * @brief  打印路线记录的详细信息
 * @param  record  路线记录指针（由Metro_FindRoute返回）
 * @retval 无
 * @note   若record为NULL，打印"未找到路线"
 */
void Metro_PrintRoute(const RouteRecord_T *record);

/**
 * @brief  释放由 Metro_FindStationsByPinyin 返回的数组
 * @param  ptr  要释放的指针
 * @retval 无
 * @note   该函数只是简单调用 free()，便于与 API 配套使用
 */
void Metro_Free(void *ptr);
    
/**
 * @brief  打印指定站点的详细信息（包括经纬度、途经线路及方向状态）
 * @param  stationName  站点名称（完全匹配）
 * @retval 无
 */
void Metro_PrintStationDetail(const char *stationName);

/**
 * @brief 获取指定线路的方向数量
 * @param lineId 线路ID（1开始）
 * @return 方向数量，无效则返回0
 */
int Metro_GetLineDirectionCount(int lineId);

/**
 * @brief 获取指定线路某个方向的名称
 * @param lineId  线路ID
 * @param dirIdx  方向索引（0开始）
 * @return 方向名称字符串，若无效则返回NULL
 */
const char* Metro_GetLineDirectionName(int lineId, int dirIdx);

/**
 * @brief 获取指定线路某个方向的车站总数
 * @param lineId  线路ID
 * @param dirIdx  方向索引
 * @return 车站数量，无效返回0
 */
int Metro_GetLineDirectionStationCount(int lineId, int dirIdx);

/**
 * @brief 获取指定线路某个方向指定位置的车站ID
 * @param lineId  线路ID
 * @param dirIdx  方向索引
 * @param idx     车站位置（0开始）
 * @return 车站ID，无效返回-1
 */
int Metro_GetLineDirectionStationId(int lineId, int dirIdx, int idx);

/**
 * @brief 获取指定线路某个方向指定位置的车站状态
 * @param lineId  线路ID
 * @param dirIdx  方向索引
 * @param idx     车站位置
 * @return 1=暂缓开通，0=运营中，无效返回255
 */
unsigned char Metro_GetLineDirectionStationStatus(int lineId, int dirIdx, int idx);

#ifdef __cplusplus
}
#endif

#endif /* __METRO_H */