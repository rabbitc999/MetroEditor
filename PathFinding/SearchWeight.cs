using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.PathFinding
{
    // ========================================================================
    // 路径搜索权重配置类
    // 职责：定义多目标路径优化的权重系数，支持从注册表加载用户偏好
    // 核心概念：将复杂的路径选择问题转化为加权单目标优化问题
    // ========================================================================
    public class SearchWeight
    {
        #region 权重属性
        /// <summary>距离权重：影响路径对物理距离的敏感度</summary>
        /// <remarks>单位：秒/米。典型值0.02表示每公里增加20秒权重</remarks>
        public double LengthWeight { get; }

        /// <summary>换乘权重：影响路径对换乘次数的敏感度</summary>
        /// <remarks>单位：秒/次。典型值0.5表示每次换乘增加30秒权重</remarks>
        public double TransferWeight { get; }

        /// <summary>票价权重：影响路径对出行成本的敏感度</summary>
        /// <remarks>单位：秒/元。典型值1.0表示每元票价增加1秒权重</remarks>
        public double MoneyWeight { get; }

        /// <summary>时间权重：影响路径对总耗时的敏感度</summary>
        /// <remarks>单位：无量纲。典型值0.01表示每秒增加0.01权重</remarks>
        public double TimeWeight { get; }

        /// <summary>站点权重：影响路径对停靠站数的敏感度</summary>
        /// <remarks>单位：秒/站。典型值0.1表示每站增加6秒权重</remarks>
        public double StationWeight { get; }
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化权重配置
        /// </summary>
        /// <param name="lengthWeight">距离权重</param>
        /// <param name="transferWeight">换乘权重</param>
        /// <param name="moneyWeight">票价权重</param>
        /// <param name="timeWeight">时间权重</param>
        /// <param name="stationWeight">站点权重</param>
        public SearchWeight(double lengthWeight, double transferWeight,
                            double moneyWeight, double timeWeight, double stationWeight)
        {
            LengthWeight = lengthWeight;
            TransferWeight = transferWeight;
            MoneyWeight = moneyWeight;
            TimeWeight = timeWeight;
            StationWeight = stationWeight;
        }
        #endregion

        #region 注册表持久化
        /// <summary>
        /// 从Windows注册表加载权重配置
        /// </summary>
        /// <returns>权重配置对象，加载失败时返回默认"Suitable"配置</returns>
        /// <remarks>
        /// 注册表路径：HKEY_CURRENT_USER\Software\MetroEditor
        /// 值名称：LengthWeight, TransferWeight, MoneyWeight, TimeWeight, StationWeight
        /// 
        /// 异常处理策略：
        /// - 注册表项不存在：返回默认配置
        /// - 读取失败（权限/类型错误）：捕获异常并返回默认配置
        /// - 类型转换失败：使用默认值（double?转换失败时返回null）
        /// 
        /// 设计考虑：权重配置是用户偏好，不应因读取失败而影响程序运行
        /// </remarks>
        public static SearchWeight LoadFromRegistry()
        {
            try
            {
                // 打开MetroEditor配置项
                using var registryKey = Registry.CurrentUser.OpenSubKey(@"Software\MetroEditor");
                if (registryKey == null)
                    return Suitable; // 配置项不存在，使用默认

                // 读取各权重值，类型转换失败时使用默认值
                double length = registryKey.GetValue("LengthWeight") as double? ?? 0.0;
                double transfer = registryKey.GetValue("TransferWeight") as double? ?? 0.0;
                double money = registryKey.GetValue("MoneyWeight") as double? ?? 100.0;
                double time = registryKey.GetValue("TimeWeight") as double? ?? 1.0;
                double station = registryKey.GetValue("StationWeight") as double? ?? 1.0;

                return new SearchWeight(length, transfer, money, time, station);
            }
            catch
            {
                // 任何异常都返回默认配置，确保程序稳定性
                return Suitable;
            }
        }
        #endregion

        #region 预设配置
        /// <summary>
        /// 最短路径预设：仅考虑物理距离，忽略其他因素
        /// 适用场景：工程分析、距离敏感性测试
        /// </summary>
        public static SearchWeight Shortest => new(1, 0, 0, 0, 0);

        /// <summary>
        /// 合适路径预设：平衡票价和时间，适合日常通勤
        /// 适用场景：大多数用户的默认选择
        /// </summary>
        public static SearchWeight Suitable => new(0, 0, 1, 1, 0);
        #endregion
    }
}