namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 车站无障碍及服务设施的集合。
    /// </summary>
    public class StationFacilities
    {
        public ToiletInfo? Toilet { get; set; }
        public ToiletInfo? AccessibleToilet { get; set; }
        public List<ElevatorInfo>? Elevators { get; set; }
        public WideGateInfo? WideGate { get; set; }
        public List<EscalatorInfo>? Escalators { get; set; }
        public TactilePavingInfo? TactilePaving { get; set; }

        public StationFacilities Clone()
        {
            return new StationFacilities
            {
                Toilet = this.Toilet?.Clone(),
                AccessibleToilet = this.AccessibleToilet?.Clone(),
                Elevators = this.Elevators?.Select(e => e.Clone()).ToList(),
                WideGate = this.WideGate?.Clone(),
                Escalators = this.Escalators?.Select(e => e.Clone()).ToList(),
                TactilePaving = this.TactilePaving?.Clone()
            };
        }
    }
}