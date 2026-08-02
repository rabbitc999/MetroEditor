namespace MetroEditor.Simulator.Algorithms
{
    /// <summary>
    /// 图中的边
    /// </summary>
    public class SimEdge
    {
        /// <summary>目标节点标识（格式：lineId|dirKey|station）</summary>
        public string Node { get; }   // 目标节点（格式：lineId|dirKey|station）

        /// <summary>边权重（0 表示换乘边，1 表示运行边）</summary>
        public int Weight { get; }    // 0 或 1

        /// <summary>
        /// 创建一条图边。
        /// </summary>
        /// <param name="node">目标节点标识</param>
        /// <param name="weight">边权重（0 或 1）</param>
        public SimEdge(string node, int weight)
        {
            Node = node;
            Weight = weight;
        }
    }
}