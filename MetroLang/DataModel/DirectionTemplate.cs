// LineTemplate.cs
namespace MetroEditor.MetroLang.DataModel
{
    public class DirectionTemplate
    {
        public string? Alias { get; set; }
        public string? AliasColor { get; set; }
        public bool? IsFuture { get; set; }
        public string? ConnectStation { get; set; }
        public HashSet<string> Attrs { get; set; } = new(); // BRANCH, FAST, LOOP
        public string? DirectionType { get; set; } // inner/outer
        public string? Inherits { get; set; }
    }
}