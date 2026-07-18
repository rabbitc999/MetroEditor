namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 卫生间设施详情。
    /// </summary>
    public class ToiletInfo
    {
        public string Position { get; set; } = "paid_area";
        public bool Accessible { get; set; }
        public string Gender { get; set; } = "mixed";

        public ToiletInfo Clone()
        {
            return new ToiletInfo
            {
                Position = this.Position,
                Accessible = this.Accessible,
                Gender = this.Gender
            };
        }
    }
}