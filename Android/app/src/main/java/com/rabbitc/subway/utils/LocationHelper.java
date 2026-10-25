package com.rabbitc.subway.utils;

import android.Manifest;
import android.content.Context;
import android.content.pm.PackageManager;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;

import androidx.annotation.NonNull;
import androidx.core.content.ContextCompat;

import com.rabbitc.subway.data.DataCache;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Map;

/**
 * 位置工具类 — 获取当前 GPS 位置、查找最近地铁站。
 *
 * <p>核心功能：</p>
 * <ul>
 *   <li>{@link #getCurrentLocation(Context, long, OnLocationResult)} — 获取最后一次已知位置或监听新定位</li>
 *   <li>{@link #findNearbyStations(double, double, double, int)} — 查找指定范围内的站点（按距离升序）</li>
 *   <li>{@link #findNearestStation(double, double)} — 查找最近的一个站点</li>
 * </ul>
 *
 * <p>注意：站点坐标存储在 {@link DataCache#getStationCoords()}（GCJ-02 火星坐标），
 * GPS 原始坐标为 WGS-84，两者间误差约 50-500 米。对于 5km 范围内的站点查找，
 * 此误差可接受；如需精确匹配，调用方可先通过 {@link CoordinateUtils#toGcj02IfInChina} 转换。</p>
 *
 * @see CoordinateUtils#haversineDistance(double, double, double, double)
 */
public final class LocationHelper {

    /** 定位超时时间（毫秒） */
    public static final long LOCATION_TIMEOUT_MS = 10000;
    /** 默认搜索半径（米） */
    public static final double DEFAULT_RADIUS_METERS = 3000;

    private LocationHelper() {}

    // ======================== 数据结构 ========================

    /**
     * 附近站点信息 — 站点名与距离。
     */
    public static class NearbyStation {
        /** 站点名 */
        public final String name;
        /** 距离（米） */
        public final double distanceMeters;

        /**
         * 创建附近站点信息。
         */
        public NearbyStation(String name, double distanceMeters) {
            this.name = name;
            this.distanceMeters = distanceMeters;
        }

        /**
         * 获取距离显示文本。
         */
        public String getDistanceText() {
            return CoordinateUtils.formatDistance(distanceMeters);
        }
    }

    /**
     * 定位结果回调接口。
     */
    public interface OnLocationResult {
        /**
         * 定位成功。
         */
        void onLocation(double longitude, double latitude);

        /**
         * 定位失败。
         *
         * @param reason 失败原因
         */
        void onError(String reason);
    }

    // ======================== 权限检查 ========================

