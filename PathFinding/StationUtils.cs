using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MetroEditor.MetroLang.Compiler.Context;

namespace MetroEditor.PathFinding
{
    // ========================================================================
    // 车站状态与换乘工具类
    // 职责：统一管理车站运营状态、换乘状态和换乘时间计算
    // 核心功能：状态查询、换乘规则匹配、换乘时间估算
    // ========================================================================
    public static class StationUtils
    {
        #region 车站状态查询
        /// <summary>
        /// 判断指定车站在特定方向上是否处于关闭状态
        /// </summary>
        /// <param name="station">车站名称</param>
        /// <param name="dirKey">方向键（格式："线路ID|起点|终点"）</param>
        /// <param name="data">路网数据</param>
        /// <returns>是否关闭（pending/closed/disable）</returns>
        /// <remarks>
        /// 关闭状态定义：
        /// - pending：待开通（建设中或规划中）
        /// - closed：已关闭（永久停运）
        /// - disable：临时停用（维修或突发事件）
        /// 
        /// 查询逻辑：从StationStatus字典中查找特定车站-线路组合的状态
        /// 未配置状态默认为运营中（false）
        /// </remarks>
        public static bool IsStationLineClosed(string station, string dirKey, ParsedData data)
        {
            var lineId = GraphKeyUtils.GetLineIdFromDirKey(dirKey);
            if (data.StationStatus.TryGetValue((station, lineId), out var status))
                return status == "pending" || status == "closed" || status == "disable";
            return false;
        }

        /// <summary>
        /// 判断给定的状态字符串是否表示关闭状态
        /// </summary>
        /// <param name="status">状态字符串</param>
        /// <returns>是否关闭</returns>
        /// <remarks>
        /// 统一的状态判断方法，用于多处状态检查的一致性
        /// 支持的状态值：pending、closed、disable
        /// </remarks>
        public static bool IsClosed(string status)
            => status == "pending" || status == "closed" || status == "disable";
        #endregion

        #region 换乘状态查询
        /// <summary>
        /// 获取两个方向在特定车站的换乘状态
        /// </summary>
        /// <param name="station">车站名称</param>
        /// <param name="dirKey1">第一个方向键</param>
        /// <param name="dirKey2">第二个方向键</param>
        /// <param name="data">路网数据</param>
        /// <returns>换乘状态字符串</returns>
        /// <remarks>
        /// 换乘状态类型：
        /// - operating：正常换乘（站内换乘）
        /// - out_station：出站换乘（需出闸机）
        /// - pending：待开通换乘
        /// - closed：换乘关闭
        /// 
        /// 匹配逻辑：
        /// 1. 按车站名称筛选
        /// 2. 按线路ID组合匹配（双向匹配：(L1,L2)或(L2,L1)）
        /// 3. 返回第一个匹配规则的状态
        /// 
        /// 默认返回值：operating（未配置换乘规则时）
        /// </remarks>
        public static string GetTransferStatus(string station, string dirKey1, string dirKey2, ParsedData data)
        {
            var line1 = GraphKeyUtils.GetLineIdFromDirKey(dirKey1);
            var line2 = GraphKeyUtils.GetLineIdFromDirKey(dirKey2);

            foreach (var transferRule in data.Transfers)
            {
                if (transferRule.Station == station &&
                    ((transferRule.L1 == line1 && transferRule.L2 == line2) ||
                     (transferRule.L1 == line2 && transferRule.L2 == line1)))
                {
                    return transferRule.Status;
                }
            }
            return "operating"; // 默认正常换乘
        }
        #endregion

        #region 换乘时间计算
        /// <summary>
        /// 获取两个方向在特定车站的换乘时间
        /// </summary>
        /// <param name="station">车站名称</param>
        /// <param name="dirKey1">第一个方向键</param>
        /// <param name="dirKey2">第二个方向键</param>
        /// <param name="data">路网数据</param>
        /// <returns>换乘时间（秒）</returns>
        /// <remarks>
        /// 时间确定优先级：
        /// 1. 换乘规则中显式定义的Time值（最高优先级）
        /// 2. 根据换乘状态使用默认时间：
        ///    - out_station：使用DefaultOutStationTime（出站步行+安检）
        ///    - 其他状态：使用DefaultInStationTime（站内步行）
        /// 
        /// 设计原则：
        /// - 显式配置优先于隐式状态推断
        /// - 出站换乘时间显著长于站内换乘
        /// - 未配置换乘规则时，默认站内换乘
        /// 
        /// 典型时间参考：
        /// - 站内换乘：2-5分钟（120-300秒）
        /// - 出站换乘：10-30分钟（600-1800秒）
        /// </remarks>
        public static double GetInnerTransferTime(string station, string dirKey1, string dirKey2, ParsedData data)
        {
            var line1 = GraphKeyUtils.GetLineIdFromDirKey(dirKey1);
            var line2 = GraphKeyUtils.GetLineIdFromDirKey(dirKey2);

            // 优先使用换乘规则中显式定义的时间
            foreach (var transferRule in data.Transfers)
            {
                if (transferRule.Station == station &&
                    ((transferRule.L1 == line1 && transferRule.L2 == line2) ||
                     (transferRule.L1 == line2 && transferRule.L2 == line1)))
                {
                    if (transferRule.Time.HasValue)
                        return transferRule.Time.Value;
                }
            }

            // 根据换乘状态使用默认时间
            var transferStatus = GetTransferStatus(station, dirKey1, dirKey2, data);
            if (transferStatus == "out_station")
                return data.DefaultOutStationTime;  // 出站换乘：包含步行、安检等
            else
                return data.DefaultInStationTime;   // 站内换乘：仅步行时间
        }
        #endregion
    }
}