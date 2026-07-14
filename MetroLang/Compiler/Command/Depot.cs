using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // DEPOT 解析：车辆段（停车场）定义
    // 定义线路的车辆段位置及出库时间安排
    // 语法示例：DEPOT LINE1 "车辆段站" LINE1_0 05:30
    // ====================================================================
    internal class Depot : ICommandHandler
    {
        Context context;

        public void Handle(Parser parser, Context context)
        {
            this.context = context;

            // 解析线路ID和车辆段所在车站
            string lineId = parser.ParseId();
            string station = parser.ParseString();

            // 可选的方向键和发车时间
            string directionKey = null;
            if (parser.Peek().TokenType == TokenType.Id)
                directionKey = parser.ParseId();

            TimeSpan? departureTime = null;
            if (parser.Peek().TokenType == TokenType.Number)
            {
                departureTime = parser.ParseClockTime();
            }

            // 创建并添加车辆段信息
            context.Depots.Add(new DepotInfo
            {
                LineId = lineId,
                Station = station,
                DirectionKey = Utils.ConvertToExternalDirKey(context, directionKey),
                DepartureTime = departureTime
            });
        }
    }
}
