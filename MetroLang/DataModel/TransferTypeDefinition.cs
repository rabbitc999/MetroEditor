namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 换乘类型模板定义（内部使用）。
    /// </summary>
    public class TransferTypeDefinition
    {
        /// <summary>JSON 中使用的换乘类型标识</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>换乘状态</summary>
        public string Status { get; set; } = "operating";

        /// <summary>换乘耗时（秒）</summary>
        public double? Time { get; set; }

        /// <summary>是否需要安检</summary>
        public bool SecurityCheck { get; set; } = false;

        public TransferTypeDefinition Clone()
        {
            return new TransferTypeDefinition
            {
                Name = this.Name,
                Status = this.Status,
                Time = this.Time,
                SecurityCheck = this.SecurityCheck
            };
        }
    }
}
