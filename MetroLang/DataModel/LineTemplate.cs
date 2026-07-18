// LineTemplate.cs
namespace MetroEditor.MetroLang.DataModel
{
    public class LineTemplate
    {
        public string? Name { get; set; }
        public string? Color { get; set; }
        public string? City { get; set; }
        public int? OpenYear { get; set; }
        public string? Formation { get; set; }
        public double? Maxspeed { get; set; }
        public double? PowerKw { get; set; }
        public double? Brake { get; set; }
        public bool? Disabled { get; set; }
        public string? ImageBase64 { get; set; }
        public string? Link { get; set; }
        public bool? IsSpecial { get; set; }
        public string? Inherits { get; set; }  // 父模板名
    }
}