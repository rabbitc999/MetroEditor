using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.PathFinding
{
    // ========================================================================
    // 行程分段模型（用于UI展示）
    // 职责：封装单段连续乘车行程的详细信息，支持界面渲染和交互
    // 核心特征：一段行程内无换乘，包含完整的站点序列
    // ========================================================================
    public class RouteSegment
    {
        #region 核心属性
        /// <summary>起点站名称</summary>
        /// <remarks>本段行程的起始车站，通常与上一段的终点站相同或为换乘站</remarks>
        public string StartStation { get; set; }

        /// <summary>线路ID</summary>
        /// <remarks>格式如"G1"、"B2"等，对应路网中的唯一线路标识</remarks>
        public string LineId { get; set; }

        /// <summary>线路显示名称</summary>
        /// <remarks>用户友好的线路名称，如"1号线"、"八通线"等</remarks>
        public string LineName { get; set; }

        /// <summary>方向描述</summary>
        /// <remarks>如"往四惠东"、"内环"等，描述列车运行方向</remarks>
        public string Direction { get; set; }

        /// <summary>终点站名称</summary>
        /// <remarks>本段行程的终止车站，通常是换乘站或目的地</remarks>
        public string EndStation { get; set; }

        /// <summary>停靠站数量</summary>
        /// <remarks>包含起点和终点，即本段行程中列车停靠的车站总数</remarks>
        public int StationCount { get; set; }

        /// <summary>是否为支线</summary>
        /// <remarks>true表示该段运行在支线上，非主线；影响UI显示样式</remarks>
        public bool IsBranch { get; set; }

        /// <summary>线路昵称颜色</summary>
        /// <remarks>十六进制颜色值（如"#FF0000"），用于UI中线路标识的着色</remarks>
        public string NickColor { get; set; }

        /// <summary>是否为出站换乘</summary>
        /// <remarks>true表示需要在本次换乘中出站（如虚拟换乘、跨站换乘）</remarks>
        public bool IsOutStationTransfer { get; set; }

        /// <summary>方向键（内部标识）</summary>
        /// <remarks>格式如"G1|苹果园|四惠东"，用于内部数据关联和计算</remarks>
        public string DirKey { get; set; }

        /// <summary>站点序列列表</summary>
        /// <remarks>
        /// 按顺序排列的本段行程所有停靠站名称（包含起点和终点）
        /// 用于UI展开显示详细站点列表，支持点击交互
        /// </remarks>
        public List<string> Stations { get; set; } = new();
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化行程分段
        /// </summary>
        /// <param name="startStation">起点站</param>
        /// <param name="lineId">线路ID</param>
        /// <param name="lineName">线路名称</param>
        /// <param name="direction">方向描述</param>
        /// <param name="endStation">终点站</param>
        /// <param name="stationCount">停靠站数</param>
        /// <param name="isBranch">是否支线</param>
        /// <param name="nickColor">线路颜色</param>
        /// <param name="isOutStationTransfer">是否出站换乘</param>
        /// <param name="dirKey">方向键</param>
        public RouteSegment(string startStation, string lineId, string lineName,
                            string direction, string endStation, int stationCount,
                            bool isBranch, string nickColor, bool isOutStationTransfer,
                            string dirKey)
        {
            StartStation = startStation;
            LineId = lineId;
            LineName = lineName;
            Direction = direction;
            EndStation = endStation;
            StationCount = stationCount;
            IsBranch = isBranch;
            NickColor = nickColor;
            IsOutStationTransfer = isOutStationTransfer;
            DirKey = dirKey;
            Stations = new List<string>(); // 初始化空列表，后续填充
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 获取行程描述文本（用于简要显示）
        /// </summary>
        /// <returns>格式化的行程描述，如"1号线（往四惠东）：苹果园 → 四惠东"</returns>
        public string GetSummary()
        {
            return $"{LineName}（{Direction}）：{StartStation} → {EndStation}";
        }

        /// <summary>
        /// 获取详细站点列表文本（用于展开显示）
        /// </summary>
        /// <returns>用箭头连接的站点序列，如"苹果园 → 古城 → ... → 四惠东"</returns>
        public string GetStationListText()
        {
            if (Stations == null || Stations.Count == 0)
                return $"{StartStation} → {EndStation}";

            return string.Join(" → ", Stations);
        }

        /// <summary>
        /// 判断是否为空段（无效行程）
        /// </summary>
        /// <returns>true表示无效行程</returns>
        public bool IsEmpty()
        {
            return string.IsNullOrEmpty(LineId) ||
                   string.IsNullOrEmpty(StartStation) ||
                   string.IsNullOrEmpty(EndStation);
        }

        /// <summary>
        /// 获取行程长度（站点数）
        /// </summary>
        /// <returns>站点数量，若列表为空则返回StationCount属性值</returns>
        public int GetActualStationCount()
        {
            return Stations?.Count ?? StationCount;
        }
        #endregion

        #region 扩展属性（计算属性）
        /// <summary>
        /// 行程方向缩写（用于紧凑显示）
        /// </summary>
        /// <remarks>如"苹果园→四惠东"，省略线路名称</remarks>
        public string ShortDirection => $"{StartStation}→{EndStation}";

        /// <summary>
        /// 是否为环线行程
        /// </summary>
        /// <remarks>通过方向键判断是否包含"LOOP"标识</remarks>
        public bool IsLoop => DirKey?.Contains("LOOP") == true;

        /// <summary>
        /// 行程类型标识
        /// </summary>
        /// <remarks>返回"支线"、"主线"或"环线"等描述</remarks>
        public string SegmentType
        {
            get
            {
                if (IsLoop) return "环线";
                if (IsBranch) return "支线";
                return "主线";
            }
        }
        #endregion
    }
}