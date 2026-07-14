namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 换乘模板实例（内部使用）。
    /// </summary>
    public class TransferTemplate
    {
        /// <summary>关联的换乘类型 ID</summary>
        public string TypeId { get; set; } = string.Empty;

        /// <summary>应用此模板的车站 ID</summary>
        public string Station { get; set; } = string.Empty;

        /// <summary>应用此模板的线路 ID</summary>
        public string LineId { get; set; } = string.Empty;
    }
}
