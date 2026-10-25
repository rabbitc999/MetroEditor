package com.rabbitc.subway.utils;

import android.content.Context;
import android.content.SharedPreferences;
import android.text.TextUtils;

import com.rabbitc.subway.data.CheckInRecord;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * 站点打卡管理器 — 打卡记录的增、删、查、导出。
 *
 * <p>数据以 JSON 数组形式存储在 SharedPreferences 中，按时间倒序排列
 * （最新打卡在前）。可通过 {@link #exportAsJson()} 导出全部记录，
 * 供 TCP 同步到 MetroEditor PC 端。</p>
 *
 * @see CheckInRecord
 */
public class CheckInManager {

    // ---------- 常量 ----------
    private static final String PREF_NAME = "station_checkins";
    private static final String KEY_RECORDS = "records";
    /** 最大保存条数，超出后丢弃最旧的记录 */
    private static final int MAX_RECORDS = 500;

    // ---------- 成员变量 ----------
    private final SharedPreferences prefs;

    // ---------- 构造方法 ----------

    /**
     * 创建打卡管理器。
     *
     * @param context Android 上下文
     */
    public CheckInManager(Context context) {
        prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
    }

    // ---------- 公共 API ----------

    /**
     * 加载所有打卡记录（按时间倒序，最新在前）。
     *
     * @return 打卡记录列表
     */
    public List<CheckInRecord> loadRecords() {
        String json = prefs.getString(KEY_RECORDS, "");
        // SharedPreferences 只保存字符串；空值直接视为“尚无记录”。
        if (TextUtils.isEmpty(json)) {
            return new ArrayList<>();
        }
        try {
            JSONArray array = new JSONArray(json);
            List<CheckInRecord> records = new ArrayList<>(array.length());
            // 按保存顺序恢复记录，调用方可直接用于倒序列表展示。
            for (int i = 0; i < array.length(); i++) {
                JSONObject obj = array.getJSONObject(i);
                CheckInRecord record = new CheckInRecord();
                record.city = obj.optString("city", "");
                record.station = obj.getString("station");
                record.timestamp = obj.getLong("timestamp");
                record.longitude = obj.optDouble("longitude", 0);
                record.latitude = obj.optDouble("latitude", 0);
                record.distance = obj.optDouble("distance", 0);
                record.note = obj.optString("note", "");
                record.essay = obj.optString("essay", "");
                record.date = obj.optString("date", "");
                record.imageBase64 = obj.optString("image", "");
                record.images = parseStringList(obj, "images");
                if (record.images.isEmpty() && !TextUtils.isEmpty(record.imageBase64)) {
                    record.images.add(record.imageBase64);
                }
                if (!record.images.isEmpty()) {
                    record.imageBase64 = record.images.get(0);
                }
                record.images = parseStringList(obj, "images");
                if (record.images.isEmpty() && !TextUtils.isEmpty(record.imageBase64)) {
                    record.images.add(record.imageBase64);
                }
                if (!record.images.isEmpty()) {
                    record.imageBase64 = record.images.get(0);
                }
                records.add(record);
            }
            return records;
        } catch (JSONException e) {
            e.printStackTrace();
            return new ArrayList<>();
        }
    }

    /**
     * 添加一条打卡记录（插入到最前），超出上限时移除最旧记录。
     *
     * @param record 打卡记录
     */
    public void addRecord(CheckInRecord record) {
        List<CheckInRecord> records = loadRecords();
        // 新打卡插入头部；裁剪尾部即可保留最新 MAX_RECORDS 条。
        records.add(0, record);
        if (records.size() > MAX_RECORDS) {
            records = new ArrayList<>(records.subList(0, MAX_RECORDS));
        }
        saveRecords(records);
    }

    /**
     * 更新一条打卡记录（按城市 + 站点 + 时间戳定位原记录）。
     */
    public void updateRecord(CheckInRecord oldRecord, CheckInRecord newRecord) {
        List<CheckInRecord> records = loadRecords();
        for (int i = 0; i < records.size(); i++) {
            if (records.get(i).sameAs(oldRecord)) {
                records.set(i, newRecord);
                saveRecords(records);
                return;
            }
        }
    }

    /**
     * 根据索引删除一条打卡记录。
     *
     * @param index 索引（从 0 开始）
     */
    public void removeRecord(int index) {
        List<CheckInRecord> records = loadRecords();
        if (index >= 0 && index < records.size()) {
            records.remove(index);
            saveRecords(records);
        }
    }

    /**
     * 清空全部打卡记录。
     */
    public void clearAll() {
        prefs.edit().remove(KEY_RECORDS).apply();
    }

    /**
     * 获取打卡记录数量。
     */
    public int getCount() {
        return loadRecords().size();
    }

    /**
     * 导出全部打卡记录为 JSON 字符串（用于 TCP 传输到 PC 端）。
     *
     * <p>格式：{"count": N, "records": [...]}</p>
     *
     * @return JSON 字符串
     */
    public String exportAsJson() {
        List<CheckInRecord> records = loadRecords();
        JSONObject root = new JSONObject();
        JSONArray array = new JSONArray();
        try {
            for (CheckInRecord r : records) {
                array.put(recordToJson(r));
            }
            root.put("count", records.size());
            root.put("records", array);
        } catch (JSONException e) {
            e.printStackTrace();
        }
        return root.toString();
    }

    /**
     * 从 JSON 合并导入打卡记录（用于接收 PC 端推送，按站点+时间戳去重）。
     *
     * <p>JSON 格式与 {@link #exportAsJson()} 相同。</p>
     *
     * @param json PC 端推送的 JSON 字符串
     * @return 合并导入的新增条数（-1 表示 JSON 解析失败）
     */
    public int mergeFromJson(String json) {
        if (TextUtils.isEmpty(json)) return 0;
        try {
            JSONObject root = new JSONObject(json);
            JSONArray array = root.optJSONArray("records");
            if (array == null) return -1;

            List<CheckInRecord> existing = loadRecords();
            int added = 0;
            for (int i = 0; i < array.length(); i++) {
                JSONObject obj = array.getJSONObject(i);
                CheckInRecord record = new CheckInRecord();
                record.city = obj.optString("city", "");
                record.station = obj.optString("station", "");
                record.timestamp = obj.optLong("timestamp", 0);
                record.longitude = obj.optDouble("longitude", 0);
                record.latitude = obj.optDouble("latitude", 0);
                record.distance = obj.optDouble("distance", 0);
                record.note = obj.optString("note", "");
                record.essay = obj.optString("essay", "");
                record.date = obj.optString("date", "");
                record.imageBase64 = obj.optString("image", "");
                record.images = parseStringList(obj, "images");
                if (record.images.isEmpty() && !TextUtils.isEmpty(record.imageBase64)) {
                    record.images.add(record.imageBase64);
                }
                if (!record.images.isEmpty()) {
                    record.imageBase64 = record.images.get(0);
                }

                if (record.station.isEmpty() || record.timestamp <= 0) continue;

                boolean duplicate = false;
                for (CheckInRecord e : existing) {
                    if (e.sameAs(record)) {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) {
                    existing.add(record);
                    added++;
                }
            }

            if (added > 0) {
                // 合并完成后重新排序并限长，保证本地与远端同步后的顺序一致。
                // 按时间戳倒序（最新在前）后保存
                Collections.sort(existing, (a, b) -> Long.compare(b.timestamp, a.timestamp));
                if (existing.size() > MAX_RECORDS) {
                    existing = new ArrayList<>(existing.subList(0, MAX_RECORDS));
                }
                saveRecords(existing);
            }
            return added;
        } catch (JSONException e) {
            e.printStackTrace();
            return -1;
        }
    }

    /**
     * 将单条记录转换为 JSON 对象。
     */
    private static JSONObject recordToJson(CheckInRecord r) {
        JSONObject obj = new JSONObject();
        try {
            obj.put("city", r.city != null ? r.city : "");
            obj.put("station", r.station);
            obj.put("timestamp", r.timestamp);
            obj.put("longitude", r.longitude);
            obj.put("latitude", r.latitude);
            obj.put("distance", r.distance);
            obj.put("note", r.note != null ? r.note : "");
            obj.put("essay", r.essay != null ? r.essay : "");
            obj.put("date", r.date != null ? r.date : "");
            obj.put("image", r.imageBase64 != null ? r.imageBase64 : "");
            JSONArray imagesArray = new JSONArray();
            if (r.images != null) {
                for (String image : r.images) {
                    if (!TextUtils.isEmpty(image)) imagesArray.put(image);
                }
            }
            obj.put("images", imagesArray);
        } catch (JSONException e) {
            e.printStackTrace();
        }
        return obj;
    }

    private static List<String> parseStringList(JSONObject obj, String key) {
        List<String> result = new ArrayList<>();
        JSONArray array = obj.optJSONArray(key);
        if (array != null) {
            for (int i = 0; i < array.length(); i++) {
                String value = array.optString(i, "");
                if (!TextUtils.isEmpty(value)) result.add(value);
            }
        }
        return result;
    }

    // ---------- 私有方法 ----------

    /**
     * 保存打卡记录列表（覆盖式写入）。
     */
    private void saveRecords(List<CheckInRecord> records) {
        JSONArray array = new JSONArray();
        for (CheckInRecord r : records) {
            array.put(recordToJson(r));
        }
        prefs.edit().putString(KEY_RECORDS, array.toString()).apply();
    }
}
