using System;

namespace MetroEditor.Simulator.Algorithms
{
    /// <summary>
    /// 用于 Dijkstra 优先队列的节点状态
    /// </summary>
    public class SimNodeState : IComparable<SimNodeState>
    {
        public string Node { get; }
        public double TotalCost { get; }

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