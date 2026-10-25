package com.rabbitc.subway.data;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;

/**
 * 站点打卡记录 — 记录用户在地铁站打卡的城市、时间与位置信息。
 *
 * <p>序列化为 JSON 存储在内部存储，可通过 TCP 同步导出到 MetroEditor PC 端。</p>
 *
 * @see com.rabbitc.subway.utils.CheckInManager
 */
public class CheckInRecord implements Serializable {

    private static final long serialVersionUID = 1L;

    /** 城市名称 */
    public String city;
    /** 站点名 */
    public String station;
    /** 打卡时间戳（毫秒） */
    public long timestamp;
    /** 打卡时的经度 */
    public double longitude;
    /** 打卡时的纬度 */
    public double latitude;
    /** 打卡时与站点的距离（米） */
    public double distance;
    /** 小作文（HTML 或 Markdown 文本） */
    public String essay;
    /** 备注（可选） */
    public String note;
    /** 打卡日期（yyyy-MM-dd，用户可选，默认打卡当天） */
    public String date;
    /** 打卡图片（Base64 JPEG，可选，来自相册或拍照，已压缩）；保留为第一张图片 */
    public String imageBase64;
    /** 打卡图片列表（支持多张） */
    public List<String> images;

    /**
     * 创建打卡记录。
     */
    public CheckInRecord() {
        images = new ArrayList<>();
    }

    /**
     * 创建打卡记录。
     *
     * @param station   站点名
     * @param timestamp 打卡时间戳（毫秒）
     * @param longitude 打卡时经度
     * @param latitude  打卡时纬度
     * @param distance  与站点距离（米）
     */
    public CheckInRecord(String station, long timestamp, double longitude,
                         double latitude, double distance) {
        this.city = "";
        this.station = station;
        this.timestamp = timestamp;
        this.longitude = longitude;
        this.latitude = latitude;
        this.distance = distance;
        this.images = new ArrayList<>();
    }

    /**
     * 获取格式化的距离文本。
     *
     * @return 如 "125米" 或 "1.2公里"；手动打卡（distance &lt; 0）返回 "手动打卡"
     */
    public String getDistanceText() {
        if (distance < 0) {
            return "手动打卡";
        }
        if (distance < 1000) {
            return String.format("%.0f米", distance);
        }
        return String.format("%.1f公里", distance / 1000);
    }

    /**
     * 判断两条记录是否为同一条（城市 + 站点 + 时间戳相同）。
     *
     * <p>用于双向同步合并时去重。</p>
     */
    public boolean sameAs(CheckInRecord other) {
        return other != null
                && timestamp == other.timestamp
                && city != null && city.equals(other.city)
                && station != null && station.equals(other.station);
    }

    @Override
    public String toString() {
        return "CheckInRecord{" +
                "city='" + city + '\'' +
                ", station='" + station + '\'' +
                ", timestamp=" + timestamp +
                ", distance=" + distance +
                ", date='" + date + '\'' +
                ", hasImage=" + (imageBase64 != null && !imageBase64.isEmpty()) +
                '}';
    }
}