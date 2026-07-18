namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 宽通道闸机详情。
    /// </summary>
    public class WideGateInfo
    {
        public int Quantity { get; set; }

        public WideGateInfo Clone()
        {
            return new WideGateInfo
            {
                Quantity = this.Quantity
            };
        }
    }
}