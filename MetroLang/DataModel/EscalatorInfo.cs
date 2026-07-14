namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 自动扶梯详情。
    /// </summary>
    public class EscalatorInfo
    {
        public int Quantity { get; set; }
        public string Direction { get; set; } = "both";
    }
}