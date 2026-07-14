using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.PathFinding
{
    // ========================================================================
    // 图节点键工具类
    // 职责：统一定义路网图中方向键和节点键的格式规范与解析逻辑
    // 核心约定：所有图内部的节点标识都遵循"方向键|车站名"的格式
    // ========================================================================
    public static class GraphKeyUtils
    {
        #region 方向键操作（Direction Key）
        /// <summary>
        /// 构建方向键：唯一标识一条运行方向
        /// </summary>
        /// <param name="lineId">线路ID（如"G1"、"B2"）</param>
        /// <param name="start">方向起点站</param>
        /// <param name="end">方向终点站</param>
        /// <returns>方向键字符串，格式："线路ID|起点站|终点站"</returns>
        /// <remarks>
        /// 方向键是路网图的顶层标识，用于区分同一线路的不同运行方向
        /// 示例："G1|苹果园|四惠东" 表示1号线往四惠东方向
        /// 
        /// 设计要点：
        /// - 起点和终点站定义了方向的物理走向
        /// - 环线方向键包含"LOOP"标识（如"G2|西直门|西直门|LOOP"）
        /// - 反向方向使用REV简写（如"G1_1"的反向为"G1_1_REV"）
        /// </remarks>
        public static string MakeDirKey(string lineId, string start, string end)
            => $"{lineId}|{start}|{end}";

        /// <summary>
        /// 解析方向键为组成部分
        /// </summary>
        /// <param name="dirKey">方向键字符串</param>
        /// <returns>字符串数组：[线路ID, 起点站, 终点站]</returns>
        /// <remarks>
        /// 解析逻辑：按'|'字符分割方向键
        /// 预期格式：始终包含3个部分（线路ID、起点、终点）
        /// 
        /// 异常处理：不验证输入格式，调用方需确保传入有效的方向键
        /// 若格式不符，可能导致数组越界或逻辑错误
        /// </remarks>
        public static string[] ParseDirKey(string dirKey)
            => dirKey.Split('|');

        /// <summary>
        /// 从方向键中提取线路ID
        /// </summary>
        /// <param name="dirKey">方向键字符串</param>
        /// <returns>线路ID部分</returns>
        /// <remarks>
        /// 快捷方法，等价于ParseDirKey(dirKey)[0]
        /// 广泛用于按线路筛选规则（如发车间隔、限速等）
        /// </remarks>
        public static string GetLineIdFromDirKey(string dirKey)
            => ParseDirKey(dirKey)[0];
        #endregion

        #region 节点键操作（Node Key）
        /// <summary>
        /// 构建节点键：唯一标识图中的具体节点
        /// </summary>
        /// <param name="dirKey">方向键</param>
        /// <param name="station">车站名称</param>
        /// <returns>节点键字符串，格式："方向键|车站名"</returns>
        /// <remarks>
        /// 节点键是路径搜索的基本单元，表示"在某条运行方向上的某个车站"
        /// 示例："G1|苹果园|四惠东|古城" 表示1号线往四惠东方向上的古城站
        /// 
        /// 设计要点：
        /// - 同一车站在不同方向上被视为不同节点（如换乘站）
        /// - 节点键确保路径搜索能正确处理换乘逻辑
        /// - 方向键部分完整保留，便于回溯路径信息
        /// </remarks>
        public static string MakeNode(string dirKey, string station)
            => $"{dirKey}|{station}";

        /// <summary>
        /// 解析节点键为方向和车站
        /// </summary>
        /// <param name="node">节点键字符串</param>
        /// <returns>字符串数组：[方向键, 车站名]</returns>
        /// <remarks>
        /// 解析逻辑：查找最后一个'|'字符，将其前后部分分割
        /// 示例："G1|苹果园|四惠东|古城" → ["G1|苹果园|四惠东", "古城"]
        /// 
        /// 设计要点：
        /// - 使用LastIndexOf而非Split，避免车站名包含'|'的情况
        /// - 车站名允许包含特殊字符，只要不是'|'
        /// - 方向键部分保持完整，便于后续解析
        /// 
        /// 异常处理：若节点键不包含'|'，将返回整个字符串作为方向键，空字符串作为车站名
        /// </remarks>
        public static string[] ParseNode(string node)
        {
            int lastSeparatorIndex = node.LastIndexOf('|');
            return new[]
            {
                node.Substring(0, lastSeparatorIndex),  // 方向键部分
                node.Substring(lastSeparatorIndex + 1)   // 车站名部分
            };
        }
        #endregion
    }
}