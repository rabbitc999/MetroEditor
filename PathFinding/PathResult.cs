using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.PathFinding
{
    // ========================================================================
    // 路径规划结果模型
    // 职责：封装路径搜索算法的完整输出，包含路径拓扑、代价评估和票价信息
    // 核心特征：不可变数据结构，确保结果的一致性和线程安全性
    // ========================================================================
    public class PathResult
    {
        #region 核心结果属性
        /// <summary>换乘次数</summary>
        /// <remarks>
        /// 路径中方向切换的次数，不包括起点和终点的虚拟换乘
        /// 用于评估路径的便捷性，换乘次数越少通常越受欢迎
        /// </remarks>
        public int Transfers { get; set; }

        /// <summary>路径节点序列</summary>
        /// <remarks>
        /// 按顺序排列的节点标识列表，格式为"方向键|车站名"
        /// 示例：["G1|苹果园", "G1|古城", ..., "G1|四惠东"]
        /// 包含起点、所有中途站和终点，是路径的拓扑核心
        /// </remarks>
        public List<string> Path { get; set; }

        /// <summary>综合代价值</summary>
        /// <remarks>
        /// 根据SearchWeight计算的综合评估分数，值越小表示路径越优
        /// 计算公式：Cost = Σ(权重 × 指标)，包含距离、时间、票价、换乘等因素
        /// 用于路径搜索算法的排序和选择
        /// </remarks>
        public double Cost { get; set; }

        /// <summary>常规总票价（元）</summary>
        /// <remarks>
        /// 基于标准票价规则计算的总费用，不含特殊线路附加费
        /// 适用于普通地铁线路的计费场景
        /// </remarks>
        public double TotalMoney { get; set; }

        /// <summary>特殊线路附加费（元）</summary>
        /// <remarks>
        /// 机场线、磁悬浮等特殊线路的额外费用
        /// 当路径包含特殊线路时，总票价 = TotalMoney + SpecialMoney
        /// 用于区分不同等级的服务定价
        /// </remarks>
        public double SpecialMoney { get; set; }
        #endregion

        #region 扩展汇总属性（计算属性）
        /// <summary>总票价（元）</summary>
        /// <remarks>常规票价与特殊附加费之和，即实际支付金额</remarks>
        public double ActualTotalMoney => TotalMoney + SpecialMoney;

        /// <summary>路径是否有效</summary>
        /// <remarks>当路径包含至少两个节点时视为有效</remarks>
        public bool IsValid => Path != null && Path.Count >= 2;

        /// <summary>起点站名称</summary>
        /// <remarks>从路径第一个节点解析得到</remarks>
        public string StartStation
        {
            get
            {
                if (!IsValid) return string.Empty;
                var parts = GraphKeyUtils.ParseNode(Path.First());
                return parts.Length > 1 ? parts[1] : string.Empty;
            }
        }

        /// <summary>终点站名称</summary>
        /// <remarks>从路径最后一个节点解析得到</remarks>
        public string EndStation
        {
            get
            {
                if (!IsValid) return string.Empty;
                var parts = GraphKeyUtils.ParseNode(Path.Last());
                return parts.Length > 1 ? parts[1] : string.Empty;
            }
        }

        /// <summary>总站点数</summary>
        /// <remarks>路径中实际停靠的车站数量，包含起点和终点</remarks>
        public int StationCount => Path?.Count ?? 0;

        /// <summary>路径长度（节点间边数）</summary>
        /// <remarks>站点数减1，表示乘坐的区间数量</remarks>
        public int EdgeCount => Math.Max(0, StationCount - 1);
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化路径规划结果
        /// </summary>
        /// <param name="transfers">换乘次数</param>
        /// <param name="cost">综合代价值</param>
        /// <param name="path">路径节点序列</param>
        public PathResult(int transfers, double cost, List<string> path)
        {
            Transfers = transfers;
            Cost = cost;
            Path = path ?? new List<string>();
            TotalMoney = 0;
            SpecialMoney = 0;
        }

        /// <summary>
        /// 默认构造函数（用于反序列化等场景）
        /// </summary>
        public PathResult()
        {
            Path = new List<string>();
            Transfers = 0;
            Cost = 0;
            TotalMoney = 0;
            SpecialMoney = 0;
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 获取路径的简要描述
        /// </summary>
        /// <returns>格式化的路径摘要，如"苹果园 → 四惠东（换乘2次）"</returns>
        public string GetSummary()
        {
            if (!IsValid) return "无效路径";
            return $"{StartStation} → {EndStation}（换乘{Transfers}次）";
        }

        /// <summary>
        /// 获取路径的详细节点列表文本
        /// </summary>
        /// <returns>用箭头连接的站点序列</returns>
        public string GetPathText()
        {
            if (!IsValid) return "路径为空";

            var stationNames = Path
                .Select(node => GraphKeyUtils.ParseNode(node))
                .Where(parts => parts.Length > 1)
                .Select(parts => parts[1]);

            return string.Join(" → ", stationNames);
        }

        /// <summary>
        /// 获取票价信息文本
        /// </summary>
        /// <returns>格式化的票价描述</returns>
        public string GetFareText()
        {
            if (SpecialMoney > 0)
                return $"¥{TotalMoney:F2} + ¥{SpecialMoney:F2}（特殊线路）";
            return $"¥{TotalMoney:F2}";
        }

        /// <summary>
        /// 验证路径结果的完整性
        /// </summary>
        /// <returns>验证结果消息，空字符串表示验证通过</returns>
        public string Validate()
        {
            if (Path == null) return "路径节点列表为空";
            if (Path.Count < 2) return "路径至少需要包含两个节点";
            if (Cost < 0) return "综合代价不能为负数";
            if (TotalMoney < 0) return "票价不能为负数";
            if (SpecialMoney < 0) return "特殊票价不能为负数";

            // 验证节点格式
            foreach (var node in Path)
            {
                var parts = GraphKeyUtils.ParseNode(node);
                if (parts.Length < 2) return $"节点格式错误：{node}";
                if (string.IsNullOrEmpty(parts[0])) return $"节点缺少方向键：{node}";
                if (string.IsNullOrEmpty(parts[1])) return $"节点缺少车站名：{node}";
            }

            return string.Empty; // 验证通过
        }

        /// <summary>
        /// 创建路径结果的深拷贝
        /// </summary>
        /// <returns>新的PathResult实例，包含独立的数据副本</returns>
        public PathResult Clone()
        {
            return new PathResult
            {
                Transfers = this.Transfers,
                Cost = this.Cost,
                Path = new List<string>(this.Path ?? new List<string>()),
                TotalMoney = this.TotalMoney,
                SpecialMoney = this.SpecialMoney
            };
        }
        #endregion

        #region 调试支持
        /// <summary>
        /// 获取用于调试的详细信息
        /// </summary>
        /// <returns>包含所有属性的详细字符串表示</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"PathResult:");
            sb.AppendLine($"  起点: {StartStation}");
            sb.AppendLine($"  终点: {EndStation}");
            sb.AppendLine($"  换乘: {Transfers}次");
            sb.AppendLine($"  站点: {StationCount}个");
            sb.AppendLine($"  代价: {Cost:F4}");
            sb.AppendLine($"  票价: {GetFareText()}");
            sb.AppendLine($"  路径: {GetPathText()}");
            return sb.ToString();
        }
        #endregion
    }
}