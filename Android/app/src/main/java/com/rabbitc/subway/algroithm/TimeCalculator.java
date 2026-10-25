package com.rabbitc.subway.algroithm;

import com.rabbitc.subway.data.parse.CrowdRule;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.SearchWeight;
import com.rabbitc.subway.data.parse.SpeedLimit;
import com.rabbitc.subway.data.parse.HeadwayRule;
import com.rabbitc.subway.utils.GraphKeyUtils;
import com.rabbitc.subway.utils.StationUtils;

import java.util.Calendar;
import java.util.List;
import java.util.Map;

/**
 * 时间计算工具类，用于计算列车区间运行时间及完整路径总时间。
 */
public class TimeCalculator {


    // ---------- 速度-加速度映射表 ----------
    private static final int[] SPEED_POINTS = {80, 100, 120, 160};
    private static final double[] ACCEL_POINTS = {0.55, 0.52, 0.48, 0.38};
    private static final double DECELERATION = 1.0;

    // ---------- 公共API：总时间计算 ----------
    //public static double computeTotalTime(List<String> path, MetroGraph metroGraph,
    //                                      SearchWeight weight) {
    //    return computeTotalTime(path, metroGraph, weight, System.currentTimeMillis());
    //}

    /** 汇总列车运行、停站、换乘、进站和候车时间，返回秒数。 */
    public static double computeTotalTime(List<String> path, MetroGraph metroGraph,
                                          SearchWeight weight, long travelTimeMillis) {
        // 总耗时由运行、换乘、停站、进站和起点候车五部分组成，单位统一为秒。
        double trainTime = computeTrainTime(path, metroGraph);
        double transferTime = computeTotalTransferTime(path, travelTimeMillis);
        double stopTime = computeStopTime(path, metroGraph);
        double entryTime = DataCache.getInstance().getAvgEntryTime();

        // ======== 新增：起点站等待时间 ========
        double startWaitTime = 0.0;
        if (!path.isEmpty()) {
            String firstNode = path.get(0);
            String[] parts = GraphKeyUtils.parseNode(firstNode);
            String dirKey = parts[0];
            DataCache cache = DataCache.getInstance();
            startWaitTime = computeTransferWaitTime(dirKey, travelTimeMillis, cache);
        }

        return trainTime + transferTime + stopTime + entryTime * 2 + startWaitTime;
    }

    // ---------- 子方法：行驶时间 ----------
    private static double computeTrainTime(List<String> path, MetroGraph metroGraph) {
        double trainTime = 0.0;
        String currentDirKey = null;
        String prevStation = null;

        for (int i = 0; i < path.size(); i++) {
            String[] parts = GraphKeyUtils.parseNode(path.get(i));
            String dirKey = parts[0];
            String station = parts[1];

            if (i == 0) {
                currentDirKey = dirKey;
                prevStation = station;
                continue;
            }

            if (!dirKey.equals(currentDirKey)) {
                currentDirKey = dirKey;
                prevStation = station;
                continue;
            }

            // 方向变化表示换乘，不计入列车运行距离；下一站重新作为新方向的起点。
            double distance = metroGraph.getDistanceInDirection(dirKey, prevStation, station);
            if (distance > 0) {
                String lineId = GraphKeyUtils.getLineIdFromDirKey(dirKey);
                int maxSpeed = getLineMaxSpeed(lineId);
                trainTime += computeSegmentTime(distance, maxSpeed, lineId, dirKey, prevStation, station);
            }
            prevStation = station;
        }
        return trainTime;
    }

    private static int getLineMaxSpeed(String lineId) {
        DataCache cache = DataCache.getInstance();
        if (cache.getLineMaxSpeedMap() != null) {
            return cache.getLineMaxSpeedMap().getOrDefault(lineId, 80);
        }
        return 80;
    }

    // ---------- 子方法：换乘时间 ----------
    public static double computeTotalTransferTime(List<String> path, long travelTimeMillis) {
        DataCache cache = DataCache.getInstance();
        double total = 0.0;
        String prevDir = null;
        for (int i = 0; i < path.size(); i++) {
            String[] parsed = GraphKeyUtils.parseNode(path.get(i));
            String dirKey = parsed[0];
            if (i == 0) { prevDir = dirKey; continue; }
            if (!dirKey.equals(prevDir)) {
                String transferStation = GraphKeyUtils.parseNode(path.get(i - 1))[1];
                String targetLineId = GraphKeyUtils.getLineIdFromDirKey(dirKey);
                // 步行时间可能包含出站换乘；候车时间由发车间隔和拥挤规则共同决定。
                // 步行时间（含出站换乘）
                double walkTime = StationUtils.getTransferTime(transferStation, prevDir, dirKey);
                // 等待时间（含拥挤度、发车间隔）
                double waitTime = computeTransferWaitTime(dirKey, travelTimeMillis, cache);
                total += walkTime + waitTime;
                prevDir = dirKey;
            }
        }
        return total;
    }

