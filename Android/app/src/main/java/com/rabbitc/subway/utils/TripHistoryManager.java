package com.rabbitc.subway.utils;

import android.content.Context;
import android.content.SharedPreferences;
import android.text.TextUtils;

import com.rabbitc.subway.data.RouteSegment;
import com.rabbitc.subway.data.TripHistory;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.List;

/**
 * 历史行程管理器 — 行程记录的增、删、查。
 *
 * <p>数据以 JSON 数组形式存储在 SharedPreferences 中，按时间倒序排列
 * （最近行程在前）。行程记录在路线规划成功后自动保存。</p>
 *
 * @see TripHistory
 */
public class TripHistoryManager {

    private static final String PREF_NAME = "trip_history";
    private static final String KEY_RECORDS = "records";
    private static final int MAX_RECORDS = 200;

    private final SharedPreferences prefs;

    public TripHistoryManager(Context context) {
        prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
    }

    public List<TripHistory> loadRecords() {
        String json = prefs.getString(KEY_RECORDS, "");
        // 历史记录是可选数据；首次使用或清空后返回空列表。
        if (TextUtils.isEmpty(json)) {
            return new ArrayList<>();
        }
        try {
            JSONArray array = new JSONArray(json);
            List<TripHistory> records = new ArrayList<>(array.length());
            // JSON 数组本身已经按时间倒序保存，因此无需额外排序。
            for (int i = 0; i < array.length(); i++) {
                JSONObject obj = array.getJSONObject(i);
                TripHistory record = new TripHistory();
                record.city = obj.optString("city", "");
                record.startStation = obj.getString("start");
                record.endStation = obj.getString("end");
                record.timestamp = obj.getLong("timestamp");
                record.distanceKm = obj.optDouble("distanceKm", 0);
                record.transfers = obj.optInt("transfers", 0);
                record.fare = obj.optDouble("fare", 0);
                record.timeMinutes = obj.optDouble("timeMinutes", 0);
                record.mode = obj.optString("mode", "");
                record.specialMoney = obj.optDouble("specialMoney", 0);
                record.stationCount = obj.optInt("stationCount", 0);
                record.stations = parseStringList(obj, "stations");
                record.pathNodes = parseStringList(obj, "pathNodes");
                record.segments = parseSegments(obj.optJSONArray("segments"));
                records.add(record);
            }
            return records;
        } catch (JSONException e) {
            e.printStackTrace();
            return new ArrayList<>();
        }
    }

    public void addRecord(TripHistory record) {
        List<TripHistory> records = loadRecords();
        // 新路线置顶，超出容量时丢弃最旧的尾部记录。
        records.add(0, record);
        if (records.size() > MAX_RECORDS) {
            records = new ArrayList<>(records.subList(0, MAX_RECORDS));
        }
        saveRecords(records);
    }

    public void removeRecord(int index) {
        List<TripHistory> records = loadRecords();
        if (index >= 0 && index < records.size()) {
            records.remove(index);
            saveRecords(records);
        }
    }

    /**
     * 删除行程信息完全相同的重复记录，仅保留最新一条。
     */
    public void removeDuplicateRoutes() {
        List<TripHistory> records = loadRecords();
        // LinkedHashMap 保留首次出现顺序，同时用时间戳挑出同一路线的最新记录。
        Map<String, TripHistory> unique = new LinkedHashMap<>();
        for (TripHistory record : records) {
            String key = buildRouteKey(record);
            TripHistory existing = unique.get(key);
            if (existing == null || record.timestamp > existing.timestamp) {
                unique.put(key, record);
            }
        }
        List<TripHistory> deduplicated = new ArrayList<>(unique.values());
        if (deduplicated.size() != records.size()) {
            deduplicated.sort((a, b) -> Long.compare(b.timestamp, a.timestamp));
            saveRecords(deduplicated);
        }
    }

    private static String buildRouteKey(TripHistory record) {
        StringBuilder sb = new StringBuilder();
        sb.append(record.startStation).append('|').append(record.endStation);
        sb.append('|').append(record.distanceKm);
        sb.append('|').append(record.transfers);
        sb.append('|').append(record.fare);
        sb.append('|').append(record.timeMinutes);
        sb.append('|').append(record.mode);
        if (record.pathNodes != null) {
            sb.append('|').append(String.join(",", record.pathNodes));
        }
        return sb.toString();
    }

    public void clearAll() {
        prefs.edit().remove(KEY_RECORDS).apply();
    }

    public int getCount() {
        return loadRecords().size();
    }

    public String exportAsJson() {
        List<TripHistory> records = loadRecords();
        JSONObject root = new JSONObject();
        JSONArray array = new JSONArray();
        try {
            for (TripHistory r : records) {
                array.put(recordToJson(r));
            }
            root.put("count", records.size());
            root.put("records", array);
        } catch (JSONException e) {
            e.printStackTrace();
        }
        return root.toString();
    }

