using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // FACILITY_DEFINE 解析：车站无障碍设施配置
    // 语法示例：
    // FACILITY_DEFINE
    //   TOILET '北京南站' POSITION paid_area ACCESSIBLE GENDER male
    //   ELEVATOR '西直门站' LOCATION 'A口' ACCESSIBLE
    //   WIDE_GATE '东单站' QUANTITY 2
    //   ESCALATOR '国贸站' QUANTITY 4 DIRECTION up
    //   TACTILE_PAVING '三元桥站' STATUS available
    // END_DEFINE
    // ====================================================================
    internal class FacilityDefine : ICommandHandler
    {
        Parser parser;
        Context context;

        public void Handle(Parser parser, Context context)
        {
            this.parser = parser;
            this.context = context;

            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) { break; }

                string subCommand = parser.ParseId().ToUpperInvariant();
                switch (subCommand)
                {
                    case "TOILET":
                    case "ACCESSIBLE_TOILET":
                        // 解析普通卫生间或无障碍卫生间
                        ParseToilet(subCommand == "ACCESSIBLE_TOILET");
                        break;
                    case "ELEVATOR":
                        // 解析垂直电梯
                        ParseElevator();
                        break;
                    case "WIDE_GATE":
                        // 解析宽通道闸机
                        ParseWideGate();
                        break;
                    case "ESCALATOR":
                        // 解析自动扶梯
                        ParseEscalator();
                        break;
                    case "TACTILE_PAVING":
                        // 解析盲道铺设情况
                        ParseTactilePaving();
                        break;
                    default:
                        throw new SyntaxException($"FACILITY_DEFINE 内未知子命令 '{subCommand}'", parser.Peek());
                }
            }
        }

        /// <summary>
        /// 解析卫生间配置（TOILET / ACCESSIBLE_TOILET）
        /// </summary>
        /// <param name="accessible">是否为无障碍卫生间</param>
        private void ParseToilet(bool accessible)
        {
            // 车站名称（字符串字面量）
            string station = parser.ParseString();
            var facilities = GetOrCreateFacilities(station);

            // 期望 POSITION 关键字
            parser.ParseId("POSITION");
            string position = parser.ParseId().ToLowerInvariant();
            if (position != "paid_area" && position != "unpaid_area")
                throw new SyntaxException($"POSITION 只能是 'paid_area' 或 'unpaid_area'，但得到 '{position}'", parser.Peek());

            // 可选：ACCESSIBLE 标志（冗余但允许）
            bool isAccessible = false;
            if (parser.IsPeekId("ACCESSIBLE"))
            {
                parser.ParseId();
                isAccessible = true;
            }

            // 可选：GENDER 属性
            string gender = "mixed";
            if (parser.IsPeekId("GENDER"))
            {
                parser.ParseId();
                gender = parser.ParseId().ToLowerInvariant();
                if (gender != "mixed" && gender != "male" && gender != "female")
                    throw new SyntaxException($"GENDER 只能是 'mixed'、'male' 或 'female'，但得到 '{gender}'", parser.Peek());
            }

            var toiletInfo = new ToiletInfo { Position = position, Accessible = isAccessible, Gender = gender };
            if (accessible)
                facilities.AccessibleToilet = toiletInfo;
            else
                facilities.Toilet = toiletInfo;
        }

        /// <summary>
        /// 解析垂直电梯配置（ELEVATOR）
        /// </summary>
        private void ParseElevator()
        {
            string station = parser.ParseString();
            var facilities = GetOrCreateFacilities(station);

            parser.ParseId("LOCATION");
            string location = parser.ParseString();

            // 可选：ACCESSIBLE 标志
            bool accessible = false;
            if (parser.IsPeekId("ACCESSIBLE"))
            {
                parser.ParseId();
                accessible = true;
            }

            // 延迟初始化电梯列表
            if (facilities.Elevators == null) facilities.Elevators = new List<ElevatorInfo>();
            facilities.Elevators.Add(new ElevatorInfo { Location = location, Accessible = accessible });
        }

        /// <summary>
        /// 解析宽通道闸机配置（WIDE_GATE）
        /// </summary>
        private void ParseWideGate()
        {
            string station = parser.ParseString();
            var facilities = GetOrCreateFacilities(station);

            parser.ParseId("QUANTITY");
            int quantity = parser.ParseInt();
            facilities.WideGate = new WideGateInfo { Quantity = quantity };
        }

        /// <summary>
        /// 解析自动扶梯配置（ESCALATOR）
        /// </summary>
        private void ParseEscalator()
        {
            string station = parser.ParseString();
            var facilities = GetOrCreateFacilities(station);

            parser.ParseId("QUANTITY");
            int quantity = parser.ParseInt();

            // 可选：DIRECTION 属性（up/down/both）
            string direction = "both";
            if (parser.IsPeekId("DIRECTION"))
            {
                parser.ParseId();
                direction = parser.ParseId().ToLowerInvariant();
                if (direction != "up" && direction != "down" && direction != "both")
                    throw new SyntaxException($"DIRECTION 只能是 'up'、'down' 或 'both'，但得到 '{direction}'", parser.Peek());
            }

            // 延迟初始化扶梯列表
            if (facilities.Escalators == null) facilities.Escalators = new List<EscalatorInfo>();
            facilities.Escalators.Add(new EscalatorInfo { Quantity = quantity, Direction = direction });
        }

        /// <summary>
        /// 解析盲道配置（TACTILE_PAVING）
        /// </summary>
        private void ParseTactilePaving()
        {
            string station = parser.ParseString();
            var facilities = GetOrCreateFacilities(station);

            parser.ParseId("STATUS");
            string status = parser.ParseId().ToLowerInvariant();
            if (status != "available" && status != "partial" && status != "none")
                throw new SyntaxException($"STATUS 只能是 'available'、'partial' 或 'none'，但得到 '{status}'", parser.Peek());

            facilities.TactilePaving = new TactilePavingInfo { Status = status };
        }

        /// <summary>
        /// 获取或创建指定车站的设施配置对象
        /// </summary>
        private StationFacilities GetOrCreateFacilities(string station)
        {
            if (!context.Facilities.TryGetValue(station, out var facilities))
            {
                facilities = new StationFacilities();
                context.Facilities[station] = facilities;
            }
            return facilities;
        }
    }
}
