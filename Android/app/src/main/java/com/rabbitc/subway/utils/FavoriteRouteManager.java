package com.rabbitc.subway.utils;

import android.content.Context;
import android.content.SharedPreferences;
import android.text.TextUtils;

import com.rabbitc.subway.data.FavoriteRoute;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/**
 * 收藏路线管理工具类，负责将用户收藏的路线（城市、起点、终点、标签）保存至 SharedPreferences。
 * <p>
 * 数据以 JSON 数组形式存储，支持增、删、查、改操作。
 * </p>
 */
public class FavoriteRouteManager {

    // ---------- 常量 ----------
    private static final String PREF_NAME = "favorite_routes";
    private static final String KEY_ROUTES = "routes";

    // ---------- 成员变量 ----------
    private final SharedPreferences prefs;

    // ---------- 构造方法 ----------
    public FavoriteRouteManager(Context context) {
        prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
    }

    // ---------- 公共API ----------

    /**
     * 加载所有收藏路线。
     *
     * @return 收藏路线列表，若无数据则返回空列表
     */
    public List<FavoriteRoute> loadRoutes() {
        String json = prefs.getString(KEY_ROUTES, "");
        // 收藏为空时返回可变列表，调用方可直接追加或删除。
        if (TextUtils.isEmpty(json)) {
            return new ArrayList<>();
        }
        try {
            JSONArray array = new JSONArray(json);
            List<FavoriteRoute> routes = new ArrayList<>(array.length());
            for (int i = 0; i < array.length(); i++) {
                JSONObject obj = array.getJSONObject(i);
                String keyCity = obj.getString("keyCity");
                String start = obj.getString("start");
                String end = obj.getString("end");
                String label = obj.optString("label", "");
                routes.add(new FavoriteRoute(keyCity, start, end, label));
            }
            return routes;
        } catch (JSONException e) {
            e.printStackTrace();
            return new ArrayList<>();
        }
    }

    /**
     * 保存收藏路线列表（覆盖式写入）。
     *
     * @param routes 路线列表
     */
    public void saveRoutes(List<FavoriteRoute> routes) {
        JSONArray array = new JSONArray();
        // 采用整体覆盖写入，避免单条更新时残留已删除的旧记录。
        for (FavoriteRoute route : routes) {
            JSONObject obj = new JSONObject();
            try {
                obj.put("keyCity", route.keyCity);
                obj.put("start", route.start);
                obj.put("end", route.end);
                obj.put("label", route.label != null ? route.label : "");
            } catch (JSONException e) {
                e.printStackTrace();
            }
            array.put(obj);
        }
        prefs.edit().putString(KEY_ROUTES, array.toString()).apply();
    }

    /**
     * 添加一条收藏路线，若已存在相同（城市、起点、终点）的路线，则覆盖更新。
     *
     * @param route 要添加的路线
     */
    public void addRoute(FavoriteRoute route) {
        List<FavoriteRoute> routes = loadRoutes();
        // 同一城市的同一起终点只保留一条，新的标签和内容覆盖旧值。
        // 移除重复项（同一城市、起点、终点）
        routes.removeIf(r -> r.start.equals(route.start)
                && r.end.equals(route.end)
                && r.keyCity.equals(route.keyCity));
        routes.add(route);
        saveRoutes(routes);
    }

    /**
     * 根据索引删除一条收藏路线。
     *
     * @param index 要删除的路线的索引（从 0 开始），若越界则不做任何操作
     */
    public void removeRoute(int index) {
        List<FavoriteRoute> routes = loadRoutes();
        if (index >= 0 && index < routes.size()) {
            routes.remove(index);
            saveRoutes(routes);
        }
    }
}
