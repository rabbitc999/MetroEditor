package com.rabbitc.subway.utils;

import android.util.Log;

/**
 * 坐标转换工具：WGS-84 ↔ GCJ-02
 * 高德/腾讯/谷歌中国使用 GCJ-02，OpenStreetMap 数据为 WGS-84
 */
public final class CoordinateUtils {

    private static final String TAG = "CoordinateUtils";
    private static final double PI = Math.PI;
    private static final double A = 6378245.0;
    private static final double EE = 0.00669342162296594323;

    private CoordinateUtils() {}

    /**
     * WGS-84 转 GCJ-02 (火星坐标系)
     * @param lon WGS-84 经度
     * @param lat WGS-84 纬度
     * @return double[2] = {gcjLon, gcjLat}
     */
    public static double[] wgs84ToGcj02(double lon, double lat) {
        if (outOfChina(lon, lat)) {
            return new double[]{lon, lat};
        }
        // 先计算经纬度偏移量，再按椭球参数换算成角度，返回 GCJ-02 坐标。
        double dLat = transformLat(lon - 105.0, lat - 35.0);
        double dLon = transformLon(lon - 105.0, lat - 35.0);
        double radLat = lat / 180.0 * PI;
        double magic = Math.sin(radLat);
        magic = 1 - EE * magic * magic;
        double sqrtMagic = Math.sqrt(magic);
        dLat = (dLat * 180.0) / ((A * (1 - EE)) / (magic * sqrtMagic) * PI);
        dLon = (dLon * 180.0) / (A / sqrtMagic * Math.cos(radLat) * PI);
        return new double[]{lon + dLon, lat + dLat};
    }

    /**
     * GCJ-02 转 WGS-84 (近似逆转换)
     * @param lon GCJ-02 经度
     * @param lat GCJ-02 纬度
     * @return double[2] = {wgsLon, wgsLat}
     */
    public static double[] gcj02ToWgs84(double lon, double lat) {
        if (outOfChina(lon, lat)) {
            return new double[]{lon, lat};
        }
        double[] gcj = wgs84ToGcj02(lon, lat);
        return new double[]{lon * 2 - gcj[0], lat * 2 - gcj[1]};
    }

    private static boolean outOfChina(double lon, double lat) {
        return lon < 72.004 || lon > 137.8347 || lat < 0.8293 || lat > 55.8271;
    }

    private static double transformLat(double x, double y) {
        double ret = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y + 0.2 * Math.sqrt(Math.abs(x));
        ret += (20.0 * Math.sin(6.0 * x * PI) + 20.0 * Math.sin(2.0 * x * PI)) * 2.0 / 3.0;
        ret += (20.0 * Math.sin(y * PI) + 40.0 * Math.sin(y / 3.0 * PI)) * 2.0 / 3.0;
        ret += (160.0 * Math.sin(y / 12.0 * PI) + 320.0 * Math.sin(y * PI / 30.0)) * 2.0 / 3.0;
        return ret;
    }

    private static double transformLon(double x, double y) {
        double ret = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y + 0.1 * Math.sqrt(Math.abs(x));
        ret += (20.0 * Math.sin(6.0 * x * PI) + 20.0 * Math.sin(2.0 * x * PI)) * 2.0 / 3.0;
        ret += (20.0 * Math.sin(x * PI) + 40.0 * Math.sin(x / 3.0 * PI)) * 2.0 / 3.0;
        ret += (150.0 * Math.sin(x / 12.0 * PI) + 300.0 * Math.sin(x / 30.0 * PI)) * 2.0 / 3.0;
        return ret;
    }

    /**
     * 智能转换：如果坐标在中国境内，则 WGS-84 → GCJ-02，否则保持不变
     */
    public static double[] toGcj02IfInChina(double lon, double lat) {
        if (outOfChina(lon, lat)) {
            Log.w(TAG, "坐标不在中国境内，不进行转换");
            return new double[]{lon, lat};
        }
        return wgs84ToGcj02(lon, lat);
    }

    /**
     * 计算两个经纬度坐标之间的球面距离（Haversine 公式）。
     *
     * @param lon1 经度1
     * @param lat1 纬度1
     * @param lon2 经度2
     * @param lat2 纬度2
     * @return 距离（米）
     */
    public static double haversineDistance(double lon1, double lat1, double lon2, double lat2) {
        final double EARTH_RADIUS = 6371000.0; // 地球平均半径（米）
        // Haversine 对短距离也稳定，适合站点附近检索而无需投影坐标系。
        double dLat = Math.toRadians(lat2 - lat1);
        double dLon = Math.toRadians(lon2 - lon1);
        double a = Math.sin(dLat / 2) * Math.sin(dLat / 2)
                + Math.cos(Math.toRadians(lat1)) * Math.cos(Math.toRadians(lat2))
                * Math.sin(dLon / 2) * Math.sin(dLon / 2);
        double c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
        return EARTH_RADIUS * c;
    }

    /**
     * 获取人类可读的距离文本。
     *
     * @param distanceMeters 距离（米）
     * @return 如 "850米" 或 "2.3公里"
     */
    public static String formatDistance(double distanceMeters) {
        return String.format("%.0f米", distanceMeters);
    }
}
