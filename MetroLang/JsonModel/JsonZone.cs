using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.JsonModel
{
    public class JsonZone
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string City { get; set; }
        public string Color { get; set; }
        public double[] Center { get; set; }
        public double? Radius { get; set; }
        public List<double[]> Polygon { get; set; }
        public List<string> Stations { get; set; }
        public JsonZoneAttributes Attributes { get; set; }
    }

    public class JsonZoneAttributes
    {
        public List<string> PeakHours { get; set; }
        public double? PopulationDensity { get; set; }
        public double? AvgRent { get; set; }
    }
}
