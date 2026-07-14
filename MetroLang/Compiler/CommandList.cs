using MetroEditor.MetroLang.Compiler.Command;

namespace MetroEditor.MetroLang.Compiler.Compiler
{
    public static class CommandUtils
    {
        public static Dictionary<string, ICommandHandler> GetCommandList()
        {
            Dictionary<string, ICommandHandler> command = new Dictionary<string, ICommandHandler>();
            // ============================================================
            // 1. 全局与基础设施类指令（无块结构）
            // ============================================================
            command["PROGRAM_DEFINE"] = new ProgramDefine();
            command["STATION_STATUS"] = new StationStatus();
            command["OPEN_TIME"] = new OpenTime();

            // ============================================================
            // 2. 新块语法指令（支持 END_DEFINE 闭合）
            // ============================================================
            command["LINE_DEFINE"] = new LineDefine();
            command["DIRECTION_DEFINE"] = new DirectionDefine();
            command["STATION_DEFINE"] = new StationDefine();
            command["PRICE_DEFINE"] = new PriceDefine();
            command["TRANSFER_DEFINE"] = new TransferDefine();
            command["VIRTUAL_TRANSFER_DEFINE"] = new VirtualTransferDefine();
            command["OPERATION_DEFINE"] = new OperationDefine();
            command["HEADWAY_PROFILE"] = new HeadwayProfile();
            command["CROWD_PROFILE"] = new CrowdProfile();
            command["DWELL_TIME_DEFINE"] = new DwellTimeDefine();
            command["LABEL_DEFINE"] = new LabelDefine();
            command["TRANSFER_TYPE"] = new TransferType();
            command["TRANSFER"] = new Transfer();
            command["SPEED_LIMIT_DEFINE"] = new SpeedLimitDefine();
            command["FACILITY_DEFINE"] = new FacilityDefine();
            command["PROMOTION_DEFINE"] = new PromotionDefine();

            // ============================================================
            // 3. 旧语法兼容指令（无块结构，逐步迁移中）
            // ============================================================
            command["STATION_MODE"] = new StationMode();        // 车站运营模式（旧语法）
            command["DEPOT"] = new Depot();                     // 车辆段定义（旧语法）
            command["VIA"] = new Via();                         // 经由方向定义（旧语法）

            return command;
        }
    }
}
