using System.Collections.Generic;

namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 运营公司定义（如广州地铁集团有限公司）。
    /// 描述某家运营公司的基础信息及其运营的线路，主要用于前端展示与查询，不参与路径规划或票价计算。
    /// </summary>
    public class OperatorCompanyInfo
    {
        /// <summary>公司显示名称（如 "广州地铁集团有限公司"）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>公司简称（如 "广州地铁"）</summary>
        public string? ShortName { get; set; }

        /// <summary>英文名称</summary>
        public string? EnName { get; set; }

        /// <summary>国家/地区</summary>
        public string? Country { get; set; }

        /// <summary>总部所在城市（默认继承 PROGRAM 的 KEY_CITY）</summary>
        public string? City { get; set; }

        /// <summary>成立年份</summary>
        public int? Founded { get; set; }

        /// <summary>官方网站 URL</summary>
        public string? Website { get; set; }

        /// <summary>客服热线 / 联系电话</summary>
        public string? Hotline { get; set; }

        /// <summary>公司简介</summary>
        public string? Description { get; set; }

        /// <summary>LOGO 图片 Base64 编码</summary>
        public string? LogoBase64 { get; set; }

        /// <summary>运营的线路 ID 列表（可选，为空表示按线路的 operator 归属）</summary>
        public List<string> Lines { get; set; } = new();

        /// <summary>
        /// 深拷贝当前运营公司信息，返回一个独立的副本。
        /// </summary>
        public OperatorCompanyInfo Clone()
        {
            return new OperatorCompanyInfo
            {
                Name = this.Name,
                ShortName = this.ShortName,
                EnName = this.EnName,
                Country = this.Country,
                City = this.City,
                Founded = this.Founded,
                Website = this.Website,
                Hotline = this.Hotline,
                Description = this.Description,
                LogoBase64 = this.LogoBase64,
                Lines = new List<string>(this.Lines)
            };
        }
    }
}
