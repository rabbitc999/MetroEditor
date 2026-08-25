#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>
#include "metro.h"

/* -------------------------------------------------------------------------
* 辅助交互函数
* ------------------------------------------------------------------------- */

/**
 * @brief  从候选站点列表中选择一个
 */
static int SelectStation(int *candidates, int count) {
    if (candidates == NULL || count == 0) return -1;
    if (count == 1) return candidates[0];
    
    printf("找到以下候选站点：\n");
    for (int i = 0; i < count; i++) {
        const Station_T *st = Metro_GetStation(candidates[i]);
        if (st != NULL) {
            printf("%d. %s\n", i + 1, st->name);
        }
    }
    printf("请选择序号 (1-%d): ", count);
    char buf[16];
    if (fgets(buf, sizeof(buf), stdin) == NULL) return -1;
    int sel = atoi(buf) - 1;
    if (sel < 0 || sel >= count) return -1;
    return candidates[sel];
}

/**
 * @brief  判断字符串是否包含字母（用于识别拼音输入）
 */
static int HasAlpha(const char *str) {
    for (const char *p = str; *p != '\0'; p++) {
        if (isalpha(*p)) return 1;
    }
    return 0;
}

/* -------------------------------------------------------------------------
* 主交互菜单
* ------------------------------------------------------------------------- */

static void MenuLoop(void) {
    char input[256];
    while (1) {
        printf("\n=== 地铁查询系统 ===\n");
        printf("1. 查询路线\n");
        printf("2. 查看线路信息\n");
        printf("3. 查询站点详情\n");
        printf("4. 退出\n");
        printf("请选择 (1-4): ");
        if (fgets(input, sizeof(input), stdin) == NULL) break;
        int choice = atoi(input);
        if (choice == 4) break;
        
        switch (choice) {
            case 1: {
                /* ---- 查询路线 ---- */
                printf("请输入起点站名或拼音首字母: ");
                if (fgets(input, sizeof(input), stdin) == NULL) break;
                input[strcspn(input, "\n")] = '\0';
                if (strlen(input) == 0) continue;
                char start[128];
                strcpy_s(start, 128, input);
                
                printf("请输入终点站名或拼音首字母: ");
                if (fgets(input, sizeof(input), stdin) == NULL) break;
                input[strcspn(input, "\n")] = '\0';
                if (strlen(input) == 0) continue;
                char end[128];
                strcpy_s(end, 128, input);
                
                int startId = -1, endId = -1;
                
                /* 起点解析 */
                if (HasAlpha(start)) {
                    int cnt;
                    int *candidates = Metro_FindStationsByPinyin(start, &cnt);
                    if (cnt == 0) {
                        printf("未找到拼音匹配 '%s'\n", start);
                        continue;
                    }
                    startId = SelectStation(candidates, cnt);
                    Metro_Free(candidates);
                    if (startId == -1) {
                        printf("选择无效\n");
                        continue;
                    }
                } else {
                    startId = Metro_FindStationByName(start);
                    if (startId == -1) {
                        printf("未知站点: %s\n", start);
                        continue;
                    }
                }
                
                /* 终点解析 */
                if (HasAlpha(end)) {
                    int cnt;
                    int *candidates = Metro_FindStationsByPinyin(end, &cnt);
                    if (cnt == 0) {
                        printf("未找到拼音匹配 '%s'\n", end);
                        continue;
                    }
                    endId = SelectStation(candidates, cnt);
                    Metro_Free(candidates);
                    if (endId == -1) {
                        printf("选择无效\n");
                        continue;
                    }
                } else {
                    endId = Metro_FindStationByName(end);
                    if (endId == -1) {
                        printf("未知站点: %s\n", end);
                        continue;
                    }
                }
                
                const RouteRecord_T *rec = Metro_FindRoute(startId, endId);
                Metro_PrintRoute(rec);
                break;
            }
            case 2: {
                /* ---- 查看线路信息 ---- */
                Metro_PrintAllLines();
                printf("\n请输入线路名称 (或输入0返回): ");
                if (fgets(input, sizeof(input), stdin) == NULL) break;
                input[strcspn(input, "\n")] = '\0';
                if (strcmp(input, "0") == 0) continue;
                if (strlen(input) > 0) {
                    Metro_PrintLineStations(input);
                }
                break;
            }
            case 3: {
                printf("请输入站点名称或拼音首字母: ");
                if (fgets(input, sizeof(input), stdin) == NULL) break;
                input[strcspn(input, "\n")] = '\0';
                if (strlen(input) == 0) continue;
                
                int stationId = -1;
                if (HasAlpha(input)) {
                    int cnt;
                    int *candidates = Metro_FindStationsByPinyin(input, &cnt);
                    if (cnt == 0) {
                        printf("未找到拼音匹配 '%s'\n", input);
                        continue;
                    }
                    stationId = SelectStation(candidates, cnt);
                    Metro_Free(candidates);
                    if (stationId == -1) {
                        printf("选择无效\n");
                        continue;
                    }
                } else {
                    stationId = Metro_FindStationByName(input);
                    if (stationId == -1) {
                        printf("未知站点: %s\n", input);
                        continue;
                    }
                }
                const Station_T *st = Metro_GetStation(stationId);
                if (st != NULL) {
                    Metro_PrintStationDetail(st->name);
                }
                break;
            }
        default:
            printf("无效选择，请重试。\n");
        }
    }
}

/* -------------------------------------------------------------------------
* 主函数
* ------------------------------------------------------------------------- */

int main(int argc, char **argv) {
    char *file_name = NULL;
    
    if (argc >= 2) {
        file_name = argv[1];
    } else {
        file_name = "E:\\Desktop\\Workspace\\MetroEditor\\Example\\routes.bin";
    }
    
    FILE *fp = fopen(file_name, "rb");
    if (fp == NULL) {
        perror("打开文件失败");
        return 1;
    }
    fseek(fp, 0, SEEK_END);
    long fileSize = ftell(fp);
    rewind(fp);
    uint8_t *data = (uint8_t*)malloc(fileSize);
    if (data == NULL) {
        perror("内存分配失败");
        fclose(fp);
        return 1;
    }
    size_t readBytes = fread(data, 1, fileSize, fp);
    fclose(fp);
    if (readBytes != (size_t)fileSize) {
        fprintf(stderr, "读取文件内容不完整\n");
        free(data);
        return 1;
    }
    
    if (!Metro_Init(data, (size_t)fileSize)) {
        fprintf(stderr, "初始化失败\n");
        free(data);
        return 1;
    }
    free(data);   /* 内部已复制所需数据，可立即释放 */
    
    printf("加载成功！共 %d 条路线，%d 个站点，%d 条线路，%d 个方向。\n",
        Metro_GetRecordCount(),
        Metro_GetStationCount(),
        Metro_GetLineCount(),
        Metro_GetDirectionCount());
    
    MenuLoop();
    
    Metro_Cleanup();
    return 0;
}
