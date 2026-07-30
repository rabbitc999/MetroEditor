# 浮生线路图 · 真实版 —— 用真实的地铁站，写一段真实的人生
%IMPORT 'GUANGZHOU.csv' AS STATIONS
%IMPORT 'SHENZHEN.csv' AS STATIONS
%IMPORT 'BEIJING.csv' AS STATIONS
%IMPORT 'SHANGHAI.csv' AS STATIONS

PROGRAM_DEFINE
    KEY_CITY '浮生'
    OTHER_CITY ['广州', '深圳', '北京', '上海']
    VERSION 'V2.0'
    AUTHOR '路过的风'
    CREATE_DATE '2026-07-28'
END_DEFINE

%INCLUDE 'TRANSFER.m'

OPERATION_DEFINE
    TRANSFER_TIME 180
    DWELL_TIME 30
    OUT_STATION_TRANSFER_TIME 300
    ENTRY_TIME 120
END_DEFINE

# ===== 主线：浮生线 =====
LINE_DEFINE FLOAT
    NAME '浮生线'
    COLOR '#4A90D9'
    MAXSPEED 120

    # 方向0：南下追爱（广州 → 深圳）
    DIRECTION_DEFINE FLOAT_0
        NICK_NAME '追光的人'
        STATIONS [
            '广州南站',          # 初到广州，满怀期待
            '公园前',            # 牵手走过公园前
            '体育西路',          # 热恋时逛过的商圈
            '珠江新城',          # 她工作的地方，也曾是未来的方向
            '客村',              # 深夜一起吃过宵夜的村子
            '广州塔',            # 塔下许过愿，后来都散了
            '沥滘',              # 转折点，开始变味
            '深圳北站'           # 一个人南下，重新开始
        ]
        DISTANCE AUTO
    END_DEFINE

    # 方向1：北归家园（深圳 → 北京）
    DIRECTION_DEFINE FLOAT_1
        NICK_NAME '归途'
        STATIONS [
            '深圳北站',          # 三年后，再次出发
            '岗厦北',            # 深圳的繁华，看够了
            '福田',              # 最后一次回望深圳
            '北京西站',          # 回到爸妈身边
            '军事博物馆',        # 和爸爸一起参观过的
            '木樨地',            # 傍晚散步的街道
            '南礼士路',          # 陪妈妈买菜的地方
            '复兴门',            # 重新找到生活的门
            '建国门'             # 把过去锁进城墙里
        ]
        DISTANCE AUTO
    END_DEFINE

    # 方向2：东行重游（北京 → 上海，途径深圳）
    DIRECTION_DEFINE FLOAT_2
        NICK_NAME '释然'
        STATIONS [
            '北京西站',
            '深圳北站',          # 再次路过，已无波澜
            '欢乐谷景区',        # 在深圳，我笑了
            '虹桥火车站',        # 东方之珠
            '南京西路',          # 南京西路的梧桐
            '人民广场',          # 喂鸽子，也喂自己一口自由
            '陆家嘴',            # 看黄浦江，看新的可能
            '上海南站'           # 最后一站，然后回家
        ]
        DISTANCE AUTO
    END_DEFINE

    # 反向方向（用于完整，但故事中不常坐）
    DIRECTION_DEFINE FLOAT_3 REV FLOAT_0
    DIRECTION_DEFINE FLOAT_4 REV FLOAT_1
    DIRECTION_DEFINE FLOAT_5 REV FLOAT_2

END_DEFINE

# ===== 换乘：在深圳北站，与过去的自己相逢 =====
TRANSFER SAME "深圳北站" FLOAT FLOAT

# ===== 情感参数 =====
# 在“客村”和“广州塔”附近设高拥挤，象征关系紧张
CROWD_PROFILE FLOAT_0
    PERIOD 08:00 09:00 2.5 WORKDAY   # 早高峰，心也很堵
    PERIOD 18:00 19:00 2.0 WORKDAY
END_DEFINE

CROWD_PROFILE FLOAT_1
    PERIOD 08:00 09:00 1.2 WORKDAY   # 归途，心很平静
END_DEFINE

# ===== 编译校验 =====
ASSERT STATION_EXISTS("广州南站") AND STATION_EXISTS("深圳北站") AND STATION_EXISTS("北京西站") AND STATION_EXISTS("虹桥火车站") MESSAGE "关键站点缺失，故事无法开启"
ASSERT LINE_EXISTS("FLOAT") MESSAGE "浮生线未定义"

# ===== 叙事输出 =====
ECHO "【浮生线路图 · 真实版】编译完成"
ECHO "============================================"
ECHO "我在广州南站下车，人生地不熟，却遇见了你"
ECHO "公园前牵手，体育西路逛街，珠江新城等你下班"
ECHO "客村的宵夜，广州塔下的誓言，都曾是真的"
ECHO "可沥滘站之后，方向变了，我独自坐上南下的列车"
ECHO "深圳北站，是我给自己的新起点"
ECHO ""
ECHO "三年，说长不长，说短不短"
ECHO "我在岗厦北看够了繁华，在福田最后一次回望"
ECHO "然后北上，北京西站的出站口，爸妈在等我"
ECHO "军事博物馆的木樨地，南礼士路的菜市场"
ECHO "复兴门的春风，建国门的落日——"
ECHO "家，原来从来都没走远"
ECHO ""
ECHO "后来，我又一次经过深圳北站"
ECHO "这一次，我在欢乐谷景区笑出了声"
ECHO "然后向东，上海虹桥站的夕阳洒在脸上"
ECHO "南京西路的梧桐，人民广场的鸽子"
ECHO "陆家嘴的灯火，上海南站的晚风——"
ECHO "我知道，列车还会继续开"
ECHO "但这一次，我不再害怕换乘"
ECHO "============================================"
ECHO "起点：{CITY} | 站点总数：{STATION_COUNT}"
ECHO "线路数：{LINE_COUNT} | 方向数：{DIRECTION_COUNT}"
ECHO "故事还在继续，愿你搭上的每一班列车，都通往心安"