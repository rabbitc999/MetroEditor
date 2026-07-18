namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 票务优惠政策详情。
    /// </summary>
    public class PromotionPolicy
    {
        /// <summary>政策唯一标识符（如 "student_card"）</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>政策显示名称（如 "学生优惠"）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>政策详细描述</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>优惠类型：rate（打折）、fixed（定额减免）、fixed_discount（固定票价）</summary>
        public string Type { get; set; } = "rate";

        /// <summary>优惠数值（如 0.5 表示 5 折）</summary>
        public double Value { get; set; }

        /// <summary>适用的线路 ID 列表。为空表示适用于全网</summary>
        public List<string> ApplicableLines { get; set; } = new();

        /// <summary>每日生效开始时间（格式：HH:mm）</summary>
        public string? StartTime { get; set; }

        /// <summary>每日生效结束时间（格式：HH:mm）</summary>
        public string? EndTime { get; set; }

        /// <summary>适用的星期集合（如 ["WORKDAY"]）</summary>
        public List<string> Days { get; set; } = new();

        public PromotionPolicy Clone()
        {
            return new PromotionPolicy
            {
                Id = this.Id,
                Name = this.Name,
                Description = this.Description,
                Type = this.Type,
                Value = this.Value,
                ApplicableLines = new List<string>(this.ApplicableLines),
                StartTime = this.StartTime,
                EndTime = this.EndTime,
                Days = new List<string>(this.Days)
            };
        }
    }
}