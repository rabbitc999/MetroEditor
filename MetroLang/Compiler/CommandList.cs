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
            // 3. 单指令
            // ============================================================
            command["STATION_MODE"] = new StationMode();
            command["DEPOT"] = new Depot();
            command["VIA"] = new Via();
            command["ECHO"] = new Echo();

            command["DIRECTION_DELETE"] = new DirectionDelete();
            command["DIRECTION_COPY"] = new DirectionCopy();
            command["DIRECTION_RENAME"] = new DirectionRename();
            command["DIRECTION_SPLIT"] = new DirectionSplit();
            command["DIRECTION_MODIFY"] = new DirectionModify();
            command["DIRECTION_REVERSE"] = new DirectionReverse();

            command["LINE_COPY"] = new LineCopy();
            command["LINE_RENAME"] = new LineRename();
            command["LINE_DELETE"] = new LineDelete();
            command["LINE_MODIFY"] = new LineModify();
            command["LINE_MERGE"] = new LineMerge();
            command["LINE_SPLIT"] = new LineSplit();

            command["STATION_COPY"] = new StationCopy();
            command["STATION_RENAME"] = new StationRename();
            command["STATION_DELETE"] = new StationDelete();
            command["STATION_MODIFY"] = new StationModify();

            command["ASSERT"] = new Assert();

            command["LINE_TEMPLATE"] = new LineTemplateCommand();
            command["DIRECTION_TEMPLATE"] = new DirectionTemplateCommand();

            return command;
        }
    }
}
