namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 垂直电梯设施详情。
    /// </summary>
    public class ElevatorInfo
    {
        public string Location { get; set; } = string.Empty;
        public bool Accessible { get; set; } = true;

        public ElevatorInfo Clone()
        {
            return new ElevatorInfo
            {
                Location = this.Location,
                Accessible = this.Accessible
            };
        }
    }
}