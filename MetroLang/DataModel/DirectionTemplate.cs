<<<<<<< HEAD
﻿// LineTemplate.cs
namespace MetroEditor.MetroLang.DataModel
{
    /// <summary>
    /// 方向定义模板，保存创建方向时可继承的默认标记和连接站。
    /// </summary>
    public class DirectionTemplate
    {
        /// <summary>是否为远期规划方向。</summary>
        public bool? IsFuture { get; set; }
        /// <summary>与其他方向关联的连接站。</summary>
        public string? ConnectStation { get; set; }
        /// <summary>模板属性集合，例如 LOOP。</summary>
        public HashSet<string> Attrs { get; set; } = new(); // LOOP
        /// <summary>环线方向类型：inner 或 outer。</summary>
        public string? DirectionType { get; set; } // inner/outer
        /// <summary>继承的父模板 ID。</summary>
        public string? Inherits { get; set; }
    }
}
=======
﻿// LineTemplate.cs
namespace MetroEditor.MetroLang.DataModel
{
    public class DirectionTemplate
    {
        public bool? IsFuture { get; set; }
        public string? ConnectStation { get; set; }
        public HashSet<string> Attrs { get; set; } = new(); // LOOP
        public string? DirectionType { get; set; } // inner/outer
        public string? Inherits { get; set; }
    }
}
>>>>>>> 68704bec0197aa2f176e12f57e1de8fb0a964814
