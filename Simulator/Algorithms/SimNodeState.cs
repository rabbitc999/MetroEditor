using System;

namespace MetroEditor.Simulator.Algorithms
{
    /// <summary>
    /// 用于 Dijkstra 优先队列的节点状态
    /// </summary>
    public class SimNodeState : IComparable<SimNodeState>
    {
        /// <summary>节点标识</summary>
        public string Node { get; }

        /// <summary>到达该节点的累计代价</summary>
        public double TotalCost { get; }

        /// <summary>
        /// 创建节点状态实例。
        /// </summary>
        /// <param name="node">节点标识</param>
        /// <param name="totalCost">累计代价</param>
        public SimNodeState(string node, double totalCost)
        {
            Node = node;
            TotalCost = totalCost;
        }

        public int CompareTo(SimNodeState? other)
        {
            if (other is null) return -1;
            int cmp = TotalCost.CompareTo(other.TotalCost);
            if (cmp != 0) return cmp;
            return string.CompareOrdinal(Node, other.Node);
        }
    }
}