    // ---------- 子方法：停站时间 ----------
    private static double computeStopTime(List<String> path, MetroGraph metroGraph) {
        if (path == null || path.size() < 2) return 0;

        DataCache cache = DataCache.getInstance();
        Map<String, Double> stopTimeMap = cache.getStationStopTimeMap();
        double avgStopTime = cache.getAvgStopTime();
        double totalStopTime = 0.0;

        // 除去最后一个站点（终点站通常不停站或单独处理）
        for (int i = 0; i < path.size() - 1; i++) {
            String node = path.get(i);
            String[] parts = GraphKeyUtils.parseNode(node);
            if (parts.length < 2) continue;
            String dirKey = parts[0];
            String stationName = parts[1];

            // 尝试获取自定义停站时间
            Double customTime = null;
            if (stopTimeMap != null) {
                String key = stationName + "|" + dirKey;
                customTime = stopTimeMap.get(key);
            }
            if (customTime != null && customTime > 0) {
                totalStopTime += customTime;
            } else {
                // 使用全局平均值
                totalStopTime += avgStopTime;
            }
        }
        return totalStopTime;
    }

    // ---------- 核心：区间运行时间计算 ----------
    // 原来的方法签名保持不变，但实现改为调用 TrainTimeCalculator
    /** 结合限速和车辆参数计算单个区间的运行时间，返回秒数。 */
    public static double computeSegmentTime(double distance, int maxSpeedKmh,
                                            String lineId, String dirKey,
                                            String fromStation, String toStation) {
        DataCache cache = DataCache.getInstance();

        // 查询限速（可选）
        if (cache.getSpeedLimits() != null) {
            for (SpeedLimit sl : cache.getSpeedLimits()) {
                if (sl.directionKey.equals(dirKey) &&
                        sl.fromStation.equals(fromStation) &&
                        sl.toStation.equals(toStation)) {
                    maxSpeedKmh = sl.speedKmh;
                    break;
                }
            }
        }

        // 获取线路参数
        String formation = cache.getLineFormationMap() != null ?
                cache.getLineFormationMap().get(lineId) : null;
        if (formation == null) formation = "6B"; // 默认

        Double powerKw = cache.getLinePowerKwMap() != null ?
                cache.getLinePowerKwMap().get(lineId) : null;
        if (powerKw == null) powerKw = 2000.0;

        Double brake = cache.getLineBrakeMap() != null ?
                cache.getLineBrakeMap().get(lineId) : null;
        if (brake == null) brake = 0.85;

        // 调用功率模型
        return TrainTimeCalculator.computeSegmentTime(distance, formation,
                maxSpeedKmh, powerKw, brake);
    }

    // ---------- 辅助：加速度插值 ----------
    public static double getAutoAcceleration(int maxSpeedKmh) {
        if (maxSpeedKmh <= 80) return 0.55;
        if (maxSpeedKmh >= 160) return 0.38;

        for (int i = 0; i < SPEED_POINTS.length - 1; i++) {
            int v1 = SPEED_POINTS[i];
            int v2 = SPEED_POINTS[i + 1];
            double a1 = ACCEL_POINTS[i];
            double a2 = ACCEL_POINTS[i + 1];

            if (maxSpeedKmh > v1 && maxSpeedKmh <= v2) {
                double ratio = (double) (maxSpeedKmh - v1) / (v2 - v1);
                double accel = a1 - ratio * (a1 - a2);
                return Math.round(accel * 1000) / 1000.0;
            }
        }
        return 0.55;
    }

