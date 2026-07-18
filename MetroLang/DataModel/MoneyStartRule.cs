namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 起步票价规则。
    /// </summary>
    public class MoneyStartRule : MoneyRule
    {
        /// <summary>起步票价金额</summary>
        public double Price { get; set; }

        public new MoneyStartRule Clone()
        {
            return new MoneyStartRule
            {
                Price = this.Price
            };
        }
    }
}
