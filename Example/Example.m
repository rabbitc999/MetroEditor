# 分手线路图 —— 从合肥出发，到广州落幕
PROGRAM_DEFINE
    KEY_CITY '广州'
    OTHER_CITY '合肥'
    VERSION 'V1.0'
    AUTHOR '赶路赴约的人'
    CREATE_DATE '2026-07-18'
END_DEFINE

%INCLUDE 'TRANSFER.m'
%IMPORT 'GUANGZHOU.csv' AS STATIONS
%IMPORT 'HEFEI.csv' AS STATIONS

# 全局基础运营参数
OPERATION_DEFINE
    TRANSFER_TIME 180
    DWELL_TIME 30
    OUT_STATION_TRANSFER_TIME 300
    ENTRY_TIME 120
END_DEFINE

# 粉色专线，从合肥奔赴广州的记忆专线
LINE_DEFINE LOVE
    NAME '分手线路图'
    COLOR '#FF69B4'
    MAXSPEED 100
    DIRECTION_DEFINE LOVE_0
        STATIONS [
            '合肥火车站',            # T221次，17小时硬座，故事启程
            '广州白云站',            # 跨越千里抵达羊城
        ]
        DISTANCE AUTO
        NICK_NAME '记忆方向'
    END_DEFINE
    DIRECTION_DEFINE LOVE_1
    	NICK_COLOR '#8100FF'
        STATIONS [
            '广州白云站',            # 跨越千里抵达羊城
            '棠涌',                  # 藏满细碎日常的街巷
            '新市墟',                # 深夜闲逛的烟火夜晚
            '永泰',                  # 朝夕同住的出租屋
            '京溪南方医院',          # 临时停靠的温柔港湾
            '神舟路',                # 她每日实习的写字楼
            '增城广场'               # 她校园岁月的终点
        ]
        BRANCH
        DISTANCE AUTO
        NICK_NAME 'Ta的城市'
    END_DEFINE
    
	# 反向线路：只剩返程，无法回头
	DIRECTION_DEFINE LOVE_2 REV LOVE_0
	DIRECTION_DEFINE LOVE_3 REV LOVE_1
END_DEFINE

# 编译校验，确认所有回忆站点真实存在
ASSERT LINE_EXISTS("LOVE") MESSAGE "这条奔赴的记忆线路不存在"
ASSERT STATION_EXISTS("合肥火车站") AND STATION_EXISTS("增城广场") MESSAGE "故事起点或终点遗失"

ECHO "【分手线路图】编译完成"
ECHO "========================"
ECHO "两年前可话相识，100天前相恋"
ECHO "为了不异地，我从合肥火车站出发，坐了17小时硬座"
ECHO "到广州白云站，然后走过永泰、京溪南方医院"
ECHO "深夜的新市墟和棠涌，她实习在神舟路"
ECHO "而她的校园终点，叫增城广场"
ECHO ""
ECHO "广州地铁的CROWD值我曾用代码精确计算"
ECHO "却算不出人心变冷的加速度"
ECHO ""
ECHO "那些PENDING（暂缓）的线路"
ECHO "像极了我们说过的未来"
ECHO "真正OPERATING（运营）的"
ECHO "只有2号线和5号线，就像我们真正拥有的"
ECHO "只有这段一起走过的日子"
ECHO ""
ECHO "终点站到了，我该下车了"
ECHO "百年官桥，千年洪圣沙，万年广火"
ECHO "========================"
ECHO "主城市：{CITY} | 记忆线路总数：{LINE_COUNT}"