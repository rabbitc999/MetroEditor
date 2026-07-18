namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 定义两站之间的固定附加费。
    /// </summary>
    public class SurchargeItem
    {
        /// <summary>起始节点（通常是车站 ID）</summary>
        public string NodeA { get; set; } = string.Empty;

        /// <summary>到达节点</summary>
        public string NodeB { get; set; } = string.Empty;

        /// <summary>附加费用金额</summary>
        public double Price { get; set; }

        public SurchargeItem Clone()
        {
            return new SurchargeItem
            {
                NodeA = this.NodeA,
                NodeB = this.NodeB,
                Price = this.Price
            };
        }
    }
}