    public int mergeFromJson(String json) {
        if (TextUtils.isEmpty(json)) return 0;
        try {
            JSONObject root = new JSONObject(json);
            JSONArray array = root.optJSONArray("records");
            if (array == null) return -1;

            List<TripHistory> existing = loadRecords();
            int added = 0;
            for (int i = 0; i < array.length(); i++) {
                JSONObject obj = array.getJSONObject(i);
                TripHistory record = new TripHistory();
                record.city = obj.optString("city", "");
                record.startStation = obj.getString("start");
                record.endStation = obj.getString("end");
                record.timestamp = obj.getLong("timestamp");
                record.distanceKm = obj.optDouble("distanceKm", 0);
                record.transfers = obj.optInt("transfers", 0);
                record.fare = obj.optDouble("fare", 0);
                record.timeMinutes = obj.optDouble("timeMinutes", 0);
                record.mode = obj.optString("mode", "");
                record.specialMoney = obj.optDouble("specialMoney", 0);
                record.stationCount = obj.optInt("stationCount", 0);
                record.stations = parseStringList(obj, "stations");
                record.pathNodes = parseStringList(obj, "pathNodes");
                record.segments = parseSegments(obj.optJSONArray("segments"));

                boolean duplicate = false;
                for (TripHistory e : existing) {
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
                // 远端记录合并后统一排序并执行容量裁剪。
                existing.sort((a, b) -> Long.compare(b.timestamp, a.timestamp));
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

    private static JSONObject recordToJson(TripHistory r) {
        JSONObject obj = new JSONObject();
        try {
            obj.put("city", r.city != null ? r.city : "");
            obj.put("start", r.startStation);
            obj.put("end", r.endStation);
            obj.put("timestamp", r.timestamp);
            obj.put("distanceKm", r.distanceKm);
            obj.put("transfers", r.transfers);
            obj.put("fare", r.fare);
            obj.put("timeMinutes", r.timeMinutes);
            obj.put("mode", r.mode != null ? r.mode : "");
            obj.put("specialMoney", r.specialMoney);
            obj.put("stationCount", r.stationCount);
            obj.put("stations", stringListToJson(r.stations));
            obj.put("pathNodes", stringListToJson(r.pathNodes));
            obj.put("segments", segmentsToJson(r.segments));
        } catch (JSONException e) {
            e.printStackTrace();
        }
        return obj;
    }

    private void saveRecords(List<TripHistory> records) {
        JSONArray array = new JSONArray();
        for (TripHistory r : records) {
            array.put(recordToJson(r));
        }
        prefs.edit().putString(KEY_RECORDS, array.toString()).apply();
    }

    private static List<String> parseStringList(JSONObject obj, String key) {
        List<String> result = new ArrayList<>();
        JSONArray array = obj.optJSONArray(key);
        if (array != null) {
            for (int i = 0; i < array.length(); i++) {
                result.add(array.optString(i, ""));
            }
        }
        return result;
    }

    private static JSONArray stringListToJson(List<String> list) {
        JSONArray array = new JSONArray();
        if (list != null) {
            for (String s : list) {
                array.put(s == null ? "" : s);
            }
        }
        return array;
    }

    private static List<RouteSegment> parseSegments(JSONArray array) {
        List<RouteSegment> segments = new ArrayList<>();
        if (array == null) return segments;
        for (int i = 0; i < array.length(); i++) {
            JSONObject obj = array.optJSONObject(i);
            if (obj == null) continue;
            segments.add(new RouteSegment(
                    obj.optString("startStation", ""),
                    obj.optString("lineId", ""),
                    obj.optString("lineName", ""),
                    obj.optString("direction", ""),
                    obj.optString("endStation", ""),
                    obj.optInt("stationCount", 0),
                    obj.optBoolean("isOutStationTransfer", false),
                    obj.optString("dirKey", "")
            ));
        }
        return segments;
    }

    private static JSONArray segmentsToJson(List<RouteSegment> segments) {
        JSONArray array = new JSONArray();
        if (segments == null) return array;
        for (RouteSegment seg : segments) {
            JSONObject obj = new JSONObject();
            try {
                obj.put("startStation", seg.startStation);
                obj.put("lineId", seg.lineId);
                obj.put("lineName", seg.lineName);
                obj.put("direction", seg.direction);
                obj.put("endStation", seg.endStation);
                obj.put("stationCount", seg.stationCount);
                obj.put("isOutStationTransfer", seg.isOutStationTransfer);
                obj.put("dirKey", seg.dirKey);
            } catch (JSONException e) {
                e.printStackTrace();
            }
            array.put(obj);
        }
        return array;
    }
}