    /**
     * 检查是否已授予定位权限。
     *
     * @param context 上下文
     * @return {@code true} 已授予精确定位权限
     */
    public static boolean hasLocationPermission(Context context) {
        return ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION)
                == PackageManager.PERMISSION_GRANTED
                || ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_COARSE_LOCATION)
                == PackageManager.PERMISSION_GRANTED;
    }

    // ======================== 定位 ========================

    /**
     * 获取当前位置（异步）。
     *
     * <p>优先使用最后已知位置（快速返回）；否则监听 GPS/网络定位，
     * 超时（默认 10 秒）后回调失败。</p>
     *
     * @param context  上下文
     * @param timeoutMs 超时时间（毫秒）
     * @param callback 定位结果回调（主线程回调）
     */
    public static void getCurrentLocation(Context context, long timeoutMs, OnLocationResult callback) {
        if (!hasLocationPermission(context)) {
            callback.onError("未授予定位权限");
            return;
        }

        LocationManager locationManager =
                (LocationManager) context.getSystemService(Context.LOCATION_SERVICE);
        if (locationManager == null) {
            callback.onError("无法获取位置服务");
            return;
        }

        // 尝试最后已知位置
        Location lastLocation = null;
        try {
            if (locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER)) {
                lastLocation = locationManager.getLastKnownLocation(LocationManager.GPS_PROVIDER);
            }
            if (lastLocation == null && locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)) {
                lastLocation = locationManager.getLastKnownLocation(LocationManager.NETWORK_PROVIDER);
            }
        } catch (SecurityException e) {
            callback.onError("定位权限被拒绝");
            return;
        }

        // 最后已知位置足够新（5 分钟内）则直接使用
        if (lastLocation != null
                && System.currentTimeMillis() - lastLocation.getTime() < 5 * 60 * 1000) {
            callback.onLocation(lastLocation.getLongitude(), lastLocation.getLatitude());
            return;
        }

        // 监听新定位
        Handler mainHandler = new Handler(Looper.getMainLooper());
        final boolean[] finished = {false};

        LocationListener listener = new LocationListener() {
            @Override
            public void onLocationChanged(@NonNull Location location) {
                if (finished[0]) return;
                finished[0] = true;
                try {
                    locationManager.removeUpdates(this);
                } catch (SecurityException ignored) {
                }
                mainHandler.post(() ->
                        callback.onLocation(location.getLongitude(), location.getLatitude()));
            }

            @Override
            public void onStatusChanged(String provider, int status, Bundle extras) {
            }

            @Override
            public void onProviderEnabled(@NonNull String provider) {
            }

            @Override
            public void onProviderDisabled(@NonNull String provider) {
            }
        };

        try {
            locationManager.requestLocationUpdates(
                    LocationManager.GPS_PROVIDER, 0, 0, listener,
                    Looper.getMainLooper());
            if (locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)) {
                locationManager.requestLocationUpdates(
                        LocationManager.NETWORK_PROVIDER, 0, 0, listener,
                        Looper.getMainLooper());
            }
        } catch (SecurityException e) {
            callback.onError("定位权限被拒绝");
            return;
        }

        // 超时处理
        mainHandler.postDelayed(() -> {
            if (finished[0]) return;
            finished[0] = true;
            try {
                locationManager.removeUpdates(listener);
            } catch (SecurityException ignored) {
            }
            // 超时后回退到最后已知位置（即使较旧）
            Location fallback = null;
            try {
                fallback = locationManager.getLastKnownLocation(LocationManager.GPS_PROVIDER);
                if (fallback == null) {
                    fallback = locationManager.getLastKnownLocation(LocationManager.NETWORK_PROVIDER);
                }
            } catch (SecurityException ignored) {
            }
            if (fallback != null) {
                callback.onLocation(fallback.getLongitude(), fallback.getLatitude());
            } else {
                callback.onError("定位超时，请确保已开启 GPS 或网络定位");
            }
        }, timeoutMs);
    }

    // ======================== 附近站点查询 ========================

    /**
     * 查找指定范围内的站点（按距离升序排列）。
     *
     * @param longitude     当前位置经度（WGS-84，内部自动转 GCJ-02）
     * @param latitude      当前位置纬度（WGS-84，内部自动转 GCJ-02）
     * @param radiusMeters  搜索半径（米），默认 3000
     * @param maxCount      最多返回站点数
     * @return 附近站点列表（按距离升序），无数据返回空列表
     */
    public static List<NearbyStation> findNearbyStations(
            double longitude, double latitude, double radiusMeters, int maxCount) {

        List<NearbyStation> result = new ArrayList<>();
        Map<String, double[]> coords = DataCache.getInstance().getStationCoords();
        if (coords == null || coords.isEmpty()) {
            return result;
        }

        // GPS 坐标 (WGS-84) 转换为 GCJ-02 以匹配站点坐标系
        double[] gcj = CoordinateUtils.toGcj02IfInChina(longitude, latitude);

        for (Map.Entry<String, double[]> entry : coords.entrySet()) {
            double[] stationCoord = entry.getValue();
            if (stationCoord == null || stationCoord.length < 2) continue;

            double dist = CoordinateUtils.haversineDistance(
                    gcj[0], gcj[1], stationCoord[0], stationCoord[1]);

            if (dist <= radiusMeters) {
                result.add(new NearbyStation(entry.getKey(), dist));
            }
        }

        result.sort(Comparator.comparingDouble(s -> s.distanceMeters));

        if (result.size() > maxCount) {
            return new ArrayList<>(result.subList(0, maxCount));
        }
        return result;
    }

    /**
     * 查找最近的一个站点。
     *
     * @param longitude 当前位置经度（WGS-84）
     * @param latitude  当前位置纬度（WGS-84）
     * @return 最近的站点信息，无站点数据返回 {@code null}
     */
    public static NearbyStation findNearestStation(double longitude, double latitude) {
        List<NearbyStation> list = findNearbyStations(
                longitude, latitude, Double.MAX_VALUE, 1);
        return list.isEmpty() ? null : list.get(0);
    }
}
