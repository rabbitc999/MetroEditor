using System.Collections.Generic;

namespace MetroEditor.Simulator.Algorithms
{
    /// <summary>
    /// 表示路径中一个连续的方向段
    /// </summary>
    public class SimSegment
    {
        /// <summary>方向键标识</summary>
        public string DirKey { get; }

        /// <summary>该方向段内的站点列表</summary>
        public List<string> Stations { get; }

        /// <summary>
        /// 创建路径分段。
        /// </summary>
        /// <param name="dirKey">方向键标识</param>
        /// <param name="stations">途经站点列表</param>
        public SimSegment(string dirKey, List<string> stations)
        {
            DirKey = dirKey;
            Stations = stations;
        }
    }
}