    /**
     * 根据方向键和当前时间获取拥挤度因子（默认为 1.0）
     */
    private static double findCrowdFactor(String dirKey, long travelTimeMillis, DataCache cache) {
        List<CrowdRule> rules = cache.getCrowdRules();
        if (rules == null || rules.isEmpty()) return 1.0;

        Calendar cal = Calendar.getInstance();
        cal.setTimeInMillis(travelTimeMillis);
        int dayOfWeek = cal.get(Calendar.DAY_OF_WEEK); // 1=Sunday, 2=Monday, ...
        int minutesOfDay = cal.get(Calendar.HOUR_OF_DAY) * 60 + cal.get(Calendar.MINUTE);

        for (CrowdRule rule : rules) {
            if (!rule.directionKey.equals(dirKey)) continue;
            // 检查星期
            if (rule.days != null && !rule.days.isEmpty()) {
                String dayName = getDayOfWeekString(dayOfWeek);
                if (!rule.days.contains(dayName)) continue;
            }
            // 检查时间范围（字符串 "HH:mm"）
            int startMin = parseTimeToMinutes(rule.startTime);
            int endMin = parseTimeToMinutes(rule.endTime);
            if (minutesOfDay >= startMin && minutesOfDay < endMin) {
                return rule.factor;
            }
        }
        return 1.0;
    }

    private static String getDayOfWeekString(int dayOfWeek) {
        switch (dayOfWeek) {
            case Calendar.MONDAY: return "Monday";
            case Calendar.TUESDAY: return "Tuesday";
            case Calendar.WEDNESDAY: return "Wednesday";
            case Calendar.THURSDAY: return "Thursday";
            case Calendar.FRIDAY: return "Friday";
            case Calendar.SATURDAY: return "Saturday";
            case Calendar.SUNDAY: return "Sunday";
            default: return "";
        }
    }

    private static int parseTimeToMinutes(String time) {
        String[] parts = time.split(":");
        return Integer.parseInt(parts[0]) * 60 + Integer.parseInt(parts[1]);
    }

    /**
     * 根据线路ID和当前时间获取发车间隔（秒），默认 120 秒
     */
    private static int findHeadway(String lineId, long travelTimeMillis, DataCache cache) {
        List<HeadwayRule> rules = cache.getHeadwayRules();
        if (rules == null || rules.isEmpty()) return 120;

        Calendar cal = Calendar.getInstance();
        cal.setTimeInMillis(travelTimeMillis);
        int dayOfWeek = cal.get(Calendar.DAY_OF_WEEK);
        int minutesOfDay = cal.get(Calendar.HOUR_OF_DAY) * 60 + cal.get(Calendar.MINUTE);

        for (HeadwayRule rule : rules) {
            if (!rule.lineId.equals(lineId)) continue;
            if (rule.days != null && !rule.days.isEmpty()) {
                String dayName = getDayOfWeekString(dayOfWeek);
                if (!rule.days.contains(dayName)) continue;
            }
            int start = parseTimeToMinutes(rule.startTime);
            int end = parseTimeToMinutes(rule.endTime);
            if (minutesOfDay >= start && minutesOfDay < end) {
                return rule.intervalSeconds;
            }
        }
        return 120;
    }

    /**
     * 计算在换乘站等待下一趟车的时间（秒）
     */
    public static double computeTransferWaitTime(String targetDirKey, long travelTimeMillis, DataCache cache) {
        String targetLineId = GraphKeyUtils.getLineIdFromDirKey(targetDirKey);
        int headway = findHeadway(targetLineId, travelTimeMillis, cache);
        double crowdFactor = findCrowdFactor(targetDirKey, travelTimeMillis, cache);
        double boardTime = cache.getDefaultBoardTime(); // 默认 3 秒
        // 公式：E[W] = T/2 + t_board + (crowdFactor - 1) * T
        return headway / 2.0 + boardTime + (crowdFactor - 1) * headway;
    }


    public static double findCrowdFactor(String dirKey, long travelTimeMillis) {
        DataCache cache = DataCache.getInstance();
        List<CrowdRule> rules = cache.getCrowdRules();
        if (rules == null || rules.isEmpty()) return 1.0;

        Calendar cal = Calendar.getInstance();
        cal.setTimeInMillis(travelTimeMillis);
        int dayOfWeek = cal.get(Calendar.DAY_OF_WEEK);
        int minutesOfDay = cal.get(Calendar.HOUR_OF_DAY) * 60 + cal.get(Calendar.MINUTE);

        for (CrowdRule rule : rules) {
            if (!rule.directionKey.equals(dirKey)) continue;
            if (rule.days != null && !rule.days.isEmpty()) {
                String dayName = getDayOfWeekString(dayOfWeek);
                if (!rule.days.contains(dayName)) continue;
            }
            int start = parseTimeToMinutes(rule.startTime);
            int end = parseTimeToMinutes(rule.endTime);
            if (minutesOfDay >= start && minutesOfDay < end) {
                return rule.factor;
            }
        }
        return 1.0;
    }
}
