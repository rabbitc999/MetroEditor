namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 盲道铺设详情。
    /// </summary>
    public class TactilePavingInfo
    {
        public string Status { get; set; } = "available";

        public TactilePavingInfo Clone()
        {
            return new TactilePavingInfo
            {
                Status = this.Status
            };
        }
    }
}