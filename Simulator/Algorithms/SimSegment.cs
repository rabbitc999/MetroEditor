using System.Collections.Generic;

namespace MetroEditor.Simulator.Algorithms
{
    /// <summary>
    /// 表示路径中一个连续的方向段
    /// </summary>
    public class SimSegment
    {
        public string DirKey { get; }
        public List<string> Stations { get; }

        public SimSegment(string dirKey, List<string> stations)
        {
            DirKey = dirKey;
            Stations = stations;
        }
    }
}