namespace MetroEditor.Simulator.Algorithms
{
    /// <summary>
    /// 图中的边
    /// </summary>
    public class SimEdge
    {
        public string Node { get; }   // 目标节点（格式：lineId|dirKey|station）
        public int Weight { get; }    // 0 或 1

        public SimEdge(string node, int weight)
        {
            Node = node;
            Weight = weight;
        }
    }
}