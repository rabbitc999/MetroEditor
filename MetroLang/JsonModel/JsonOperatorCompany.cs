using System.Collections.Generic;

namespace MetroEditor.MetroLang.JsonModel
{
    /// <summary>
    /// 运营公司定义的 JSON 序列化模型。
    /// </summary>
    public class JsonOperatorCompany
    {
        /// <summary>运营公司 ID</summary>
        public string Id { get; set; }

        /// <summary>公司显示名称</summary>
        public string Name { get; set; }

        /// <summary>公司简称</summary>
        public string ShortName { get; set; }

        /// <summary>英文名称</summary>
        public string EnName { get; set; }

        /// <summary>国家/地区</summary>
        public string Country { get; set; }

        /// <summary>总部所在城市</summary>
        public string City { get; set; }

        /// <summary>成立年份</summary>
        public int? Founded { get; set; }

        /// <summary>官方网站 URL</summary>
        public string Website { get; set; }

        /// <summary>客服热线 / 联系电话</summary>
        public string Hotline { get; set; }

        /// <summary>公司简介</summary>
        public string Description { get; set; }

        /// <summary>LOGO 图片 Base64 编码</summary>
        public string LogoBase64 { get; set; }

        /// <summary>运营的线路 ID 列表</summary>
        public List<string> Lines { get; set; }
    }
}
